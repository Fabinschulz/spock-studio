import { errorMessage } from '@/shared/api';
import { useCallback, useState } from 'react';

/**
 * Uma ação por vez sobre a central (conectar, testar, vincular...). Guarda o texto da falha, que é o do motor quando ele
 * explica, e avisa quem lista quando a ação deu certo.
 */
export function useAction(onDone: () => void) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const run = useCallback(
    async (action: () => Promise<unknown>, failure: string): Promise<boolean> => {
      setBusy(true);
      setError(null);
      try {
        await action();
        onDone();
        return true;
      } catch (cause) {
        setError(errorMessage(cause, failure));
        return false;
      } finally {
        setBusy(false);
      }
    },
    [onDone]
  );

  return { busy, error, run };
}
