import { errorMessage } from '@/shared/api';
import { useCallback, useEffect, useMemo, useState } from 'react';

/** Uma página lida: os itens, o corte da página seguinte (nulo no fim) e o que a leitura traz além da lista (contagens, por exemplo). */
export interface PagedPage<T, E> {
  items: T[];
  next: string | null;
  extra: E;
}

export type PagedState<T, E> =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; items: T[]; extra: E; next: string | null; loadingMore: boolean; moreError: string | null };

type Ready<T, E> = Extract<PagedState<T, E>, { status: 'ready' }>;

/**
 * Uma lista lida por páginas, com "ver mais". Cada leitura é guardada junto com a função `read` que a pediu: trocar `read` (o
 * projeto ou um filtro mudou) volta a "lendo", e uma página que chega depois da troca não é gravada, para nunca aparecer sob um
 * filtro novo. Quem chama estabiliza `read` com `useCallback`. Uma página nova junta ao que já havia sem repetir itens.
 */
export function usePagedRead<T, E>(
  read: (before: string | null) => Promise<PagedPage<T, E>>,
  idOf: (item: T) => string,
  messages: { failure: string; more: string }
) {
  const [version, setVersion] = useState(0);
  const [result, setResult] = useState<{ read: typeof read; version: number; value: PagedState<T, E> } | null>(null);
  const { failure, more } = messages;

  useEffect(() => {
    let active = true;
    read(null).then(
      (page) => {
        if (!active) return;
        setResult({
          read,
          version,
          value: {
            status: 'ready',
            items: page.items,
            extra: page.extra,
            next: page.next,
            loadingMore: false,
            moreError: null
          }
        });
      },
      (error: unknown) => {
        if (!active) return;
        setResult({ read, version, value: { status: 'error', message: errorMessage(error, failure) } });
      }
    );
    return () => {
      active = false;
    };
  }, [read, version, failure]);

  const state = useMemo<PagedState<T, E>>(
    () => (result?.read === read && result.version === version ? result.value : { status: 'loading' }),
    [result, read, version]
  );

  const loadMore = useCallback(() => {
    if (state.status !== 'ready' || state.next === null || state.loadingMore) return;
    const { next } = state;
    // Só a leitura que ainda é a atual muda: se o filtro trocou no meio do caminho, a página velha é descartada.
    const update = (change: (current: Ready<T, E>) => Ready<T, E>) =>
      setResult((previous) =>
        previous?.read === read && previous.version === version && previous.value.status === 'ready'
          ? { read, version, value: change(previous.value) }
          : previous
      );

    update((current) => ({ ...current, loadingMore: true, moreError: null }));
    read(next).then(
      (page) =>
        update((current) => {
          const seen = new Set(current.items.map(idOf));
          return {
            ...current,
            items: [...current.items, ...page.items.filter((item) => !seen.has(idOf(item)))],
            next: page.next,
            loadingMore: false,
            moreError: null
          };
        }),
      (error: unknown) =>
        update((current) => ({ ...current, loadingMore: false, moreError: errorMessage(error, more) }))
    );
  }, [read, version, state, idOf, more]);

  const reload = useCallback(() => setVersion((n) => n + 1), []);

  return { state, loadMore, reload };
}
