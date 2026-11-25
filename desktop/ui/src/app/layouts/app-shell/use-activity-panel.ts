import { readPreference, writePreference } from '@/shared/utils';
import { useCallback, useEffect, useState } from 'react';

const STORAGE_KEY = 'spock.activity';
const WIDTH_KEY = 'spock.activityWidth';
const OPEN = 'open';
const CLOSED = 'closed';

export const ACTIVITY_WIDTH = { min: 288, initial: 336, step: 32 } as const;

/** O painel nunca toma mais da metade da janela: a tela em que a pessoa trabalha continua à vista. */
export function maxActivityWidth(windowWidth: number): number {
  return Math.max(ACTIVITY_WIDTH.min, Math.round(windowWidth * 0.5));
}

export function clampActivityWidth(width: number, windowWidth: number): number {
  return Math.min(Math.max(Math.round(width), ACTIVITY_WIDTH.min), maxActivityWidth(windowWidth));
}

function readWidth(): number {
  const stored = Number(readPreference(WIDTH_KEY));
  return Number.isFinite(stored) && stored > 0 ? stored : ACTIVITY_WIDTH.initial;
}

/** Começa fechado: o painel só ocupa espaço quando a pessoa o pede. Aberto ou fechado e a largura valem para a próxima abertura. */
export function useActivityPanel() {
  const [open, setOpen] = useState(() => readPreference(STORAGE_KEY) === OPEN);
  const [width, setWidth] = useState(readWidth);
  // Conta os pedidos da pessoa: o painel que reabre sozinho ao iniciar não deve puxar o foco.
  const [focusRequest, setFocusRequest] = useState(0);

  const show = useCallback(() => {
    setOpen(true);
    setFocusRequest((value) => value + 1);
  }, []);
  const hide = useCallback(() => setOpen(false), []);
  const resize = useCallback((next: number) => setWidth(clampActivityWidth(next, window.innerWidth)), []);

  useEffect(() => writePreference(STORAGE_KEY, open ? OPEN : CLOSED), [open]);
  useEffect(() => writePreference(WIDTH_KEY, String(width)), [width]);

  return { open, width, focusRequest, show, hide, resize };
}
