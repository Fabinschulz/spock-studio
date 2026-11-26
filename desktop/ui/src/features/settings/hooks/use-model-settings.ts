import type { ModelConnection } from '@/shared/schema';
import { useAsyncResource } from '@/shared/hooks';
import { useCallback, useState } from 'react';

import { describeFailure } from '../domain/model-errors';
import type { ModelApi } from '../service/model-api';

/** De onde veio a ação: o retorno aparece ali, não no topo da página. */
export type FeedbackTarget = `provider:${string}` | `purpose:${string}` | `budget:${string}`;

export interface Feedback {
  target: FeedbackTarget;
  tone: 'success' | 'danger';
  text: string;
}

export function useModelSettings(api: ModelApi) {
  const load = useCallback(() => api.connection(), [api]);
  const initial = useAsyncResource(load);
  // O que as ações devolveram vale mais que a leitura inicial.
  const [updated, setUpdated] = useState<ModelConnection | null>(null);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<Feedback | null>(null);

  const connection = updated ?? (initial.status === 'ready' ? initial.value : null);
  const loadError =
    connection === null && initial.status === 'error'
      ? describeFailure(initial.error, 'Não foi possível ler o estado do modelo.')
      : null;

  const run = useCallback(
    async (
      target: FeedbackTarget,
      action: () => Promise<ModelConnection>,
      failure: string,
      success: (next: ModelConnection) => string | null
    ) => {
      setBusy(true);
      setFeedback(null);
      try {
        const next = await action();
        setUpdated(next);
        const text = success(next);
        if (text) setFeedback({ target, tone: 'success', text });
        return true;
      } catch (cause) {
        setFeedback({ target, tone: 'danger', text: describeFailure(cause, failure) });
        return false;
      } finally {
        setBusy(false);
      }
    },
    []
  );

  const save = useCallback(
    (provider: string, name: string, key: string) =>
      run(
        `provider:${provider}`,
        async () => {
          await api.save(provider, key);
          return api.connection();
        },
        'Não foi possível guardar a chave.',
        () => `Chave de ${name} guardada: o provedor a aceitou.`
      ),
    [api, run]
  );

  const test = useCallback(
    (provider: string, name: string) =>
      run(
        `provider:${provider}`,
        () => api.test(provider),
        'Não foi possível conferir a chave.',
        // Uma recusa vem no estado, não como falha: só um "conectado" é sucesso.
        (next) =>
          next.providers.find((entry) => entry.id === provider)?.status === 'connected'
            ? `${name} aceitou a chave.`
            : null
      ),
    [api, run]
  );

  const choose = useCallback(
    (purpose: string, model: string | null, label: string) =>
      run(
        `purpose:${purpose}`,
        () => api.setPurpose(purpose, model),
        'Não foi possível trocar o modelo.',
        () => `Modelo de ${label.toLowerCase()} atualizado.`
      ),
    [api, run]
  );

  const setBudget = useCallback(
    (purpose: string, tokens: number | null, label: string) =>
      run(
        `budget:${purpose}`,
        () => api.setBudget(purpose, tokens),
        'Não foi possível trocar o teto de tokens.',
        (next) => {
          const budget = next.budgets.find((entry) => entry.purpose === purpose);
          return budget
            ? `Teto de ${label.toLowerCase()}: ${budget.tokens.toLocaleString('pt-BR')} tokens por execução.`
            : null;
        }
      ),
    [api, run]
  );

  const remove = useCallback(
    (provider: string, name: string) =>
      run(
        `provider:${provider}`,
        () => api.remove(provider),
        'Não foi possível remover a chave.',
        () => `Chave de ${name} removida deste computador.`
      ),
    [api, run]
  );

  const setDefault = useCallback(
    (provider: string, name: string) =>
      run(
        `provider:${provider}`,
        () => api.setDefault(provider),
        'Não foi possível trocar o provedor padrão.',
        () => `${name} é o provedor padrão.`
      ),
    [api, run]
  );

  return {
    connection,
    busy,
    loadError,
    reload: initial.reload,
    feedback,
    save,
    test,
    choose,
    setBudget,
    remove,
    setDefault
  };
}
