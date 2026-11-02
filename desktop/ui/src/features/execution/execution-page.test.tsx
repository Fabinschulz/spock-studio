import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import type { EngineClient } from '@/shared/api';
import type { RunEvent } from '@/shared/schema';
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

  it('acompanha os eventos ao vivo e recupera a lacuna pelo sinceSeq', async () => {
    const running: WorkflowSnapshot = {
      runId: 'run-1',
      projectId: 'p-aaaaaaaaaaaa',
      kind: 'workflow',
      state: 'running',
      lastSeq: 0,
      events: [{ seq: 0, type: 'workflow.started', data: {} }],
      result: null
    };
    const snapshots: Array<Record<string, unknown>> = [];
    let emit: (event: RunEvent) => void = () => undefined;
    const client: EngineClient = {
      async request(method, params, schema) {
        if (method !== 'run.snapshot') throw new Error(method);
        snapshots.push(params);
        if (params.sinceSeq === undefined) return schema.parse(running);
        if (params.sinceSeq === 1) {
          return schema.parse({
            ...running,
            lastSeq: 3,
            events: [
              { seq: 2, type: 'context.retrieved', data: { filePath: 'docs/nota.md' } },
              { seq: 3, type: 'agent.completed', data: { agentId: 'architecture' } }
            ]
          });
        }
        return schema.parse({ ...done, lastSeq: 5, events: [] });
      },
      async onEvent(handler) {
        emit = handler;
        return () => undefined;
      }
    };
    const event = (seq: number, type: string, data: Record<string, unknown> = {}): RunEvent => ({
      v: 1,
      kind: 'event',
      runId: 'run-1',
      seq,
      type,
      data
    });

    render(
      <AppProviders>
        <ExecutionPage runId="run-1" client={client} />
      </AppProviders>
    );
    expect(await screen.findByText('Execução iniciada')).toBeInTheDocument();

    act(() => emit(event(1, 'agent.started', { agentId: 'architecture' })));
    act(() => emit(event(1, 'agent.started', { agentId: 'architecture' })));
    expect(screen.getAllByText('Arquitetura começou')).toHaveLength(1);

    act(() => emit(event(3, 'agent.completed', { agentId: 'architecture' })));
    expect(await screen.findByText('Trecho recuperado de docs/nota.md')).toBeInTheDocument();
    expect(screen.getAllByText('Arquitetura concluiu')).toHaveLength(1);
    expect(snapshots[1]).toEqual({ runId: 'run-1', sinceSeq: 1 });

    act(() => emit(event(4, 'run.completed', { state: 'completed_with_limitations' })));
    expect(await screen.findByText('trecho visível')).toBeInTheDocument();
    expect(screen.getByText('Concluído com limitações')).toBeInTheDocument();
    expect(screen.getByText('O que aconteceu').closest('section')?.querySelectorAll('li')).toHaveLength(4);
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
