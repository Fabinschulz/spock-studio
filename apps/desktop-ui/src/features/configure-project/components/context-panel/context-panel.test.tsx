import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import applyResultJson from '@contracts/fixtures/golden/apply-result.json';
import planJson from '@contracts/fixtures/golden/plan.json';

import { planSchema, runResultSchema } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';

import type { JourneyResults } from '../../types/journey';
import type { RunView } from '../../types/run';
import { ContextPanel } from './context-panel';

const plan = planSchema.parse(planJson);
const apply = runResultSchema.parse(applyResultJson);
const noResults: JourneyResults = { apply: null, revert: null, validate: null };

const base = {
  step: 'plan' as const,
  projectName: 'empreganet-api',
  aiEnvironment: 'claude-code',
  plan: { status: 'ready' as const, data: plan },
  approvalCount: 11,
  run: null,
  results: noResults
};

const participants = () => within(screen.getByRole('region', { name: 'Participantes' })).getAllByRole('listitem');

describe('ContextPanel', () => {
  it('resume o que já foi decidido, com os dados da jornada', () => {
    render(<ContextPanel {...base} step="review" />);
    const summary = within(screen.getByRole('region', { name: 'Resumo' }));

    expect(summary.getByText('empreganet-api')).toBeInTheDocument();
    expect(summary.getByText('Claude Code')).toBeInTheDocument();
    expect(summary.getByText(`${plan.changes.length} alterações`)).toBeInTheDocument();
    expect(summary.getByText('11')).toBeInTheDocument();
  });

  it('antes do plano, diz que ele ainda não foi gerado e não mostra o Harness Engineer', () => {
    render(<ContextPanel {...base} step="model" plan={{ status: 'idle' }} approvalCount={0} aiEnvironment={null} />);

    expect(screen.getByText('ainda não gerado')).toBeInTheDocument();
    expect(screen.getByText('a escolher')).toBeInTheDocument();
    expect(participants()).toHaveLength(1);
    expect(participants()[0]).toHaveTextContent('Motor Mariliz');
  });

  it('o Harness Engineer aparece como "aguardando proposta" (fato do plano), sem inventar atividade', () => {
    render(<ContextPanel {...base} />);
    const harness = participants().find((li) => li.textContent?.includes('Harness Engineer'));

    expect(harness).toHaveTextContent('Aguardando proposta');
    expect(harness).toHaveTextContent('Roda no seu ambiente de IA');
    expect(harness).not.toHaveTextContent('Em atividade');
  });

  it('com proposta do Harness Engineer, diz que a proposta foi recebida', () => {
    const withProposal = { ...plan, proposalOrigin: 'harness-engineer', limitations: [] };
    render(<ContextPanel {...base} plan={{ status: 'ready', data: withProposal }} />);

    expect(participants().find((li) => li.textContent?.includes('Harness Engineer'))).toHaveTextContent(
      'Proposta recebida'
    );
  });

  it('durante a execução, o motor está em atividade na etapa que ele publicou, e o feed mostra os eventos', () => {
    const run: RunView = {
      kind: 'apply',
      runId: 'r1',
      state: 'running',
      steps: [
        {
          id: 'write',
          title: 'Aplicar as alterações aprovadas',
          status: 'running',
          detail: null,
          startedAt: 2000,
          endedAt: null
        }
      ],
      files: [],
      feed: [{ at: 2000, text: 'Iniciou: Aplicar as alterações aprovadas' }],
      startedAt: 1000,
      error: null
    };
    render(<ContextPanel {...base} step="run" run={run} />);

    const engine = participants()[0]!;
    expect(engine).toHaveTextContent('Em atividade');
    expect(engine).toHaveTextContent('Aplicar as alterações aprovadas');
    expect(
      within(screen.getByRole('region', { name: 'Atividade' })).getByText('Iniciou: Aplicar as alterações aprovadas')
    ).toBeInTheDocument();
    expect(screen.getByText('0:01')).toBeInTheDocument();
  });

  it('uma execução com erro mostra o motor como falho, nunca como em atividade', () => {
    const run: RunView = {
      kind: 'apply',
      runId: 'r1',
      state: 'failed',
      steps: [],
      files: [],
      feed: [],
      startedAt: 0,
      error: 'x'
    };
    render(<ContextPanel {...base} step="run" run={run} />);

    expect(participants()[0]).toHaveTextContent('Falhou');
  });

  it('no resultado, o juiz por modelo aparece com o estado real: não executado', () => {
    render(<ContextPanel {...base} step="result" results={{ ...noResults, apply }} />);
    const judge = participants().find((li) => li.textContent?.includes('Juiz por modelo'));

    expect(judge).toHaveTextContent('Não executado');
    expect(participants()[0]).toHaveTextContent('Concluído');
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    const { container } = render(<ContextPanel {...base} step="result" results={{ ...noResults, apply }} />);

    await expectNoA11yViolations(container);
  });
});
