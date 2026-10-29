import { useEffect, useState } from 'react';

import { isTauriRuntime } from '@/shared/api';

/** Estado real do motor .NET, como o host o informa (comando e evento `engine_status`). */
export type EngineStatus = 'unknown' | 'starting' | 'ready' | 'crashed';

const KNOWN: readonly string[] = ['starting', 'ready', 'crashed'];

export const parseEngineStatus = (value: unknown): EngineStatus =>
  typeof value === 'string' && KNOWN.includes(value) ? (value as EngineStatus) : 'unknown';

/**
 * Acompanha o estado do motor. Fora do aplicativo desktop não há motor: o estado fica `unknown` e `hosted` é falso,
 * para a tela dizer isso em vez de fingir que o sistema está pronto.
 */
export function useEngineStatus(): { status: EngineStatus; hosted: boolean } {
  const hosted = isTauriRuntime();
  const [status, setStatus] = useState<EngineStatus>('unknown');

  useEffect(() => {
    if (!hosted) return;
    let active = true;
    let unlisten: (() => void) | undefined;

    void (async () => {
      const [{ invoke }, { listen }] = await Promise.all([
        import('@tauri-apps/api/core'),
        import('@tauri-apps/api/event')
      ]);
      unlisten = await listen<unknown>('engine_status', (event) => {
        if (active) setStatus(parseEngineStatus(event.payload));
      });
      try {
        const current = parseEngineStatus(await invoke('engine_status'));
        // Um evento que chegou antes da consulta é mais recente: não o sobrescreve.
        if (active) setStatus((previous) => (previous === 'unknown' ? current : previous));
      } catch {
        if (active) setStatus('unknown');
      }
    })().catch(() => undefined);

    return () => {
      active = false;
      unlisten?.();
    };
  }, [hosted]);

  return { status, hosted };
}
