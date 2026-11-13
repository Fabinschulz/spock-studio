import { errorMessage } from '@/shared/api';
import type { ModelConnection } from '@/shared/schema';
import { useAsyncResource } from '@/shared/hooks';
import { useCallback, useState } from 'react';

import type { ModelApi } from '../service/model-api';

/** O provedor de modelo como conexão e as três ações sobre ele; cada ação tem o próprio texto de falha e de sucesso. */
export function useModelSettings(api: ModelApi) {
  const load = useCallback(() => api.connection(), [api]);
  const initial = useAsyncResource(load);
  // O que as ações devolveram vale mais que a leitura inicial.
  const [updated, setUpdated] = useState<ModelConnection | null>(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const connection = updated ?? (initial.status === 'ready' ? initial.value : null);
  const error =
    actionError ??
    (connection === null && initial.status === 'error'
      ? errorMessage(initial.error, 'Não foi possível ler o estado do modelo.')
      : null);

  const run = useCallback(
    async (
      action: () => Promise<ModelConnection>,
      failure: string,
      success: (next: ModelConnection) => string | null
    ) => {
      setBusy(true);
      setActionError(null);
      setNotice(null);
      try {
        const next = await action();
        setUpdated(next);
        setNotice(success(next));
        return true;
      } catch (cause) {
        setActionError(errorMessage(cause, failure));
        return false;
      } finally {
        setBusy(false);
      }
    },
    []
  );

  const save = useCallback(
    (provider: string, key: string) =>
      run(
        async () => {
          await api.save(provider, key);
          return api.connection();
        },
        'Não foi possível guardar a chave.',
        () => 'Chave guardada: o provedor a aceitou.'
      ),
    [api, run]
  );

  const test = useCallback(
    () =>
      run(
        () => api.test(),
        'Não foi possível conferir a chave.',
        // Uma recusa vem no estado, não como falha: só um "conectado" é sucesso.
        (next) => (next.status === 'connected' ? 'O provedor aceitou a chave.' : null)
      ),
    [api, run]
  );

  const choose = useCallback(
    (purpose: string, model: string | null, label: string) =>
      run(
        () => api.setPurpose(purpose, model),
        'Não foi possível trocar o modelo.',
        () => `Modelo de ${label.toLowerCase()} atualizado.`
      ),
    [api, run]
  );

  const clear = useCallback(
    () =>
      run(
        async () => {
          await api.clear();
          return api.connection();
        },
        'Não foi possível remover a chave.',
        () => 'Chave removida deste computador.'
      ),
    [api, run]
  );

  return { connection, busy, error, notice, save, test, choose, clear };
}
