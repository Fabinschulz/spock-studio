import type { EngineClient } from '@/shared/api';
import { useEffect, useState } from 'react';

import { readRunActivity, type RunActivity } from '../service/runs-api';

/** De quanto em quanto tempo o menu relê o que está andando. O motor lê os diários a cada leitura: não há por que correr. */
export const ACTIVITY_REFRESH_MS = 20_000;

const same = (a: RunActivity | null, b: RunActivity) =>
  a !== null && a.running === b.running && a.awaitingApproval === b.awaitingApproval;

/**
 * O que está andando ou esperando aprovação, para o indicador do menu. Relê ao ligar, a cada {@link ACTIVITY_REFRESH_MS}
 * e quando `refreshKey` muda (a pessoa trocou de tela, e uma aprovação pode ter chegado). É um aviso, não uma tela: uma
 * leitura que falha deixa o último valor, e sem motor pronto não há indicador, em vez de um número velho.
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
