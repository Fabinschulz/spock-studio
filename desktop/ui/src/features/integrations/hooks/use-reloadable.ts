import { useCallback, useEffect, useState } from 'react';

export type Reloadable<T> = {
  /** O último valor lido. Continua na tela enquanto uma releitura está em andamento. */
  value: T | null;
  error: unknown;
  settled: boolean;
  reload: () => void;
};

/**
 * Uma leitura que pode ser refeita sem esvaziar a tela. Quem troca de assunto (outro projeto) remonta o componente com
 * `key`: o valor guardado é sempre do mesmo `load`.
 */
export function useReloadable<T>(load: () => Promise<T>): Reloadable<T> {
  const [state, setState] = useState<{ value: T | null; error: unknown; settled: boolean }>({
    value: null,
    error: null,
    settled: false
  });
  const [tick, setTick] = useState(0);

  useEffect(() => {
    let active = true;
    load().then(
      (value) => {
        if (active) setState({ value, error: null, settled: true });
      },
      (error: unknown) => {
        if (active) setState((previous) => ({ ...previous, error, settled: true }));
      }
    );
    return () => {
      active = false;
    };
  }, [load, tick]);

  const reload = useCallback(() => setTick((current) => current + 1), []);
  return { ...state, reload };
}
