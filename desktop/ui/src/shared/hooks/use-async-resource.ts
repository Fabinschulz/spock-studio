import { useCallback, useEffect, useMemo, useState } from 'react';

export type Resource<T> = (
  { status: 'loading' } | { status: 'error'; error: unknown } | { status: 'ready'; value: T }
) & {
  /** Só existe em leituras que sabem se refazer: é o que dá o "Tentar de novo" ao `ResourceView`. */
  reload?: () => void;
};

const LOADING = { status: 'loading' } as const;

/**
 * Quem chama estabiliza `load` (useCallback ou useMemo): trocá-lo descarta a leitura em andamento e volta a `loading`,
 * sem mostrar o resultado da anterior. Com `keepPrevious`, um `reload` do mesmo `load` deixa o resultado anterior na tela
 * até o novo chegar, como um botão "Ler de novo" espera.
 */
export function useAsyncResource<T>(load: () => Promise<T>, { keepPrevious = false } = {}): Resource<T> {
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
  const sameLoad = state?.load === load;
  const outcome = sameLoad && (state.attempt === attempt || keepPrevious) ? state.outcome : LOADING;
  return useMemo(() => ({ ...outcome, reload }), [outcome, reload]);
}
