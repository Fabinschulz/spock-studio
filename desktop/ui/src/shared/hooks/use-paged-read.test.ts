import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { usePagedRead, type PagedPage } from './use-paged-read';

const messages = { failure: 'Falhou.', more: 'Falhou ao carregar mais.' };
const idOf = (item: { id: string }) => item.id;

type Item = { id: string };
type Deferred = { resolve: (page: PagedPage<Item, number>) => void; reject: (error: Error) => void };

/** Uma leitura cuja resposta o teste entrega quando quiser, na ordem em que foi pedida. */
function controlled() {
  const pending: Deferred[] = [];
  const read = () =>
    new Promise<PagedPage<Item, number>>((resolve, reject) => {
      pending.push({ resolve, reject });
    });
  return { read, pending };
}

describe('usePagedRead', () => {
  it('lê a primeira página, depois a seguinte pelo corte, sem repetir item', async () => {
    const cuts: Array<string | null> = [];
    const read = async (before: string | null) => {
      cuts.push(before);
      return before === null
        ? { items: [{ id: 'a' }, { id: 'b' }], next: 'b', extra: 1 }
        : { items: [{ id: 'b' }, { id: 'c' }], next: null, extra: 2 };
    };
    const { result } = renderHook(() => usePagedRead(read, idOf, messages));

    await waitFor(() => expect(result.current.state.status).toBe('ready'));
    act(() => result.current.loadMore());
    await waitFor(() => {
      const state = result.current.state;
      expect(state.status === 'ready' && state.items.map(idOf)).toEqual(['a', 'b', 'c']);
    });

    expect(cuts).toEqual([null, 'b']);
    const state = result.current.state;
    expect(state.status === 'ready' && state.next).toBeNull();
    // O resumo do conjunto vem da página mais nova.
    expect(state.status === 'ready' && state.extra).toBe(2);
  });

  it('uma página que chega depois de a leitura mudar não aparece sob a leitura nova', async () => {
    const first = controlled();
    const second = controlled();
    const { result, rerender } = renderHook(({ read }) => usePagedRead(read, idOf, messages), {
      initialProps: { read: first.read }
    });

    rerender({ read: second.read });
    expect(result.current.state.status).toBe('loading');

    await act(async () => {
      first.pending[0]!.resolve({ items: [{ id: 'velha' }], next: null, extra: 1 });
    });
    expect(result.current.state.status).toBe('loading');

    await act(async () => {
      second.pending[0]!.resolve({ items: [{ id: 'nova' }], next: null, extra: 2 });
    });
    const state = result.current.state;
    expect(state.status === 'ready' && state.items.map(idOf)).toEqual(['nova']);
  });

  it('se carregar mais falha, mantém a lista e diz o motivo; falha da primeira leitura vira erro', async () => {
    let calls = 0;
    const read = async () => {
      calls += 1;
      if (calls === 2) throw new Error('rede');
      return { items: [{ id: 'a' }], next: 'a', extra: 1 };
    };
    const { result } = renderHook(() => usePagedRead(read, idOf, messages));
    await waitFor(() => expect(result.current.state.status).toBe('ready'));

    act(() => result.current.loadMore());
    await waitFor(() => {
      const state = result.current.state;
      expect(state.status === 'ready' && state.moreError).not.toBeNull();
    });
    const state = result.current.state;
    expect(state.status === 'ready' && state.items.map(idOf)).toEqual(['a']);

    const failing = async (): Promise<PagedPage<Item, number>> => {
      throw new Error('nada');
    };
    const broken = renderHook(() => usePagedRead(failing, idOf, messages));
    await waitFor(() => expect(broken.result.current.state.status).toBe('error'));
  });

  it('ler de novo volta a lendo e traz a primeira página outra vez', async () => {
    let calls = 0;
    const read = async () => ({ items: [{ id: `r${(calls += 1)}` }], next: null, extra: 1 });
    const { result } = renderHook(() => usePagedRead(read, idOf, messages));
    await waitFor(() => expect(result.current.state.status).toBe('ready'));

    act(() => result.current.reload());

    await waitFor(() => {
      const state = result.current.state;
      expect(state.status === 'ready' && state.items.map(idOf)).toEqual(['r2']);
    });
  });
});
