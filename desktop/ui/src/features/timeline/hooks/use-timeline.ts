import { errorMessage, type EngineClient } from '@/shared/api';
import type { ProjectTimeline, ProjectTimelineEvent, ProjectTimelineSource } from '@/shared/schema';
import { useCallback, useEffect, useMemo, useState } from 'react';

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

type Ready = Extract<TimelineState, { status: 'ready' }>;

const LOADING: TimelineState = { status: 'loading' };

/**
 * A linha do tempo com "Ver mais antigos". Cada leitura é guardada com a chave que a pediu (projeto e tipos escolhidos): trocar
 * um deles volta a "lendo", e uma página que chega depois da troca não é gravada, para nunca aparecer sob um filtro novo.
 */
export function useTimeline(client: EngineClient, projectId: string, kinds: string[]) {
  const [version, setVersion] = useState(0);
  const [result, setResult] = useState<{ key: string; value: TimelineState } | null>(null);
  const kindsKey = kinds.join(',');
  const key = `${projectId}|${kindsKey}|${version}`;

  useEffect(() => {
    let active = true;
    readTimeline(client, projectId, kindsKey === '' ? [] : kindsKey.split(','), null).then(
      (page) => {
        if (!active) return;
        setResult({ key, value: ready(page) });
      },
      (error: unknown) => {
        if (!active) return;
        setResult({
          key,
          value: { status: 'error', message: errorMessage(error, 'Não foi possível ler a linha do tempo.') }
        });
      }
    );
    return () => {
      active = false;
    };
  }, [client, projectId, kindsKey, key]);

  const value = useMemo<TimelineState>(() => (result?.key === key ? result.value : LOADING), [result, key]);

  const loadMore = useCallback(() => {
    if (value.status !== 'ready' || value.next === null || value.loadingMore) return;
    const { next } = value;
    // Só a leitura que ainda é a atual muda: se o filtro trocou no meio do caminho, a página velha é descartada.
    const update = (change: (current: Ready) => Ready) =>
      setResult((previous) =>
        previous?.key === key && previous.value.status === 'ready' ? { key, value: change(previous.value) } : previous
      );

    update((current) => ({ ...current, loadingMore: true, moreError: null }));
    readTimeline(client, projectId, kindsKey === '' ? [] : kindsKey.split(','), next).then(
      (page) =>
        update((current) => {
          const seen = new Set(current.events.map((event) => event.id));
          return {
            ...current,
            events: [...current.events, ...page.events.filter((event) => !seen.has(event.id))],
            next: page.nextBefore,
            loadingMore: false,
            moreError: null
          };
        }),
      (error: unknown) =>
        update((current) => ({
          ...current,
          loadingMore: false,
          moreError: errorMessage(error, 'Não foi possível carregar os acontecimentos mais antigos.')
        }))
    );
  }, [client, projectId, kindsKey, key, value]);

  const reload = useCallback(() => setVersion((n) => n + 1), []);

  return { state: value, loadMore, reload };
}

function ready(page: ProjectTimeline): Ready {
  return {
    status: 'ready',
    events: page.events,
    sources: page.sources,
    next: page.nextBefore,
    loadingMore: false,
    moreError: null
  };
}
