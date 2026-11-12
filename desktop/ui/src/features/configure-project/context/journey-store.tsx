import { createContext, useContext, useState, type ReactNode } from 'react';
import type { JourneyState } from '../types/journey';

/** Onde a jornada de cada projeto fica guardada enquanto a pessoa está em outra tela. Só na memória: fechar o aplicativo recomeça. */
export interface JourneyStore {
  read(projectId: string): JourneyState | null;
  write(projectId: string, state: JourneyState): void;
  clear(projectId: string): void;
}

export function createJourneyStore(): JourneyStore {
  const saved = new Map<string, JourneyState>();
  return {
    read: (projectId) => saved.get(projectId) ?? null,
    write: (projectId, state) => void saved.set(projectId, state),
    clear: (projectId) => void saved.delete(projectId)
  };
}

const Context = createContext<JourneyStore | null>(null);

/**
 * Guarda a jornada Configurar por projeto, para ela sobreviver a ir ao Projeto, à Gestão ou ao Pedido de engenharia e
 * voltar. Sem este provedor, a jornada funciona como antes e recomeça a cada visita.
 */
export function JourneyStoreProvider({ children }: { children: ReactNode }) {
  const [store] = useState(createJourneyStore);
  return <Context.Provider value={store}>{children}</Context.Provider>;
}

export function useJourneyStore(): JourneyStore | null {
  return useContext(Context);
}
