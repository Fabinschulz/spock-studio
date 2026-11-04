import { errorMessage } from '@/shared/api';
import type { ModelStatus } from '@/shared/schema';
import { useCallback, useEffect, useState } from 'react';

import type { ModelApi } from '../service/model-api';

/** Estado da chave do modelo e as duas ações sobre ela; cada ação tem o próprio texto de falha. */
export function useModelSettings(api: ModelApi) {
  const [status, setStatus] = useState<ModelStatus | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    api
      .status()
      .then((next) => {
        if (active) setStatus(next);
      })
      .catch((cause: unknown) => {
        if (active) setError(errorMessage(cause, 'Não foi possível ler o estado do modelo.'));
      });
    return () => {
      active = false;
    };
  }, [api]);

  const run = useCallback(async (action: () => Promise<ModelStatus>, failure: string) => {
    setBusy(true);
    setError(null);
    try {
      setStatus(await action());
      return true;
    } catch (cause) {
      setError(errorMessage(cause, failure));
      return false;
    } finally {
      setBusy(false);
    }
  }, []);

  const save = useCallback((key: string) => run(() => api.save(key), 'Não foi possível guardar a chave.'), [api, run]);
  const clear = useCallback(() => run(() => api.clear(), 'Não foi possível remover a chave.'), [api, run]);

  return { status, busy, error, save, clear };
}
