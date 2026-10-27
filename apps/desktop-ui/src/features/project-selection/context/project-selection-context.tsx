import { createContext, useContext, useMemo, type ReactNode } from 'react';

import { isTauriRuntime, tauriProjectPicker } from '../core/tauri-project-picker';
import type { ProjectPickerPort, SelectedProject } from '../core/types';
import { useProjectSelection } from '../hooks/use-project-selection';

const HOST_UNAVAILABLE = 'Disponível apenas no aplicativo desktop.';

export interface ProjectSelectionValue {
  selected: SelectedProject | null;
  busy: boolean;
  error: string | null;
  /** Abre o diálogo nativo. Devolve a pasta escolhida, ou `null` se a pessoa cancelou ou houve erro. */
  pick: () => Promise<SelectedProject | null>;
  /** Presente quando a seleção não está disponível neste ambiente (ex.: navegador sem o host desktop). */
  unavailableReason?: string;
}

const Context = createContext<ProjectSelectionValue | null>(null);

interface ProjectSelectionProviderProps {
  /** Padrão: o diálogo nativo do host Tauri. Os testes injetam um dublê. */
  picker?: ProjectPickerPort;
  unavailableReason?: string;
  children: ReactNode;
}

/** A pasta escolhida vale para o aplicativo inteiro: a tela inicial e a jornada leem a mesma seleção. */
export function ProjectSelectionProvider({ picker, unavailableReason, children }: ProjectSelectionProviderProps) {
  const reason = unavailableReason ?? (picker || isTauriRuntime() ? undefined : HOST_UNAVAILABLE);
  const { selected, busy, error, pick } = useProjectSelection(picker ?? tauriProjectPicker);

  const value = useMemo<ProjectSelectionValue>(
    () => ({ selected, busy, error, pick, unavailableReason: reason }),
    [selected, busy, error, pick, reason]
  );
  return <Context.Provider value={value}>{children}</Context.Provider>;
}

export function useProjectSelectionContext(): ProjectSelectionValue {
  const value = useContext(Context);
  if (!value) throw new Error('useProjectSelectionContext precisa estar dentro de ProjectSelectionProvider.');
  return value;
}
