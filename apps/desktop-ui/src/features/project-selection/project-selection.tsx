import { InlineNotice } from '@/shared/components/ui';

import { useProjectSelectionContext } from './context/project-selection-context';
import styles from './project-selection.module.scss';
import { ProjectPicker } from './components/project-picker';

export function ProjectSelection() {
  const { selected, busy, error, cancelled, pick, unavailableReason } = useProjectSelectionContext();

  return (
    <div className={styles.selection}>
      {error ? (
        <InlineNotice tone="danger" title="Não foi possível usar esta pasta">
          {error}
        </InlineNotice>
      ) : null}
      <ProjectPicker selected={selected} busy={busy} onPick={() => void pick()} unavailableReason={unavailableReason} />
      <div role="status" aria-live="polite">
        {cancelled && !error ? (
          <InlineNotice tone="info">Seleção cancelada. Nenhuma pasta foi escolhida e nada foi alterado.</InlineNotice>
        ) : null}
        {busy ? <span className={styles.busy}>Abrindo o seletor de pastas…</span> : null}
      </div>
    </div>
  );
}
