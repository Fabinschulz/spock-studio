import { Alert } from '@synthra.io/ui-kit';

import { isTauriRuntime, tauriProjectPicker } from './core/tauri-project-picker';
import type { ProjectPickerPort } from './core/types';
import { useProjectSelection } from './hooks/use-project-selection';
import styles from './project-selection.module.scss';
import { ProjectPicker } from './ui/project-picker';

const HOST_UNAVAILABLE = 'Disponível apenas no aplicativo desktop.';

interface ProjectSelectionProps {
  /** Padrão: o diálogo nativo do host Tauri. Os testes injetam um dublê. */
  picker?: ProjectPickerPort;
  unavailableReason?: string;
}

export function ProjectSelection({ picker, unavailableReason }: ProjectSelectionProps) {
  const reason = unavailableReason ?? (picker || isTauriRuntime() ? undefined : HOST_UNAVAILABLE);
  const { selected, busy, error, pick } = useProjectSelection(picker ?? tauriProjectPicker);

  return (
    <div className={styles.selection}>
      {error ? <Alert severity="error" description={error} /> : null}
      <ProjectPicker selected={selected} busy={busy} onPick={pick} unavailableReason={reason} />
    </div>
  );
}
