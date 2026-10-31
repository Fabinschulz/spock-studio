import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { isTauriRuntime, tauriProjectPicker } from '../domain/tauri-project-picker';
import type { ProjectPickerPort, RecentProject, SelectedProject } from '../domain/types';
import { useProjectSelection } from '../hooks/use-project-selection';

const HOST_UNAVAILABLE = 'Disponível apenas no aplicativo desktop.';

export interface ProjectSelectionValue {
  selected: SelectedProject | null;
  busy: boolean;
  error: string | null;
  /** A pessoa fechou o diálogo sem escolher. Volta a falso na próxima tentativa. */
  cancelled: boolean;
  /** Abre o diálogo nativo. Devolve a pasta escolhida, ou `null` se a pessoa cancelou ou houve erro. */
  pick: () => Promise<SelectedProject | null>;
  recents: RecentProject[];
  /** Seleciona um recente pelo id. Devolve a pasta, ou `null` se não foi possível. */
  openRecent: (id: string) => Promise<SelectedProject | null>;
  unavailableReason?: string;
}

const Context = createContext<ProjectSelectionValue | null>(null);

interface ProjectSelectionProviderProps {
  picker?: ProjectPickerPort;
  unavailableReason?: string;
  children: ReactNode;
}

export function ProjectSelectionProvider({ picker, unavailableReason, children }: ProjectSelectionProviderProps) {
  const reason = unavailableReason ?? (picker || isTauriRuntime() ? undefined : HOST_UNAVAILABLE);
  const { selected, busy, error, cancelled, recents, pick, openRecent } = useProjectSelection(
    picker ?? tauriProjectPicker
  );

  const value = useMemo<ProjectSelectionValue>(
    () => ({ selected, busy, error, cancelled, recents, pick, openRecent, unavailableReason: reason }),
    [selected, busy, error, cancelled, recents, pick, openRecent, reason]
  );
  return <Context.Provider value={value}>{children}</Context.Provider>;
}

export function useProjectSelectionContext(): ProjectSelectionValue {
  const value = useContext(Context);
  if (!value) throw new Error('useProjectSelectionContext precisa estar dentro de ProjectSelectionProvider.');
  return value;
}
