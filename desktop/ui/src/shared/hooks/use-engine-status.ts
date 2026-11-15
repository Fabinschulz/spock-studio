import { isTauriRuntime } from '@/shared/api';
import { useEffect, useState } from 'react';
import { z } from 'zod';

/** Estado real do motor .NET, como o host o informa (comando e evento `engine_status`). */
export type EngineStatus = 'unknown' | 'starting' | 'ready' | 'crashed';

/** Indisponível não significa que o motor caiu. */
export type AiRuntimeStatus = 'unknown' | 'ready' | 'stopped' | 'unavailable';

export interface EngineReport {
  status: EngineStatus;
  aiRuntime: AiRuntimeStatus;
}

// Valor fora do vocabulário vira `unknown`: um texto novo do host nunca é lido como "pronto".
const engineStatusSchema = z.enum(['unknown', 'starting', 'ready', 'crashed']).catch('unknown');
const aiRuntimeSchema = z.enum(['unknown', 'ready', 'stopped', 'unavailable']).catch('unknown');

const reportSchema = z.union([
  // Texto antigo do host: só o estado do motor.
  z.string().transform((engine): EngineReport => ({ status: engineStatusSchema.parse(engine), aiRuntime: 'unknown' })),
  z.object({ engine: z.unknown(), aiRuntime: z.unknown() }).transform((report): EngineReport => ({
    status: engineStatusSchema.parse(report.engine),
    aiRuntime: aiRuntimeSchema.parse(report.aiRuntime)
  }))
]);

const UNKNOWN: EngineReport = { status: 'unknown', aiRuntime: 'unknown' };

/**
 * O evento do host só traz o estado do motor; a inteligência local sobe no primeiro pedido e muda sem aviso. Sem
 * reconsultar, a barra dizia "parada" com um workflow rodando.
 */
export const AI_RUNTIME_REFRESH_MS = 10_000;

export const parseEngineReport = (value: unknown): EngineReport => {
  const parsed = reportSchema.safeParse(value);
  return parsed.success ? parsed.data : UNKNOWN;
};

/** Fora do aplicativo desktop não há motor: `hosted` é falso, para a tela dizer isso em vez de fingir que está pronto. */
export function useEngineStatus(): EngineReport & { hosted: boolean } {
  const hosted = isTauriRuntime();
  const [status, setStatus] = useState<EngineStatus>('unknown');
  const [aiRuntime, setAiRuntime] = useState<AiRuntimeStatus>('unknown');

  useEffect(() => {
    if (!hosted) return;
    let active = true;
    let unlisten: (() => void) | undefined;
    let timer: ReturnType<typeof setInterval> | undefined;

    const follow = async () => {
      const [{ invoke }, { listen }] = await Promise.all([
        import('@tauri-apps/api/core'),
        import('@tauri-apps/api/event')
      ]);
      const stop = await listen<unknown>('engine_status', (event) => {
        if (!active) return;
        const report = parseEngineReport(event.payload);
        setStatus(report.status);
        if (report.aiRuntime !== 'unknown') setAiRuntime(report.aiRuntime);
      });
      // Desmontou enquanto a escuta era ligada: desliga agora, senão ela ficaria viva para sempre.
      if (!active) {
        stop();
        return;
      }
      unlisten = stop;

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

      // A consulta ao runtime só pergunta (runtime.health não liga o Python): pode repetir sem custo.
      timer = setInterval(() => {
        invoke('engine_status')
          .then((value) => {
            const report = parseEngineReport(value);
            if (active && report.aiRuntime !== 'unknown') setAiRuntime(report.aiRuntime);
          })
          .catch(() => undefined);
      }, AI_RUNTIME_REFRESH_MS);
      if (!active) clearInterval(timer);
    };
    follow().catch(() => undefined);

    return () => {
      active = false;
      unlisten?.();
      if (timer) clearInterval(timer);
    };
  }, [hosted]);

  return { status, aiRuntime, hosted };
}
