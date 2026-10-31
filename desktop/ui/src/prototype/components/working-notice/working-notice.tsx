import { StatusGlyph } from '@/shared/components/ui';

import screen from '@/prototype/styles/screen.module.scss';

import styles from './working-notice.module.scss';
import type { WorkingNoticeProps } from './types';

// O motor não informa o andamento destas chamadas: sem etapas nem percentual inventados.
export function WorkingNotice({ title, note }: WorkingNoticeProps) {
  return (
    <div className={styles.working} role="status">
      <div className={styles.workingLine}>
        <StatusGlyph state="running" />
        <span>{title}</span>
      </div>
      <div className={styles.indeterminate} aria-hidden="true" />
      <p className={screen.note}>{note}</p>
    </div>
  );
}
