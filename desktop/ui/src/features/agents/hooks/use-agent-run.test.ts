import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { RunEvent, TimelineEvent } from '@/shared/schema';

import type { AgentApi, AgentSnapshot } from '../service/agent-api';
import { useAgentRun } from './use-agent-run';

/** Guarda os eventos como o motor e deixa o teste perder alguns no caminho até a tela. */
function fakeAgentApi() {
  const events: TimelineEvent[] = [];
  let listener: ((event: RunEvent) => void) | null = null;
  let state = 'running';
  const result = {
    kind: 'agent' as const,
    tool: 'claude-code',
    toolName: 'Claude Code',
    exitCode: 0,
    error: null,
    answer: 'pronto'
  };
  const reads: (number | undefined)[] = [];

  const api = {
    tools: vi.fn(),
    ideConnection: vi.fn(),
    cancel: vi.fn(async () => undefined),
    start: vi.fn(async () => {
      publish('agent.output', { line: 'antes do id' });
      return 'run-1';
    }),
    onEvent: vi.fn(async (handler: (event: RunEvent) => void) => {
      listener = handler;
      return () => {
        listener = null;
      };
    }),
    snapshot: vi.fn(async (runId: string, sinceSeq?: number): Promise<AgentSnapshot> => {
      reads.push(sinceSeq);
      return {
        runId,
        projectId: 'p-1',
        kind: 'agent',
        state,
        lastSeq: events.length - 1,
        events: events.filter((event) => sinceSeq === undefined || event.seq > sinceSeq),
        result: state === 'completed' ? result : null
      };
    })
  } satisfies AgentApi;

  function publish(type: string, data: Record<string, unknown>, deliver = true) {
    const event = { seq: events.length, type, data };
    events.push(event);
    if (deliver) listener?.({ v: 1, kind: 'event', runId: 'run-1', ...event });
  }

  return {
    api,
    reads,
    line: (text: string, deliver = true) => act(() => publish('agent.output', { line: text }, deliver)),
    finish: () =>
      act(() => {
        state = 'completed';
        publish('run.completed', { state: 'completed' });
      })
  };
}

describe('useAgentRun', () => {
  it('linha perdida no caminho volta pela lacuna de numeração, sem repetir a que saiu antes do id', async () => {
    const engine = fakeAgentApi();
    const { result } = renderHook(() => useAgentRun(engine.api));

    act(() => {
      void result.current.start('p-1', 'claude-code', 'O que este projeto faz?');
    });
    await waitFor(() => expect(engine.reads).toEqual([undefined]));
    engine.line('um');
    engine.line('dois', false);
    engine.line('três');

    await waitFor(() =>
      expect(result.current.run).toMatchObject({ status: 'running', lines: ['antes do id', 'um', 'dois', 'três'] })
    );
    expect(engine.reads).toEqual([undefined, 1]);

    engine.finish();
    await waitFor(() => expect(result.current.run.status).toBe('finished'));
    expect(result.current.run).toMatchObject({ lines: ['antes do id', 'um', 'dois', 'três'], state: 'completed' });
  });
});
