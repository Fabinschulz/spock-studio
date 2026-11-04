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
    expect(validationSummary([result({})])).toBe('1 de 1 aprovados');
  });

  it('abre sozinha a verificação reprovada ou bloqueada e deixa a aprovada recolhida', () => {
    const report = {
      outcome: 'failed' as const,
      results: [
        result({ id: 'EV-001', name: 'Passou' }),
        result({ id: 'EV-002', name: 'Quebrou', status: 'failed', reason: 'arquivo ausente' }),
        result({ id: 'EV-003', name: 'Travou', status: 'blocked' })
      ]
    };
    const { container } = render(<ValidationList report={report} />);

    const open = [...container.querySelectorAll('details')].map((node) => node.open);
    expect(open).toEqual([false, true, true]);
  });

  it('expande para mostrar critério, evidências e limites', async () => {
    const report = {
      outcome: 'passed_with_limitations' as const,
      results: [result({ limitations: ['sem juiz por modelo'], evidence: ['AGENTS.md', 'CLAUDE.md'] })]
    };
    const { container } = render(<ValidationList report={report} />);
    const details = container.querySelector('details')!;
    expect(details.open).toBe(false);

    await userEvent.click(screen.getByText('Estrutura presente'));

    expect(details.open).toBe(true);
    expect(screen.getByText(/Critério: Todos os arquivos/)).toBeTruthy();
    expect(screen.getByText('CLAUDE.md')).toBeTruthy();
    expect(screen.getByText(/Limite: sem juiz por modelo/)).toBeTruthy();
  });

  it('cada verificação diz o estado em texto, não só pela cor', () => {
    render(<ValidationList report={{ outcome: 'passed', results: [result({})] }} />);
    expect(screen.getByText('Estrutura presente')).toBeTruthy();
    expect(screen.getByText('Aprovado')).toBeTruthy();
  });
});
