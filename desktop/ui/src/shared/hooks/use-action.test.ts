import { act, renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EngineError } from '@/shared/api';

import { useAction } from './use-action';

describe('useAction', () => {
  it('fica ocupada durante a ação, avisa quem recarrega e tira a mensagem do resultado', async () => {
    const onDone = vi.fn();
    let finish: (value: { ok: boolean }) => void = () => undefined;
    const { result } = renderHook(() => useAction(onDone));

    let outcome: Promise<boolean> = Promise.resolve(false);
    act(() => {
      outcome = result.current.run(() => new Promise<{ ok: boolean }>((resolve) => (finish = resolve)), 'Falhou.', {
        success: (value) => (value.ok ? 'Conectado.' : null),
        target: 'c-1'
      });
    });
    expect(result.current.busy).toBe(true);

    await act(async () => {
      finish({ ok: true });
      await outcome;
    });

    await expect(outcome).resolves.toBe(true);
    expect(result.current).toMatchObject({ busy: false, error: null, notice: { text: 'Conectado.', target: 'c-1' } });
    expect(onDone).toHaveBeenCalledTimes(1);
  });

  it('sucesso que não pede mensagem não deixa a do anterior na tela', async () => {
    const { result } = renderHook(() => useAction(() => undefined));

    await act(() => result.current.run(async () => 1, 'Falhou.', { success: () => 'Feito.' }));
    await act(() => result.current.run(async () => 2, 'Falhou.'));

    expect(result.current.notice).toBeNull();
  });

  it('a falha mostra a mensagem do motor ou a de quem chamou, sem recarregar', async () => {
    const onDone = vi.fn();
    const { result } = renderHook(() => useAction(onDone));

    let ok = true;
    await act(async () => {
      ok = await result.current.run(async () => {
        throw new EngineError('CLOUD_DENIED', 'A conta recusou.');
      }, 'Falhou.');
    });
    expect(ok).toBe(false);
    expect(result.current.error).toBe('A conta recusou.');

    await act(() =>
      result.current.run(async () => {
        throw new Error('interno');
      }, 'Não foi possível testar.')
    );
    expect(result.current).toMatchObject({ busy: false, error: 'Não foi possível testar.', notice: null });
    expect(onDone).not.toHaveBeenCalled();
  });
});
