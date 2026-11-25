import { useMediaQuery } from '@/shared/hooks';
import { useCallback, useEffect, useState } from 'react';
import { readPreference, writePreference } from '@/shared/utils';

const STORAGE_KEY = 'spock.rail';
const COLLAPSED = 'collapsed';
const EXPANDED = 'expanded';
// Abaixo de 864 px o painel não cabe ao lado do conteúdo; a janela inicial do aplicativo (900 px) ainda o mantém aberto.
const NARROW_WINDOW = '(max-width: 863.98px)';
// Com a janela muito estreita (ou muito zoom) a barra fica só com os ícones.
const COMPACT_WINDOW = '(max-width: 599.98px)';

/**
 * O painel da área fica recolhido por escolha da pessoa, lembrada na próxima abertura. Na janela estreita ele começa
 * recolhido e, aberto, fica por cima do conteúdo só na tela em que foi pedido: trocar de tela o fecha.
 */
export function useRail(pathname: string) {
  const [chosenCollapsed, setChosenCollapsed] = useState(() => readPreference(STORAGE_KEY) === COLLAPSED);
  const [peekAt, setPeekAt] = useState<string | null>(null);
  const narrow = useMediaQuery(NARROW_WINDOW);
  const compact = useMediaQuery(COMPACT_WINDOW);
  const peeking = narrow && peekAt === pathname;

  const toggle = useCallback(() => {
    if (narrow) setPeekAt((current) => (current === pathname ? null : pathname));
    else setChosenCollapsed((value) => !value);
  }, [narrow, pathname]);
  const dismiss = useCallback(() => setPeekAt(null), []);

  useEffect(() => writePreference(STORAGE_KEY, chosenCollapsed ? COLLAPSED : EXPANDED), [chosenCollapsed]);

  return { collapsed: narrow ? !peeking : chosenCollapsed, overlay: peeking, compact, toggle, dismiss };
}
