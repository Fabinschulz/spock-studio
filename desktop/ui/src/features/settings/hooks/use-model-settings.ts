import { errorMessage } from '@/shared/api';
import type { ModelStatus } from '@/shared/schema';
import { useAsyncResource } from '@/shared/hooks';
import { useCallback, useState } from 'react';

import type { ModelApi } from '../service/model-api';

/** Estado da chave do modelo e as duas ações sobre ela; cada ação tem o próprio texto de falha. */
export function useModelSettings(api: ModelApi) {
  const load = useCallback(() => api.status(), [api]);
  const initial = useAsyncResource(load);
  // O que as ações devolveram vale mais que a leitura inicial.
  const [updated, setUpdated] = useState<ModelStatus | null>(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const status = updated ?? (initial.status === 'ready' ? initial.value : null);
  const error =
    actionError ??
    (status === null && initial.status === 'error'
      ? errorMessage(initial.error, 'Não foi possível ler o estado do modelo.')
      : null);

  const run = useCallback(async (action: () => Promise<ModelStatus>, failure: string) => {
    setBusy(true);
    setActionError(null);
    try {
      setUpdated(await action());
      return true;
    } catch (cause) {
      setActionError(errorMessage(cause, failure));
      return false;
    } finally {
      setBusy(false);
    }
  }, []);

  const save = useCallback((key: string) => run(() => api.save(key), 'Não foi possível guardar a chave.'), [api, run]);
  const clear = useCallback(() => run(() => api.clear(), 'Não foi possível remover a chave.'), [api, run]);

  return { status, busy, error, save, clear };
}
