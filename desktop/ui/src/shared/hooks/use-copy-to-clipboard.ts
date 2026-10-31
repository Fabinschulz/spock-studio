import { useCallback, useState } from 'react';

export type CopyState = 'idle' | 'copied' | 'failed';

export function useCopyToClipboard(): { state: CopyState; copy: (text: string) => Promise<void> } {
  const [state, setState] = useState<CopyState>('idle');

  const copy = useCallback(async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setState('copied');
    } catch {
      setState('failed');
    }
  }, []);

  return { state, copy };
}
