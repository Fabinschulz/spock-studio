import type { EngineClient } from '@/shared/api';
import { usePagedRead } from '@/shared/hooks';
import type { RunItem, RunList } from '@/shared/schema';
import { useCallback, useMemo } from 'react';

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

const MESSAGES = {
  failure: 'Não foi possível ler as execuções.',
  more: 'Não foi possível carregar mais execuções.'
};

const idOf = (run: RunItem) => run.runId;

/**
 * A paginação e o descarte da página que chega depois da troca de filtro são do `usePagedRead`; aqui só os nomes que a
 * tela de execuções usa.
 */
export function useRunList(client: EngineClient, filters: RunFilters) {
  const { projectId, kind, state } = filters;
  const read = useCallback(
    async (before: string | null) => {
      const page = await listRuns(client, { projectId, kind, state }, before);
      return { items: page.runs, next: page.nextCursor, extra: page.summary };
    },
    [client, projectId, kind, state]
  );
  const paged = usePagedRead(read, idOf, MESSAGES);

  const value = useMemo<RunListState>(() => {
    const current = paged.state;
    if (current.status !== 'ready') return current;
    return {
      status: 'ready',
      runs: current.items,
      summary: current.extra,
      nextCursor: current.next,
      loadingMore: current.loadingMore,
      moreError: current.moreError
    };
  }, [paged.state]);

  return { state: value, loadMore: paged.loadMore, reload: paged.reload };
}
