import { useEffect, useRef } from 'react';

import { shortcutOf, type ShortcutId } from './shortcuts';

/**
 * Um só ouvinte para todos os atalhos do shell; quem chama pode trocar os handlers a cada renderização. Escuta na fase
 * de captura e para o evento ali: o terminal trata Ctrl+J como nova linha e interrompe a propagação, então um ouvinte
 * comum nunca veria o atalho com o foco nele, e fechar o painel é a saída do terminal pelo teclado.
 */
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
      event.stopPropagation();
      handler();
    };
    window.addEventListener('keydown', onKeyDown, { capture: true });
    return () => window.removeEventListener('keydown', onKeyDown, { capture: true });
  }, []);
}
