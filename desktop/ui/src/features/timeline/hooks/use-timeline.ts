import type { EngineClient } from '@/shared/api';
import { usePagedRead } from '@/shared/hooks';
import type { ProjectTimelineEvent, ProjectTimelineSource } from '@/shared/schema';
import { useCallback, useMemo } from 'react';

import { readTimeline } from '../service/timeline-api';

export type TimelineState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | {
      status: 'ready';
      events: ProjectTimelineEvent[];
      sources: ProjectTimelineSource[];
      next: string | null;
      loadingMore: boolean;
      moreError: string | null;
    };

const MESSAGES = {
  failure: 'Não foi possível ler a linha do tempo.',
  more: 'Não foi possível carregar os acontecimentos mais antigos.'
};

const idOf = (event: ProjectTimelineEvent) => event.id;

export function useTimeline(client: EngineClient, projectId: string, kinds: string[]) {
  const kindsKey = kinds.join(',');
  const read = useCallback(
    (before: string | null) =>
      readTimeline(client, projectId, kindsKey === '' ? [] : kindsKey.split(','), before).then((page) => ({
        items: page.events,
        next: page.nextBefore,
        extra: page.sources
      })),
    [client, projectId, kindsKey]
  );
  const { state, loadMore, reload } = usePagedRead(read, idOf, MESSAGES);

  const timeline = useMemo<TimelineState>(
    () =>
      state.status === 'ready'
        ? {
            status: 'ready',
            events: state.items,
            sources: state.extra,
            next: state.next,
            loadingMore: state.loadingMore,
            moreError: state.moreError
          }
        : state,
    [state]
  );

  return { state: timeline, loadMore, reload };
}
