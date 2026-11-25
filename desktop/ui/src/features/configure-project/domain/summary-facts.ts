import { plural } from '@/shared/utils';

import type { SummaryFact, SummaryInput } from '../types/summary';
import { aiEnvironmentNames } from './ai-environments';

const STEPS_WITH_APPROVALS = ['review', 'run', 'result'];

/** Os mesmos números da Revisão: arquivos que mudam (preservar não conta) e comandos, nunca "alterações" soltas. */
function planText(plan: SummaryInput['plan']): string {
  if (plan.status !== 'ready') return 'ainda não gerado';
  const files = plan.data.changes.filter((change) => change.action !== 'preserve').length;
  const commands = plan.data.commands.length;
  const parts = [plural(files, 'arquivo', 'arquivos')];
  if (commands > 0) parts.push(plural(commands, 'comando', 'comandos'));
  return parts.join(' · ');
}

export function buildSummaryFacts({
  step,
  projectName,
  aiEnvironments,
  plan,
  approvalCount
}: SummaryInput): SummaryFact[] {
  const environments = aiEnvironments.map((id) => aiEnvironmentNames[id] ?? id).join(', ');
  return [
    ['Projeto', projectName],
    [aiEnvironments.length > 1 ? 'Ambientes de IA' : 'Ambiente de IA', environments || 'a escolher'],
    ['Plano', planText(plan)],
    ['Aprovados', STEPS_WITH_APPROVALS.includes(step) ? `${approvalCount}` : 'não se aplica']
  ];
}
