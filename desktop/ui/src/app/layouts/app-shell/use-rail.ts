import { useMediaQuery } from '@/shared/hooks';
import { useCallback, useEffect, useState } from 'react';
import { readPreference, writePreference } from './preference';

const STORAGE_KEY = 'spock.rail';
const COLLAPSED = 'collapsed';
const EXPANDED = 'expanded';
// Abaixo de 864 px o menu fica estreito de qualquer jeito; a janela inicial do aplicativo (900 px) ainda o mantém aberto.
const NARROW_WINDOW = '(max-width: 863.98px)';

/** O menu lateral fica estreito por escolha da pessoa, lembrada na próxima abertura, ou porque a janela não comporta. */
export function useRail() {
  const [chosenCollapsed, setChosenCollapsed] = useState(() => readPreference(STORAGE_KEY) === COLLAPSED);
  const narrowWindow = useMediaQuery(NARROW_WINDOW);
  const toggle = useCallback(() => setChosenCollapsed((value) => !value), []);

  useEffect(() => writePreference(STORAGE_KEY, chosenCollapsed ? COLLAPSED : EXPANDED), [chosenCollapsed]);

  return { collapsed: chosenCollapsed || narrowWindow, canToggle: !narrowWindow, toggle };
}
