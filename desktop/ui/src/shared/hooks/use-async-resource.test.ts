import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { useAsyncResource } from './use-async-resource';

describe('useAsyncResource', () => {
  it('começa carregando e entrega o valor lido', async () => {
    const load = () => Promise.resolve('ok');
    const { result } = renderHook(() => useAsyncResource(load));

    expect(result.current).toMatchObject({ status: 'loading' });
    await waitFor(() => expect(result.current).toMatchObject({ status: 'ready', value: 'ok' }));
  });

  it('entrega a falha sem derrubar quem chamou', async () => {
    const failure = new Error('caiu');
    const load = () => Promise.reject(failure);
    const { result } = renderHook(() => useAsyncResource(load));

    await waitFor(() => expect(result.current).toMatchObject({ status: 'error', error: failure }));
  });

  it('refazer a leitura depois de uma falha volta a carregar e entrega o valor novo', async () => {
    const load = vi.fn<() => Promise<string>>().mockRejectedValueOnce(new Error('caiu')).mockResolvedValue('de novo');
    const { result } = renderHook(() => useAsyncResource(load));
    await waitFor(() => expect(result.current.status).toBe('error'));

    act(() => result.current.reload?.());

    expect(result.current).toMatchObject({ status: 'loading' });
    await waitFor(() => expect(result.current).toMatchObject({ status: 'ready', value: 'de novo' }));
    expect(load).toHaveBeenCalledTimes(2);
  });

  it('o resultado só muda de identidade quando a leitura muda (quem o usa em dependências não entra em laço)', async () => {
    const load = () => Promise.resolve('ok');
    const { result, rerender } = renderHook(() => useAsyncResource(load));
    await waitFor(() => expect(result.current.status).toBe('ready'));
    const settled = result.current;

    rerender();

    expect(result.current).toBe(settled);
  });

  it('trocar a leitura volta a carregar e descarta a que ficou para trás', async () => {
    let releaseSlow: (value: string) => void = () => undefined;
    const slow = () => new Promise<string>((resolve) => (releaseSlow = resolve));
    const fast = () => Promise.resolve('novo');
    const { result, rerender } = renderHook(({ load }) => useAsyncResource(load), { initialProps: { load: slow } });

    rerender({ load: fast });
    await waitFor(() => expect(result.current).toMatchObject({ status: 'ready', value: 'novo' }));

    releaseSlow('velho');
    await Promise.resolve();
    expect(result.current).toMatchObject({ status: 'ready', value: 'novo' });
  });
});
