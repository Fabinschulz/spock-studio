import { EngineError, errorMessage } from '@/shared/api';
import { useCallback, useState } from 'react';

export type AskError = { message: string; needsKey: boolean };

/**
 * Uma pergunta por vez. Falha vira `null` com o erro guardado, e a falta de chave fica marcada para a tela oferecer o
 * caminho, não só a mensagem do motor.
 */
export function useAsk() {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<AskError | null>(null);

  const run = useCallback(async <T>(request: () => Promise<T>): Promise<T | null> => {
    setBusy(true);
    setError(null);
    try {
      return await request();
    } catch (cause) {
      setError({
        message: errorMessage(cause, 'Não foi possível obter a resposta.'),
        needsKey: cause instanceof EngineError && cause.code === 'MODEL_NOT_CONFIGURED'
      });
      return null;
    } finally {
      setBusy(false);
    }
  }, []);

  return { busy, error, run };
}
