import type { EngineClient } from '@/shared/api';
import type { RunEvent } from '@/shared/schema';
import { useCallback, useEffect, useRef, useState } from 'react';

import { applyLive, fromSnapshot, isTerminal, workflowSnapshotSchema, type RunView } from '../domain/run-timeline';

/**
 * Acompanha uma execução pelos eventos reais do motor. Escuta antes de ler o retrato, para não perder o que acontecer no
 * meio; eventos que chegam antes do retrato esperam e passam pela mesma dobra. Lacuna de numeração vira `sinceSeq`.
 */
export function useRunFollow(client: EngineClient, runId: string) {
  const [view, setView] = useState<RunView | null>(null);
  const [error, setError] = useState<string | null>(null);
  const viewRef = useRef<RunView | null>(null);
  const pending = useRef<Parameters<typeof applyLive>[1][]>([]);
  const catchingUp = useRef(false);

  const commit = useCallback((next: RunView) => {
    viewRef.current = next;
    setView(next);
  }, []);

  const refresh = useCallback(async () => {
    const current = viewRef.current;
    const params = current ? { runId, sinceSeq: current.lastSeq } : { runId };
    const snapshot = await client.request('run.snapshot', params, workflowSnapshotSchema);
    let next = fromSnapshot(snapshot, current ?? undefined);
    for (const event of pending.current) next = applyLive(next, event).view;
    pending.current = [];
    commit(next);
  }, [client, commit, runId]);

  const catchUp = useCallback(() => {
    if (catchingUp.current) return;
    catchingUp.current = true;
    refresh()
      .catch(() => setError('Alguns eventos não chegaram. Recarregue a execução.'))
      .finally(() => {
        catchingUp.current = false;
      });
  }, [refresh]);

  useEffect(() => {
    let active = true;
    let unlisten: (() => void) | undefined;
    viewRef.current = null;
    pending.current = [];

    const onEvent = (event: RunEvent) => {
      if (!active || event.runId !== runId) return;
      const item = { seq: event.seq, type: event.type, data: event.data };
      const current = viewRef.current;
      if (!current || catchingUp.current) {
        pending.current.push(item);
        return;
      }
      const outcome = applyLive(current, item);
      if (outcome.gap) {
        pending.current.push(item);
        catchUp();
        return;
      }
      if (outcome.view !== current) commit(outcome.view);
      // O resultado só vem no retrato: ao terminar, a página busca o que falta.
      if (item.type === 'run.completed' || (item.type === 'run.state' && isTerminal(outcome.view.snapshot.state)))
        catchUp();
    };

    // A escuta precisa estar ativa antes do primeiro retrato: um evento entre os dois (até o de término) não se perde.
    const follow = async () => {
      try {
        const stop = await client.onEvent(onEvent);
        if (!active) {
          stop();
          return;
        }
        unlisten = stop;
      } catch {
        if (active) setError('Não foi possível acompanhar os eventos desta execução.');
      }
      try {
        await refresh();
      } catch {
        if (active) setError('Não foi possível ler esta execução.');
      }
    };
    void follow();

    return () => {
      active = false;
      unlisten?.();
    };
  }, [catchUp, client, commit, refresh, runId]);

  return { view, error, setError, refresh };
}
