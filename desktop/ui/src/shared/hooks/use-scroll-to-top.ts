import { useEffect, useRef, type RefObject } from 'react';
import { useContentArea } from './content-area';

interface ScrollToTopOptions {
  /**
   * Além de voltar ao topo, leva o foco do teclado para a área de conteúdo quando `key` muda. É o que quem usa leitor de
   * tela ou teclado espera ao trocar de tela ou de etapa: sem isso o foco fica num botão que já não está lá. Não acontece
   * na primeira renderização, para o aplicativo não tirar o foco de onde ele começa.
   */
  focus?: boolean;
  /** A área que rola. Padrão: a que o shell informa; sem shell, o documento. */
  container?: RefObject<HTMLElement | null>;
}

/** Volta ao topo quando `key` muda: sem isso, a tela nova herda a rolagem da anterior. */
export function useScrollToTop(key: unknown, { focus = false, container }: ScrollToTopOptions = {}): void {
  const fromShell = useContentArea();
  const area = container ?? fromShell;
  const previous = useRef(key);

  useEffect(() => {
    const target = area?.current ?? document.scrollingElement ?? document.documentElement;
    target.scrollTo?.({ top: 0 });

    // Comparar com a chave anterior (e não "é a primeira vez?") mantém a regra certa quando o React remonta o efeito.
    if (focus && previous.current !== key) area?.current?.focus({ preventScroll: true });
    previous.current = key;
  }, [key, area, focus]);
}
