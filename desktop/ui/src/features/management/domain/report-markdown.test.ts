import reportFixture from '@contracts/fixtures/management/report.json';
import { describe, expect, it } from 'vitest';

import { projectReportSchema, type ProjectReport } from '@/shared/schema';

import { reportToMarkdown } from './report-markdown';

const report = projectReportSchema.parse(reportFixture);
const label = (kind: string) => (kind === 'issues' ? 'Issues' : kind);
const lines = (value: ProjectReport) => reportToMarkdown(value, label).split('\n');

describe('relatório em Markdown', () => {
  it('abre com o projeto e o período, em datas por extenso do motor', () => {
    const text = lines(report);

    expect(text[0]).toBe('# Relatório do período: loja');
    expect(text[2]).toMatch(/^Período: \d{2}\/\d{2}\/2026 a \d{2}\/\d{2}\/2026 \(4 semanas\)\.$/);
  });

  it('segue a ordem e os nomes das seções da tela, que seguem as abas do Acompanhamento', () => {
    const headings = lines(report).filter((line) => line.startsWith('## '));

    expect(headings).toEqual([
      '## Previsão de entrega',
      '## Fluxo nas últimas 4 semanas',
      '## Marcos',
      '## Riscos',
      '## Mudanças de risco no período',
      '## Decisões do período',
      '## Execuções do período'
    ]);
  });

  it('traz o fluxo de cada tipo com o valor do motor e o motivo quando não há valor', () => {
    const text = reportToMarkdown(report, label);

    expect(text).toContain('### Issues');
    expect(text).toContain('- Entregues por semana (média): 1,5');
    expect(text).toContain('- Tempo de trabalho, do início à entrega (mediana): 3 dias');
    expect(text).toMatch(/- Bloqueados: 1\b/);
    // Sem histórico de status o cycle time vem com o motivo, e não como zero.
    const noHistory = {
      ...report,
      metrics: {
        ...report.metrics,
        kinds: report.metrics.kinds.map((flow) => ({
          ...flow,
          cycleTime: {
            ...flow.cycleTime,
            medianDays: null,
            p85Days: null,
            unavailable: 'A fonte não informa quando o trabalho começou.'
          }
        }))
      }
    };
    expect(reportToMarkdown(noHistory, label)).toContain(
      '- Tempo de trabalho, do início à entrega (mediana): sem valor (A fonte não informa quando o trabalho começou.)'
    );
  });

  it('lista a previsão com as datas dos cenários', () => {
    const text = reportToMarkdown(report, label);

    expect(text).toContain('### Issues: 1 aberto');
    expect(text).not.toContain('1 abertos');
    expect(text).toContain('- Provável: até 03/12/2026');
    expect(text).toContain('- Conservador: até 31/12/2026');
  });

  it('lista os riscos de agora, as mudanças do período, as decisões e as execuções', () => {
    const text = reportToMarkdown(report, label);

    expect(text).toContain('## Riscos');
    expect(text).toContain('## Mudanças de risco no período');
    expect(text).toContain('Adiar a release. Decisão de Adiar a release');
    expect(text).toContain('- v1.0 (Issues, prazo 12/12/2026): 4 de 5 entregues, 1 aberto. Situação: em risco.');
    expect(text).toContain('- Configuração aplicada · Concluído · 1 execução');
    expect(text).toContain('- Pedido de engenharia · Falhou · 1 execução');
  });

  it('seção vazia diz que está vazia em vez de sumir', () => {
    const empty: ProjectReport = {
      ...report,
      riskChanges: [],
      decisions: [],
      runs: [],
      risks: { ...report.risks, risks: [] },
      milestones: { ...report.milestones, milestones: [] }
    };

    const text = reportToMarkdown(empty, label);

    expect(text).toContain('Nenhuma regra de risco disparou.');
    expect(text).toContain('Nenhuma mudança de risco registrada no período.');
    expect(text).toContain('Nenhuma decisão registrada no período.');
    expect(text).toContain('Nenhuma execução começou no período.');
    expect(text).toContain('Nenhum item pertence a um marco.');
  });

  it('sem dado, previsão, fluxo e riscos mantêm a seção e usam a frase do Acompanhamento', () => {
    const noData: ProjectReport = {
      ...report,
      forecast: { ...report.forecast, kinds: [] },
      metrics: { ...report.metrics, kinds: [] },
      risks: { ...report.risks, hasData: false, risks: [] }
    };

    const body = reportToMarkdown(noData, label);

    expect(body).toContain('## Previsão de entrega\n\nSem dados para calcular a previsão ainda.');
    expect(body).toContain('## Fluxo nas últimas 4 semanas\n\nSem dados para calcular o fluxo ainda.');
    expect(body).toContain('## Riscos\n\nSem dados para detectar riscos ainda.');
    expect(body).not.toContain('Nenhuma regra de risco disparou.');
    // Sem fonte lida, o aviso de fonte incompleta seria falso: a tela só o mostra quando há números.
    expect(reportToMarkdown({ ...noData, metrics: { ...noData.metrics, complete: false } }, label)).not.toContain(
      'incompleta'
    );
  });

  it('avisa quando uma fonte está incompleta e fecha com a base do motor', () => {
    const incomplete = { ...report, metrics: { ...report.metrics, complete: false } };

    const text = reportToMarkdown(incomplete, label);

    expect(text).toContain('Atenção: ao menos uma fonte está incompleta');
    expect(text.trimEnd().endsWith(report.basis)).toBe(true);
  });

  it('não escreve nenhum número que o motor não tenha devolvido', () => {
    const text = reportToMarkdown(report, label);

    expect(text).not.toContain('NaN');
    expect(text).not.toContain('undefined');
    expect(text).not.toContain('null');
  });
});
