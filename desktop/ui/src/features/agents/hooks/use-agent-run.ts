import { errorMessage } from '@/shared/api';
import { agentOutputSchema, isTerminalState, type AgentRunResult, type RunEvent } from '@/shared/schema';
import { useCallback, useEffect, useRef, useState } from 'react';

import type { AgentApi, AgentSnapshot } from '../service/agent-api';

const MAX_LINES = 2000;

export type AgentRun =
  | { status: 'idle' }
  | { status: 'starting' }
  | { status: 'running'; runId: string; lines: string[] }
  | { status: 'finished'; runId: string; lines: string[]; state: string; result: AgentRunResult | null }
  | { status: 'failed'; message: string };

const outputOf = (events: readonly { type: string; data: Record<string, unknown> }[]) =>
  events.flatMap((event) => {
    if (event.type !== 'agent.output') return [];
    const parsed = agentOutputSchema.safeParse(event.data);
    return parsed.success ? [parsed.data.line] : [];
  });

/**
 * Escuta antes de pedir: as primeiras linhas podem sair antes de o pedido voltar com o id. O retrato traz o que já saiu;
 * o que chega depois entra pela numeração, sem repetir nem perder linha.
 */
export function useAgentRun(api: AgentApi) {
  const [run, setRun] = useState<AgentRun>({ status: 'idle' });
  const stop = useRef<(() => void) | null>(null);

  useEffect(() => () => stop.current?.(), []);

  // O retrato guarda só os últimos eventos do motor: as linhas já recebidas ao vivo valem mais que as dele.
  const finish = useCallback((snapshot: AgentSnapshot, lines = outputOf(snapshot.events)) => {
    setRun({
      status: 'finished',
      runId: snapshot.runId,
      lines: lines.slice(-MAX_LINES),
      state: snapshot.state,
      result: snapshot.result
    });
  }, []);

  const start = useCallback(
    async (projectId: string, tool: string, prompt: string) => {
      stop.current?.();
      setRun({ status: 'starting' });
      const early: RunEvent[] = [];
      let follow: ((event: RunEvent) => void) | null = null;
      stop.current = await api.onEvent((event) => (follow ? follow(event) : early.push(event)));

      try {
        const runId = await api.start(projectId, tool, prompt);
        const snapshot = await api.snapshot(runId);
        if (isTerminalState(snapshot.state)) {
          finish(snapshot);
          return;
        }
        let lastSeq = snapshot.lastSeq;
        let lines = outputOf(snapshot.events);
        setRun({ status: 'running', runId, lines });

        follow = (event) => {
          if (event.runId !== runId || event.seq <= lastSeq) return;
          lastSeq = event.seq;
          if (event.type === 'run.completed') {
            void api.snapshot(runId).then(
              (final) => finish(final, lines),
              () => setRun({ status: 'failed', message: 'A resposta terminou, mas não foi possível lê-la.' })
            );
            return;
          }
          const added = outputOf([event]);
          if (added.length === 0) return;
          lines = [...lines, ...added].slice(-MAX_LINES);
          setRun({ status: 'running', runId, lines });
        };
        early.forEach((event) => follow?.(event));
      } catch (error) {
        stop.current?.();
        stop.current = null;
        setRun({ status: 'failed', message: errorMessage(error, 'Não foi possível pedir ao agente.') });
      }
    },
    [api, finish]
  );

  const cancel = useCallback(async () => {
    if (run.status === 'running') await api.cancel(run.runId).catch(() => undefined);
  }, [api, run]);

  return { run, start, cancel };
}
