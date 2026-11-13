import type { EngineClient } from '@/shared/api';
import { useEffect, useState } from 'react';

import { readRunActivity, type RunActivity } from '../service/runs-api';

/** O motor lê os diários a cada leitura: não há por que correr. */
export const ACTIVITY_REFRESH_MS = 20_000;

const same = (a: RunActivity | null, b: RunActivity) =>
  a !== null && a.running === b.running && a.awaitingApproval === b.awaitingApproval;

/**
 * Relê também quando `refreshKey` muda: uma aprovação pode ter chegado. Uma leitura que falha deixa o último valor, e
 * sem motor pronto não há indicador, em vez de um número velho.
 */
export function useRunActivity(client: EngineClient, enabled: boolean, refreshKey: string): RunActivity | null {
  const [activity, setActivity] = useState<RunActivity | null>(null);

  useEffect(() => {
    if (!enabled) return;
    let active = true;

    const read = () => {
      if (document.hidden) return;
      readRunActivity(client).then(
        (next) => {
          if (active) setActivity((previous) => (same(previous, next) ? previous : next));
        },
        () => {
          // O indicador é um aviso: falhar em lê-lo não é um erro para mostrar.
        }
      );
    };

    read();
    const timer = setInterval(read, ACTIVITY_REFRESH_MS);
    return () => {
      active = false;
      clearInterval(timer);
    };
  }, [client, enabled, refreshKey]);

  return enabled ? activity : null;
}
