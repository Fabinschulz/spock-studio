import { useEffect, useState } from 'react';

export type Resource<T> = { status: 'loading' } | { status: 'error'; error: unknown } | { status: 'ready'; value: T };

/**
 * Lê algo uma vez por `load` e guarda o desfecho. Quem chama estabiliza `load` (useCallback ou useMemo): trocá-lo
 * descarta a leitura que ainda estava em andamento e volta a `loading`, sem mostrar o resultado da leitura anterior.
 */
export function useAsyncResource<T>(load: () => Promise<T>): Resource<T> {
  const [state, setState] = useState<{ load: () => Promise<T>; resource: Resource<T> } | null>(null);

  useEffect(() => {
    let active = true;
    load().then(
      (value) => {
        if (active) setState({ load, resource: { status: 'ready', value } });
      },
      (error: unknown) => {
        if (active) setState({ load, resource: { status: 'error', error } });
      }
    );
    return () => {
      active = false;
    };
  }, [load]);

  return state?.load === load ? state.resource : { status: 'loading' };
}
