import { useCallback, useEffect, useState } from 'react';

import { readPreference, writePreference } from '../preference';

export type PanelTab = 'terminal' | 'output';

const OPEN_KEY = 'spock.panel';
const TAB_KEY = 'spock.panelTab';
const HEIGHT_KEY = 'spock.panelHeight';

export const PANEL_HEIGHT = { min: 140, initial: 260, step: 32 } as const;

/** O painel nunca toma mais de 65% da janela: o conteúdo da tela continua à vista. */
export function clampHeight(height: number, windowHeight: number): number {
  const max = Math.max(PANEL_HEIGHT.min, Math.round(windowHeight * 0.65));
  return Math.min(Math.max(Math.round(height), PANEL_HEIGHT.min), max);
}

function readHeight(): number {
  const stored = Number(readPreference(HEIGHT_KEY));
  return Number.isFinite(stored) && stored > 0 ? stored : PANEL_HEIGHT.initial;
}

export function useBottomPanel() {
  const [open, setOpen] = useState(() => readPreference(OPEN_KEY) === 'open');
  const [tab, setTab] = useState<PanelTab>(() => (readPreference(TAB_KEY) === 'output' ? 'output' : 'terminal'));
  const [height, setHeight] = useState(readHeight);

  useEffect(() => writePreference(OPEN_KEY, open ? 'open' : 'closed'), [open]);
  useEffect(() => writePreference(TAB_KEY, tab), [tab]);
  useEffect(() => writePreference(HEIGHT_KEY, String(height)), [height]);

  const show = useCallback((target: PanelTab) => {
    setTab(target);
    setOpen(true);
  }, []);
  const close = useCallback(() => setOpen(false), []);
  // O botão da aba que já está à vista fecha o painel; o de outra aba troca para ela.
  const toggleTab = useCallback(
    (target: PanelTab) => {
      if (open && tab === target) setOpen(false);
      else show(target);
    },
    [open, tab, show]
  );
  const resize = useCallback((next: number) => setHeight(clampHeight(next, window.innerHeight)), []);

  return { open, tab, height, show, close, toggleTab, resize, selectTab: setTab };
}
