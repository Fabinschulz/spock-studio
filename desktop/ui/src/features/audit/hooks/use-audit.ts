import type { EngineClient } from '@/shared/api';
import { usePagedRead } from '@/shared/hooks';
import type { ProjectAuditCategory, ProjectAuditEntry, ProjectAuditSource } from '@/shared/schema';
import { useCallback, useMemo } from 'react';

import { readAudit } from '../service/audit-api';

export type AuditState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | {
      status: 'ready';
      entries: ProjectAuditEntry[];
      categories: ProjectAuditCategory[];
      sources: ProjectAuditSource[];
      next: string | null;
      loadingMore: boolean;
      moreError: string | null;
    };

const MESSAGES = {
  failure: 'Não foi possível ler a trilha de auditoria.',
  more: 'Não foi possível carregar as entradas mais antigas.'
};

const idOf = (entry: ProjectAuditEntry) => entry.id;

/** A trilha de auditoria com "Ver mais antigas": a leitura por páginas é a de `usePagedRead`; aqui só estão o projeto e as categorias escolhidas. */
export function useAudit(client: EngineClient, projectId: string, categories: string[]) {
  const key = categories.join(',');
  const read = useCallback(
    (before: string | null) =>
      readAudit(client, projectId, key === '' ? [] : key.split(','), before).then((page) => ({
        items: page.entries,
        next: page.nextBefore,
        extra: { categories: page.categories, sources: page.sources }
      })),
    [client, projectId, key]
  );
  const { state, loadMore, reload } = usePagedRead(read, idOf, MESSAGES);

  const audit = useMemo<AuditState>(
    () =>
      state.status === 'ready'
        ? {
            status: 'ready',
            entries: state.items,
            categories: state.extra.categories,
            sources: state.extra.sources,
            next: state.next,
            loadingMore: state.loadingMore,
            moreError: state.moreError
          }
        : state,
    [state]
  );

  return { state: audit, loadMore, reload };
}
