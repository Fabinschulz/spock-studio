import { engineRunSource, followRun, type EngineClient, type RunFollow } from '@/shared/api';
import { useCallback, useEffect, useRef, useState } from 'react';

import {
  applyLive,
  fromSnapshot,
  workflowSnapshotSchema,
  type RunView,
  type WorkflowSnapshot
} from '../domain/run-timeline';

/**
 * Acompanha uma execução pelo `followRun`, sem parar no fim: depois de uma aprovação a mesma execução continua. A ordem,
 * a lacuna de numeração e a escuta antes do primeiro retrato ficam com ele; aqui só se dobra o que chega na `RunView`.
 */
export function useRunFollow(client: EngineClient, runId: string) {
  const [view, setView] = useState<RunView | null>(null);
  const [error, setError] = useState<string | null>(null);
  // Tentar de novo depois de uma leitura que falhou recomeça o acompanhamento inteiro.
  const [attempt, setAttempt] = useState(0);
  const viewRef = useRef<RunView | null>(null);
  const follow = useRef<RunFollow<WorkflowSnapshot> | null>(null);

  const commit = useCallback((next: RunView) => {
    viewRef.current = next;
    setView(next);
  }, []);

  useEffect(() => {
    viewRef.current = null;
    let active = true;
    const current = followRun(
      engineRunSource(client, workflowSnapshotSchema),
      runId,
      {
        snapshot: (snapshot) => {
          if (active) commit(fromSnapshot(snapshot, viewRef.current ?? undefined));
        },
        event: (event) => {
          const shown = viewRef.current;
          if (!active || !shown) return;
          const next = applyLive(shown, event).view;
          if (next !== shown) commit(next);
        },
        lost: () => {
          if (active) setError('Alguns eventos não chegaram. Tente de novo para reler a execução.');
        },
        deaf: () => {
          if (active) setError('Não foi possível acompanhar os eventos desta execução.');
        }
      },
      { untilDone: false }
    );
    follow.current = current;
    // Sem `untilDone`, `done` só termina com erro: a leitura do retrato falhou.
    current.done.catch(() => {
      if (active) setError('Não foi possível ler esta execução.');
    });

    return () => {
      active = false;
      current.stop();
    };
  }, [attempt, client, commit, runId]);

  const refresh = useCallback(async () => {
    await follow.current?.refresh();
  }, []);

  const retry = useCallback(() => {
    setError(null);
    setAttempt((value) => value + 1);
  }, []);

  return { view, error, setError, refresh, retry };
}
