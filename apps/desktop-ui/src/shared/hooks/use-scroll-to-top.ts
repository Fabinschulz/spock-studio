import { useEffect } from 'react';

/** Volta ao topo quando `key` muda: sem isso, a tela nova herda a rolagem da anterior. */
export function useScrollToTop(key: unknown): void {
  useEffect(() => {
    (document.scrollingElement ?? document.documentElement).scrollTo?.({ top: 0 });
  }, [key]);
}
