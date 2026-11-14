import type { ConfigurationPlan } from '@/shared/schema';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { decisionsSummary } from './decisions-list';
import { PlanStep } from './plan-step';

type Decision = ConfigurationPlan['decisions'][number];

const decision = (over: Partial<Decision>): Decision => ({
  componentId: 'agent.claude-code.harness-engineer',
  kind: 'agent',
  decision: 'enable',
  origin: 'baseline-rules',
  justification: 'Planeja a configuração no ambiente de IA',
  evidence: ['regra-base 12'],
  ...over
});

const plan = (decisions: Decision[], over: Partial<ConfigurationPlan> = {}) =>
  ({
    proposalOrigin: 'baseline',
    decisions,
    limitations: [],
    observations: [],
    risks: [],
    ...over
  }) as unknown as ConfigurationPlan;

describe('PlanStep', () => {
  it('resume as decisões que existem, sem listar zeros', () => {
    expect(
      decisionsSummary([decision({}), decision({ componentId: 'b' }), decision({ componentId: 'c', decision: 'keep' })])
    ).toBe('3 decisões · 2 habilitar · 1 manter');
  });

  it('mostra a justificativa na linha e a origem e as evidências só ao expandir', async () => {
    const { container } = render(<PlanStep plan={plan([decision({})])} onBack={vi.fn()} onContinue={vi.fn()} />);

    expect(screen.getByText('Planeja a configuração no ambiente de IA')).toBeTruthy();
    expect(screen.getByText('Habilitar')).toBeTruthy();
    const details = container.querySelector('details')!;
    expect(details.open).toBe(false);

    await userEvent.click(screen.getByText('Agente'));

    expect(details.open).toBe(true);
    expect(screen.getByText('Origem: Regras-base')).toBeTruthy();
    expect(screen.getByText('regra-base 12')).toBeTruthy();
  });

  it('regra que vem da arquitetura aprovada diz de onde veio e aponta o documento', async () => {
    render(
      <PlanStep
        plan={plan([
          decision({
            componentId: 'mariliz.rule.stack-dotnet',
            kind: 'rule',
            origin: 'architecture',
            justification:
              'Stack .NET planejada na arquitetura aprovada (ASP.NET Core 9); ainda sem arquivos no projeto.',
            evidence: ['.mariliz/arquitetura/arquitetura.md']
          })
        ])}
        onBack={vi.fn()}
        onContinue={vi.fn()}
      />
    );

    await userEvent.click(screen.getByText('Regra'));

    expect(screen.getByText('Origem: Arquitetura aprovada (.mariliz/arquitetura)')).toBeTruthy();
    expect(screen.getByText('.mariliz/arquitetura/arquitetura.md')).toBeTruthy();
  });

  it('componente bloqueado já vem aberto', () => {
    const { container } = render(
      <PlanStep
        plan={plan([decision({ decision: 'blocked' }), decision({ componentId: 'x' })])}
        onBack={vi.fn()}
        onContinue={vi.fn()}
      />
    );
    expect([...container.querySelectorAll('details')].map((node) => node.open)).toEqual([true, false]);
  });

  it('limitações, observações e riscos aparecem só quando existem, e continuar vai para a revisão', async () => {
    const onContinue = vi.fn();
    render(
      <PlanStep
        plan={plan([decision({})], {
          limitations: [{ code: 'X', message: 'sem proposta do agente' }],
          observations: [{ kind: 'inference', text: 'usa dotnet', evidence: [] }],
          risks: ['pode sobrescrever regras']
        })}
        onBack={vi.fn()}
        onContinue={onContinue}
      />
    );

    expect(screen.getByText('sem proposta do agente')).toBeTruthy();
    expect(screen.getByText('Inferência')).toBeTruthy();
    expect(screen.getByText('pode sobrescrever regras')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Revisar alterações' }));
    expect(onContinue).toHaveBeenCalledTimes(1);
  });
});
