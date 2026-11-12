import { errorMessage } from '@/shared/api';
import { useCallback, useState } from 'react';

/**
 * Uma ação por vez sobre a central (conectar, testar, vincular...). Guarda o texto da falha, que é o do motor quando ele
 * explica, e avisa quem lista quando a ação deu certo.
 *
 * A mensagem de sucesso é opcional e sai do resultado da ação, não de ela não ter falhado: `connection.test`, por exemplo,
 * não falha quando o provedor recusa a credencial, só marca a conexão com erro. Devolver `null` não diz nada.
 *
 * `target` diz a que linha a mensagem pertence, para a tela mostrá-la ao lado da ação; sem ele, a mensagem é da seção.
 */
export interface ActionNotice {
  text: string;
  target?: string;
}

interface RunOptions<T> {
  success?: (result: T) => string | null;
  target?: string;
}

export function useAction(onDone: () => void) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<ActionNotice | null>(null);

  const run = useCallback(
    async <T>(action: () => Promise<T>, failure: string, { success, target }: RunOptions<T> = {}): Promise<boolean> => {
      setBusy(true);
      setError(null);
      setNotice(null);
      try {
        const result = await action();
        onDone();
        const text = success?.(result) ?? null;
        setNotice(text === null ? null : { text, target });
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

  return { busy, error, notice, run };
}
