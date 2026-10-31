import { isTauriRuntime } from '@/shared/api';
import { useEffect, useState } from 'react';

/** Estado real do motor .NET, como o host o informa (comando e evento `engine_status`). */
export type EngineStatus = 'unknown' | 'starting' | 'ready' | 'crashed';

/** Runtime de IA, separado do motor. Indisponível não significa que o motor caiu. */
export type AiRuntimeStatus = 'unknown' | 'ready' | 'stopped' | 'unavailable';

const KNOWN: readonly string[] = ['starting', 'ready', 'crashed'];
const AI_KNOWN: readonly string[] = ['unknown', 'ready', 'stopped', 'unavailable'];

export const parseEngineStatus = (value: unknown): EngineStatus =>
  typeof value === 'string' && KNOWN.includes(value) ? (value as EngineStatus) : 'unknown';

export const parseAiRuntimeStatus = (value: unknown): AiRuntimeStatus =>
  typeof value === 'string' && AI_KNOWN.includes(value) ? (value as AiRuntimeStatus) : 'unknown';

/** Aceita o texto antigo do host e o relatório `{ engine, aiRuntime }`. */
export const parseEngineReport = (value: unknown): { status: EngineStatus; aiRuntime: AiRuntimeStatus } => {
  if (typeof value === 'string') return { status: parseEngineStatus(value), aiRuntime: 'unknown' };
  if (typeof value === 'object' && value !== null) {
    const record = value as Record<string, unknown>;
    return { status: parseEngineStatus(record.engine), aiRuntime: parseAiRuntimeStatus(record.aiRuntime) };
  }
  return { status: 'unknown', aiRuntime: 'unknown' };
};

/**
 * Acompanha o estado do motor. Fora do aplicativo desktop não há motor: o estado fica `unknown` e `hosted` é falso,
 * para a tela dizer isso em vez de fingir que o sistema está pronto.
 */
export function useEngineStatus(): { status: EngineStatus; aiRuntime: AiRuntimeStatus; hosted: boolean } {
  const hosted = isTauriRuntime();
  const [status, setStatus] = useState<EngineStatus>('unknown');
  const [aiRuntime, setAiRuntime] = useState<AiRuntimeStatus>('unknown');

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
        if (!active) return;
        const report = parseEngineReport(event.payload);
        setStatus(report.status);
        if (report.aiRuntime !== 'unknown') setAiRuntime(report.aiRuntime);
      });
      try {
        const current = parseEngineReport(await invoke('engine_status'));
        // Um evento que chegou antes da consulta é mais recente: não o sobrescreve.
        if (active) {
          setStatus((previous) => (previous === 'unknown' ? current.status : previous));
          setAiRuntime((previous) => (previous === 'unknown' ? current.aiRuntime : previous));
        }
      } catch {
        if (active) setStatus('unknown');
      }
    })().catch(() => undefined);

    return () => {
      active = false;
      unlisten?.();
    };
  }, [hosted]);

  return { status, aiRuntime, hosted };
}
