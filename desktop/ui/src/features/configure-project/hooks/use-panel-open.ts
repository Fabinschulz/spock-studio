import { useContentArea } from '@/shared/hooks';
import { useCallback, useState, useSyncExternalStore, type RefObject } from 'react';

// A mesma medida da container query de `journey-frame.module.scss`: com a área de conteúdo a partir de 62rem o painel
// fica ao lado do conteúdo; abaixo disso, por cima dele. Quem decide é a área de conteúdo, não a janela: o menu da área e
// o painel de atividade também ocupam a largura da janela.
const BESIDE_MIN_REM = 62;

type Area = RefObject<HTMLElement | null> | null;

function observeArea(area: Area, onChange: () => void): () => void {
  const element = area?.current;
  if (!element || typeof ResizeObserver === 'undefined') return () => undefined;
  const observer = new ResizeObserver(onChange);
  observer.observe(element);
  return () => observer.disconnect();
}

function fitsBeside(area: Area): boolean {
  const element = area?.current;
  if (!element) return false;
  const rem = parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
  return element.clientWidth >= BESIDE_MIN_REM * rem;
}

function useFitsBeside(): boolean {
  const area = useContentArea();
  const subscribe = useCallback((onChange: () => void) => observeArea(area, onChange), [area]);
  const snapshot = useCallback(() => fitsBeside(area), [area]);

  return useSyncExternalStore(subscribe, snapshot, () => false);
}

/**
 * Sem escolha da pessoa, o painel segue o espaço: aberto quando cabe ao lado do conteúdo, fechado quando o cobriria. A
 * escolha dela vale enquanto o espaço não muda de faixa; ao mudar, o painel volta a seguir o espaço.
 */
export function usePanelOpen() {
  const fits = useFitsBeside();
  const [choice, setChoice] = useState<{ open: boolean; fits: boolean } | null>(null);
  const open = choice !== null && choice.fits === fits ? choice.open : fits;

  const toggle = useCallback(() => setChoice({ open: !open, fits }), [open, fits]);
  const close = useCallback(() => setChoice({ open: false, fits }), [fits]);

  return { open, toggle, close };
}
