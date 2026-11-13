import { useCallback, useEffect, useMemo, useState } from 'react';

export type Resource<T> = (
  { status: 'loading' } | { status: 'error'; error: unknown } | { status: 'ready'; value: T }
) & {
  /** Refaz a leitura. Só existe em leituras que sabem se refazer (`useAsyncResource`): é o que dá o "Tentar de novo" ao `ResourceView`. */
  reload?: () => void;
};

const LOADING = { status: 'loading' } as const;

/**
 * Lê algo uma vez por `load` e guarda o desfecho. Quem chama estabiliza `load` (useCallback ou useMemo): trocá-lo
 * descarta a leitura que ainda estava em andamento e volta a `loading`, sem mostrar o resultado da leitura anterior.
 * `reload` refaz a leitura (volta a `loading`); o resultado só muda de identidade quando a leitura muda.
 */
export function useAsyncResource<T>(load: () => Promise<T>): Resource<T> {
  const [attempt, setAttempt] = useState(0);
  const [state, setState] = useState<{
    load: () => Promise<T>;
    attempt: number;
    outcome: { status: 'error'; error: unknown } | { status: 'ready'; value: T };
  } | null>(null);

  useEffect(() => {
    let active = true;
    load().then(
      (value) => {
        if (active) setState({ load, attempt, outcome: { status: 'ready', value } });
      },
      (error: unknown) => {
        if (active) setState({ load, attempt, outcome: { status: 'error', error } });
      }
    );
    return () => {
      active = false;
    };
  }, [load, attempt]);

  const reload = useCallback(() => setAttempt((current) => current + 1), []);
  const outcome = state?.load === load && state.attempt === attempt ? state.outcome : LOADING;
  return useMemo(() => ({ ...outcome, reload }), [outcome, reload]);
}
