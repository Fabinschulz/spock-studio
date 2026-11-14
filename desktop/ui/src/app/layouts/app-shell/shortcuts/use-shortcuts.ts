import { useEffect, useRef } from 'react';

import { shortcutOf, type ShortcutId } from './shortcuts';

/** Um só ouvinte para todos os atalhos do shell; quem chama pode trocar os handlers a cada renderização. */
export function useShortcuts(handlers: Partial<Record<ShortcutId, () => void>>) {
  const latest = useRef(handlers);

  useEffect(() => {
    latest.current = handlers;
  });

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const id = shortcutOf(event);
      const handler = id ? latest.current[id] : undefined;
      if (!handler) return;
      event.preventDefault();
      handler();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);
}
