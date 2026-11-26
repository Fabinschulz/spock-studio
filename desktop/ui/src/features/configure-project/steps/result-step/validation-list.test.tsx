import type { EvalResult } from '@/shared/schema';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { ValidationList, validationSummary } from './validation-list';

const result = (over: Partial<EvalResult>): EvalResult => ({
  id: 'EV-001',
  name: 'Estrutura presente',
  property: 'O projeto tem a estrutura esperada',
  kind: 'deterministic',
  required: true,
  status: 'passed',
  criteria: 'Todos os arquivos obrigatórios existem',
  evidence: ['AGENTS.md'],
  reason: null,
  limitations: [],
  ...over
});

describe('ValidationList', () => {
  it('resume só os estados que existem', () => {
    expect(validationSummary([result({}), result({}), result({ status: 'failed' })])).toBe(
      '2 de 3 aprovados · 1 reprovado'
    );
    expect(validationSummary([result({})])).toBe('1 de 1 aprovado');
    expect(validationSummary([result({}), result({ status: 'blocked' }), result({ status: 'blocked' })])).toBe(
      '1 de 3 aprovados · 2 bloqueados'
    );
  });

  it('o que pede atenção vem primeiro e aberto; as aprovadas ficam numa linha recolhida', () => {
    const report = {
      outcome: 'failed' as const,
      results: [
        result({ id: 'EV-001', name: 'Passou' }),
        result({ id: 'EV-002', name: 'Quebrou', status: 'failed', reason: 'arquivo ausente' }),
        result({ id: 'EV-003', name: 'Travou', status: 'blocked' })
      ]
    };
    const { container } = render(<ValidationList report={report} />);

    const names = [...container.querySelectorAll('summary')].map((node) => node.textContent);
    expect(names).toEqual(['QuebrouReprovado', 'TravouBloqueado', '1 aprovada', 'PassouAprovado']);
    const open = [...container.querySelectorAll('details')].map((node) => node.open);
    expect(open).toEqual([true, true, false, false]);
  });

  it('expande para mostrar critério, evidências e limites', async () => {
    const report = {
      outcome: 'passed_with_limitations' as const,
      results: [result({ limitations: ['sem juiz por modelo'], evidence: ['AGENTS.md', 'CLAUDE.md'] })]
    };
    const { container } = render(<ValidationList report={report} />);
    await userEvent.click(screen.getByText('1 aprovada'));
    const details = container.querySelectorAll('details')[1]!;
    expect(details.open).toBe(false);

    await userEvent.click(screen.getByText('Estrutura presente'));

    expect(details.open).toBe(true);
    expect(screen.getByText(/Critério: Todos os arquivos/)).toBeTruthy();
    expect(screen.getByText('CLAUDE.md')).toBeTruthy();
    expect(screen.getByText(/Limite: sem juiz por modelo/)).toBeTruthy();
  });

  it('diz quem confere e o que pesa no resultado, sem jargão; o código fica no fim', () => {
    const report = {
      outcome: 'failed' as const,
      results: [
        result({ id: 'EV-SPEC-001', status: 'failed' }),
        result({ id: 'EV-JUDGE-001', kind: 'model', required: false, status: 'blocked' })
      ]
    };
    render(<ValidationList report={report} />);

    expect(screen.queryByText(/determinístico|juiz por modelo/)).toBeNull();
    expect(
      screen.getByText(
        'Conferida por uma regra fixa do Spock, que dá sempre o mesmo resultado. Obrigatória: se for reprovada, a aplicação conta como falha.'
      )
    ).toBeTruthy();
    expect(
      screen.getByText(
        /Conferida por um modelo de IA.*Opcional: se for reprovada, a aplicação conclui com limitações\./
      )
    ).toBeTruthy();
    expect(screen.getByText('Código da verificação: EV-SPEC-001')).toBeTruthy();
  });

  it('cada verificação diz o estado em texto, não só pela cor', () => {
    render(<ValidationList report={{ outcome: 'passed', results: [result({})] }} />);
    expect(screen.getByText('Estrutura presente')).toBeTruthy();
    expect(screen.getByText('Aprovado')).toBeTruthy();
  });
});
