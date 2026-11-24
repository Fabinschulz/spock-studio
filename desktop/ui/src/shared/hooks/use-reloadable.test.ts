import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { useReloadable } from './use-reloadable';

describe('useReloadable', () => {
  it('começa sem valor e sem resposta, e entrega o valor lido', async () => {
    const { result } = renderHook(() => useReloadable(async () => 'ok'));

    expect(result.current).toMatchObject({ value: null, error: null, settled: false });
    await waitFor(() => expect(result.current).toMatchObject({ value: 'ok', error: null, settled: true }));
  });

  it('a releitura deixa o valor anterior na tela até o novo chegar', async () => {
    let release: (value: string) => void = () => undefined;
    const load = vi
      .fn<() => Promise<string>>()
      .mockResolvedValueOnce('primeiro')
      .mockImplementationOnce(() => new Promise<string>((resolve) => (release = resolve)));
    const { result } = renderHook(() => useReloadable(load));
    await waitFor(() => expect(result.current.value).toBe('primeiro'));

    act(() => result.current.reload());
    expect(result.current.value).toBe('primeiro');

    act(() => release('segundo'));
    await waitFor(() => expect(result.current.value).toBe('segundo'));
  });

  it('uma releitura que falha mostra o erro e mantém o último valor bom', async () => {
    const failure = new Error('caiu');
    const load = vi.fn<() => Promise<string>>().mockResolvedValueOnce('bom').mockRejectedValueOnce(failure);
    const { result } = renderHook(() => useReloadable(load));
    await waitFor(() => expect(result.current.value).toBe('bom'));

    act(() => result.current.reload());

    await waitFor(() => expect(result.current.error).toBe(failure));
    expect(result.current).toMatchObject({ value: 'bom', settled: true });
  });

  it('a primeira leitura que falha deixa o valor vazio e a resposta dada', async () => {
    const failure = new Error('caiu');
    const { result } = renderHook(() => useReloadable(() => Promise.reject(failure)));

    await waitFor(() => expect(result.current).toMatchObject({ value: null, error: failure, settled: true }));
  });
});
