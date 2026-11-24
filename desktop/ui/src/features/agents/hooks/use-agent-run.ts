import { EngineError, errorMessage, followRun } from '@/shared/api';
import { agentOutputSchema, type AgentRunResult, type TimelineEvent } from '@/shared/schema';
import { useCallback, useEffect, useRef, useState } from 'react';

import type { AgentApi } from '../service/agent-api';

const MAX_LINES = 2000;

export type AgentRun =
  | { status: 'idle' }
  | { status: 'starting' }
  | { status: 'running'; runId: string; lines: string[] }
  | { status: 'finished'; runId: string; lines: string[]; state: string; result: AgentRunResult | null }
  | { status: 'failed'; message: string };

const lineOf = (event: TimelineEvent): string | null => {
  if (event.type !== 'agent.output') return null;
  const parsed = agentOutputSchema.safeParse(event.data);
  return parsed.success ? parsed.data.line : null;
};

/**
 * As linhas vêm do `followRun`: as que saíram antes de o pedido voltar com o id, as do retrato e as que faltaram numa
 * lacuna de numeração entram em ordem e uma vez só.
 */
export function useAgentRun(api: AgentApi) {
  const [run, setRun] = useState<AgentRun>({ status: 'idle' });
  const stop = useRef<(() => void) | null>(null);

  useEffect(() => () => stop.current?.(), []);

  const start = useCallback(
    async (projectId: string, tool: string, prompt: string) => {
      stop.current?.();
      setRun({ status: 'starting' });
      let runId = '';
      let lines: string[] = [];
      const follow = followRun(api, () => api.start(projectId, tool, prompt), {
        started: (id) => {
          runId = id;
          setRun({ status: 'running', runId, lines });
        },
        event: (event) => {
          const line = lineOf(event);
          if (line === null) return;
          lines = [...lines, line].slice(-MAX_LINES);
          setRun({ status: 'running', runId, lines });
        },
        lost: () => {
          follow.stop();
          setRun({ status: 'failed', message: 'A resposta do agente não pôde ser lida.' });
        }
      });
      stop.current = follow.stop;

      try {
        const final = await follow.done;
        setRun({ status: 'finished', runId, lines, state: final.state, result: final.result });
      } catch (error) {
        follow.stop();
        if (!(error instanceof EngineError && error.code === 'ABORTED'))
          setRun({ status: 'failed', message: errorMessage(error, 'Não foi possível pedir ao agente.') });
      }
    },
    [api]
  );

  const cancel = useCallback(async () => {
    if (run.status === 'running') await api.cancel(run.runId).catch(() => undefined);
  }, [api, run]);

  return { run, start, cancel };
}
