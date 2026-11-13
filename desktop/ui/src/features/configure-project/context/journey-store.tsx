import { createContext, useContext, useState, type ReactNode } from 'react';
import type { JourneyState } from '../types/journey';

/** Só na memória: fechar o aplicativo recomeça a jornada. */
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

/** Sem este provedor, a jornada recomeça a cada visita ao Configurar. */
export function JourneyStoreProvider({ children }: { children: ReactNode }) {
  const [store] = useState(createJourneyStore);
  return <Context.Provider value={store}>{children}</Context.Provider>;
}

export function useJourneyStore(): JourneyStore | null {
  return useContext(Context);
}
