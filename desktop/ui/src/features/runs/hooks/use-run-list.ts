import { errorMessage, type EngineClient } from '@/shared/api';
import type { RunItem, RunList } from '@/shared/schema';
import { useCallback, useEffect, useMemo, useState } from 'react';

import { listRuns, type RunFilters } from '../service/runs-api';

export type RunListState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | {
      status: 'ready';
      runs: RunItem[];
      summary: RunList['summary'];
      nextCursor: string | null;
      loadingMore: boolean;
      moreError: string | null;
    };

type Ready = Extract<RunListState, { status: 'ready' }>;

const LOADING: RunListState = { status: 'loading' };

/**
 * Cada leitura é guardada com a chave dos filtros que a pediu: uma página que chega depois da troca de filtro não é
 * gravada, para nunca aparecer sob um filtro novo.
 */
export function useRunList(client: EngineClient, filters: RunFilters) {
  const { projectId, kind, state } = filters;
  const [version, setVersion] = useState(0);
  const [result, setResult] = useState<{ key: string; value: RunListState } | null>(null);
  const key = `${projectId ?? ''}|${kind ?? ''}|${state ?? ''}|${version}`;

  useEffect(() => {
    let active = true;
    listRuns(client, { projectId, kind, state }, null).then(
      (page) => {
        if (!active) return;
        setResult({
          key,
          value: {
            status: 'ready',
            runs: page.runs,
            summary: page.summary,
            nextCursor: page.nextCursor,
            loadingMore: false,
            moreError: null
          }
        });
      },
      (error: unknown) => {
        if (!active) return;
        setResult({
          key,
          value: { status: 'error', message: errorMessage(error, 'Não foi possível ler as execuções.') }
        });
      }
    );
    return () => {
      active = false;
    };
  }, [client, projectId, kind, state, key]);

  const value = useMemo<RunListState>(() => (result?.key === key ? result.value : LOADING), [result, key]);

  const loadMore = useCallback(() => {
    if (value.status !== 'ready' || value.nextCursor === null || value.loadingMore) return;
    const { nextCursor } = value;
    // Só a leitura que ainda é a atual muda: se o filtro trocou no meio do caminho, a página velha é descartada.
    const update = (change: (current: Ready) => Ready) =>
      setResult((previous) =>
        previous?.key === key && previous.value.status === 'ready' ? { key, value: change(previous.value) } : previous
      );

    update((current) => ({ ...current, loadingMore: true, moreError: null }));
    listRuns(client, { projectId, kind, state }, nextCursor).then(
      (page) =>
        update((current) => ({
          status: 'ready',
          runs: [...current.runs, ...page.runs],
          summary: page.summary,
          nextCursor: page.nextCursor,
          loadingMore: false,
          moreError: null
        })),
      (error: unknown) =>
        update((current) => ({
          ...current,
          loadingMore: false,
          moreError: errorMessage(error, 'Não foi possível carregar mais execuções.')
        }))
    );
  }, [client, projectId, kind, state, key, value]);

  const reload = useCallback(() => setVersion((n) => n + 1), []);

  return { state: value, loadMore, reload };
}
