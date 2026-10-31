import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { ExecutionPage, type WorkflowSnapshot } from './execution-page';

const paused: WorkflowSnapshot = {
  runId: 'run-1',
  projectId: 'p-aaaaaaaaaaaa',
  kind: 'workflow',
  state: 'awaiting_approval',
  events: [{ seq: 1, type: 'agent.completed', data: { agentId: 'architecture' } }],
  result: null
};

const done: WorkflowSnapshot = {
  ...paused,
  state: 'completed_with_limitations',
  result: {
    kind: 'workflow',
    summary: '1 etapa(s). Stacks detectadas: 1. Trechos: 1. Nenhuma alteração no projeto.',
    evidence: [{ filePath: 'docs/nota.md', excerpt: 'trecho visível', chunkId: 'docs/nota.md#1' }],
    limitations: [{ code: 'JUDGE_NOT_RUN', message: 'O juiz por modelo não é o veredito desta fase.' }],
    impact: { schema: 'engineering.impact.v1', areas: ['servico'], risks: [], evidence: ['docs/nota.md#1'] }
  }
};

function clientFor(first: WorkflowSnapshot, second?: WorkflowSnapshot): EngineClient & { calls: string[] } {
  const calls: string[] = [];
  let reads = 0;
  return {
    calls,
    async request(method, _params, schema) {
      calls.push(method);
      if (method === 'run.snapshot') {
        reads += 1;
        return schema.parse(reads === 1 ? first : (second ?? first));
      }
      if (method === 'workflow.resume') return schema.parse({ runId: first.runId });
      throw new Error(method);
    },
    async onEvent() {
      return () => undefined;
    }
  };
}

describe('Execução', () => {
  it('mostra o agente que de fato concluiu e pede aprovação', async () => {
    const client = clientFor(paused);
    const { container } = render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={client} />
      </AppProviders>
    );

    expect(await screen.findByRole('heading', { name: 'Execução' })).toBeInTheDocument();
    expect(screen.getByText('Aguardando aprovação')).toBeInTheDocument();
    expect(screen.getByText('Arquitetura concluiu')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Aprovar' })).toBeEnabled();
    await expectNoA11yViolations(container);
  });

  it('aprova e então mostra evidência, impacto e limitação reais', async () => {
    const client = clientFor(paused, done);
    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={client} />
      </AppProviders>
    );
    await userEvent.click(await screen.findByRole('button', { name: 'Aprovar' }));

    expect(await screen.findByText('Concluído com limitações')).toBeInTheDocument();
    expect(screen.getByText('docs/nota.md')).toBeInTheDocument();
    expect(screen.getByText('trecho visível')).toBeInTheDocument();
    expect(screen.getByText('Serviço')).toBeInTheDocument();
    expect(screen.getByText('O juiz por modelo não é o veredito desta fase.')).toBeInTheDocument();
    expect(client.calls).toEqual(['run.snapshot', 'workflow.resume', 'run.snapshot']);
    expect(screen.queryByRole('button', { name: 'Aprovar' })).not.toBeInTheDocument();
  });

  it('não inventa evidência quando o resultado não traz nenhuma', async () => {
    const empty = clientFor({
      ...done,
      events: [],
      result: { ...done.result!, evidence: [], impact: undefined }
    });
    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={empty} />
      </AppProviders>
    );

    expect(await screen.findByText('Nenhuma evidência nesta execução.')).toBeInTheDocument();
    expect(screen.queryByText('Arquitetura concluiu')).not.toBeInTheDocument();
  });
});
