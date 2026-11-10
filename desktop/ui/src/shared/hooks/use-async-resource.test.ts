import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { useAsyncResource } from './use-async-resource';

describe('useAsyncResource', () => {
  it('começa carregando e entrega o valor lido', async () => {
    const load = () => Promise.resolve('ok');
    const { result } = renderHook(() => useAsyncResource(load));

    expect(result.current).toEqual({ status: 'loading' });
    await waitFor(() => expect(result.current).toEqual({ status: 'ready', value: 'ok' }));
  });

  it('entrega a falha sem derrubar quem chamou', async () => {
    const failure = new Error('caiu');
    const load = () => Promise.reject(failure);
    const { result } = renderHook(() => useAsyncResource(load));

    await waitFor(() => expect(result.current).toEqual({ status: 'error', error: failure }));
  });

  it('trocar a leitura volta a carregar e descarta a que ficou para trás', async () => {
    let releaseSlow: (value: string) => void = () => undefined;
    const slow = () => new Promise<string>((resolve) => (releaseSlow = resolve));
    const fast = () => Promise.resolve('novo');
    const { result, rerender } = renderHook(({ load }) => useAsyncResource(load), { initialProps: { load: slow } });

    rerender({ load: fast });
    await waitFor(() => expect(result.current).toEqual({ status: 'ready', value: 'novo' }));

    releaseSlow('velho');
    await Promise.resolve();
    expect(result.current).toEqual({ status: 'ready', value: 'novo' });
  });
});
