import { useCallback, useEffect, useRef, useState } from 'react';

export type CopyState = 'idle' | 'copied' | 'failed';

interface CopyOptions {
  /** Volta a `idle` depois desse tempo, para o aviso de "copiado" não ficar para sempre. */
  resetAfterMs?: number;
}

export function useCopyToClipboard({ resetAfterMs }: CopyOptions = {}): {
  state: CopyState;
  copy: (text: string) => Promise<void>;
} {
  const [state, setState] = useState<CopyState>('idle');
  const timer = useRef<number | undefined>(undefined);

  useEffect(() => () => window.clearTimeout(timer.current), []);

  const copy = useCallback(
    async (text: string) => {
      window.clearTimeout(timer.current);
      try {
        await navigator.clipboard.writeText(text);
        setState('copied');
      } catch {
        setState('failed');
      }
      if (resetAfterMs !== undefined) timer.current = window.setTimeout(() => setState('idle'), resetAfterMs);
    },
    [resetAfterMs]
  );

  return { state, copy };
}
