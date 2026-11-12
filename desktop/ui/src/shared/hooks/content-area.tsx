import { createContext, useContext, type RefObject } from 'react';

/**
 * A área da tela que rola e recebe o foco quando o conteúdo muda: no aplicativo, o `<main>` do shell. O documento em si
 * não rola (o shell tem a altura da janela), então rolar `document` não faz nada; quem precisa voltar ao topo precisa
 * saber qual é a área.
 */
const ContentAreaContext = createContext<RefObject<HTMLElement | null> | null>(null);

export const ContentAreaProvider = ContentAreaContext.Provider;

export function useContentArea(): RefObject<HTMLElement | null> | null {
  return useContext(ContentAreaContext);
}
