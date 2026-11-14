import { useSyncExternalStore } from 'react';

// O `change` do MediaQueryList não chega em toda WebView (nem existe nas antigas do macOS); o `resize` da janela, sim.
function subscribe(onChange: () => void) {
  window.addEventListener('resize', onChange);
  return () => window.removeEventListener('resize', onChange);
}

/** Sem `matchMedia` (jsdom) a consulta responde que não casa: o layout padrão é o da janela larga. */
export function useMediaQuery(query: string): boolean {
  return useSyncExternalStore(
    subscribe,
    () => typeof window.matchMedia === 'function' && window.matchMedia(query).matches,
    () => false
  );
}
