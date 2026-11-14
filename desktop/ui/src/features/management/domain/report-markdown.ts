import type { ProjectReport } from '@/shared/schema';
import { runKindLabel, runStateLabel } from '@/shared/utils';

import { milestoneOutlookLabel, severityLabel } from './delivery-labels';
import { formatDay, formatDays, formatDecimal } from './format';
import { describeRiskChange } from './risk-history-text';

const day = (iso: string) => formatDay(iso.slice(0, 10));

/** Um indicador sem base nunca vira zero no texto. */
function value(text: string | null, unavailable: string | null): string {
  return text !== null ? text : `sem valor (${unavailable ?? 'sem base'})`;
}

/** Escrito só a partir do que o motor devolveu: nenhum número é calculado aqui e nenhum trecho é escrito por modelo. */
export function reportToMarkdown(report: ProjectReport, kindLabel: (kind: string) => string): string {
  const lines: string[] = [
    `# Relatório do período: ${report.projectName}`,
    '',
    `Período: ${day(report.periodFrom)} a ${day(report.periodTo)} (${report.weeks} semanas).`,
    ''
  ];

  lines.push('## Fluxo');
  if (report.metrics.kinds.length === 0) lines.push('', 'Sem itens sincronizados.');
  if (!report.metrics.complete)
    lines.push('', 'Atenção: ao menos uma fonte está incompleta; os números valem para o que chegou.');
  for (const flow of report.metrics.kinds) {
    lines.push(
      '',
      `### ${kindLabel(flow.kind)}`,
      `- Entregues por semana (média): ${value(flow.throughput.weeklyAverage === null ? null : formatDecimal(flow.throughput.weeklyAverage), flow.throughput.unavailable)}`,
      `- Lead time mediano: ${value(flow.leadTime.medianDays === null ? null : formatDays(flow.leadTime.medianDays), flow.leadTime.unavailable)}`,
      `- Cycle time mediano: ${value(flow.cycleTime.medianDays === null ? null : formatDays(flow.cycleTime.medianDays), flow.cycleTime.unavailable)}`,
      `- Em andamento: ${value(flow.wip.value === null ? null : String(flow.wip.value), flow.wip.unavailable)}`,
      `- Bloqueados: ${value(flow.blocked.value === null ? null : String(flow.blocked.value), flow.blocked.unavailable)}`,
      `- Prazo vencido: ${value(flow.overdue.value === null ? null : String(flow.overdue.value), flow.overdue.unavailable)}`
    );
  }

  lines.push('', '## Previsão');
  if (report.forecast.kinds.length === 0) lines.push('', 'Sem itens sincronizados.');
  for (const forecast of report.forecast.kinds) {
    lines.push(
      '',
      `### ${kindLabel(forecast.kind)}: ${forecast.remaining} ${forecast.remaining === 1 ? 'aberto' : 'abertos'}`
    );
    if (forecast.unavailable !== null) lines.push(`- Sem previsão: ${forecast.unavailable}`);
    for (const scenario of forecast.scenarios) lines.push(`- ${scenario.label}: até ${formatDay(scenario.by)}`);
  }

  lines.push('', '## Marcos');
  if (report.milestones.milestones.length === 0) lines.push('', 'Nenhum item pertence a um marco.');
  for (const milestone of report.milestones.milestones) {
    const due = milestone.dueDate ? `prazo ${formatDay(milestone.dueDate)}` : 'sem prazo';
    lines.push(
      `- ${milestone.name} (${kindLabel(milestone.kind)}, ${due}): ${milestone.delivered} de ${milestone.counted} entregues, ` +
        `${milestone.open} ${milestone.open === 1 ? 'aberto' : 'abertos'}. Situação: ${milestoneOutlookLabel(milestone.outlook).label.toLowerCase()}.`
    );
  }

  lines.push('', '## Riscos agora');
  if (report.risks.risks.length === 0) lines.push('', 'Nenhuma regra de risco disparou.');
  for (const risk of report.risks.risks) {
    lines.push(`- ${risk.title} (gravidade ${severityLabel(risk.severity).label}): ${risk.evidence.join(' ')}`);
  }

  lines.push('', '## Mudanças de risco no período');
  if (report.riskChanges.length === 0) lines.push('', 'Nenhuma mudança registrada no período.');
  for (const item of report.riskChanges) {
    lines.push(`- ${day(item.change.at)}: ${item.title}. ${describeRiskChange(item.change)}`);
  }

  lines.push('', '## Decisões do período');
  if (report.decisions.length === 0) lines.push('', 'Nenhuma decisão registrada no período.');
  for (const decision of report.decisions)
    lines.push(`- ${day(decision.recordedAt)}: ${decision.title}. ${decision.decision}`);

  lines.push('', '## Execuções do período');
  if (report.runs.length === 0) lines.push('', 'Nenhuma execução no período.');
  for (const run of report.runs)
    lines.push(`- ${runKindLabel(run.kind)}, ${runStateLabel(run.state).toLowerCase()}: ${run.count}`);

  lines.push('', '---', '', report.basis, '');
  return lines.join('\n');
}
