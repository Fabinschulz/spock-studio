import { useCallback, useEffect, useRef } from 'react';

/**
 * O botão clicado fica desabilitado enquanto o motor responde, ou some depois; sem isto o foco cairia no começo da
 * página. O foco volta ao campo da linha quando a ação termina, e só depois de o campo estar habilitado de novo.
 */
export function useRefocus<T extends { focus: () => void }>(busy: boolean) {
  const target = useRef<T>(null);
  const pending = useRef(false);

  useEffect(() => {
    if (busy || !pending.current) return;
    pending.current = false;
    target.current?.focus();
  }, [busy]);

  const after = useCallback(<R>(action: Promise<R>) => {
    pending.current = true;
    return action;
  }, []);

  return { target, after };
}
