import { Alert } from '@synthra.io/ui-kit';

import { useProjectSelectionContext } from './context/project-selection-context';
import styles from './project-selection.module.scss';
import { ProjectPicker } from './ui/project-picker';

export function ProjectSelection() {
  const { selected, busy, error, pick, unavailableReason } = useProjectSelectionContext();

  return (
    <div className={styles.selection}>
      {error ? <Alert severity="error" description={error} /> : null}
      <ProjectPicker selected={selected} busy={busy} onPick={() => void pick()} unavailableReason={unavailableReason} />
    </div>
  );
}
