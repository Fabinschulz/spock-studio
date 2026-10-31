import { Glyph } from '@/shared/components/ui';

import { IconButton } from '@/prototype/components/icon-button';

import styles from './context-drawer.module.scss';
import type { ContextDrawerProps } from './types';

export function ContextDrawer({ title, overlay, onClose, children }: ContextDrawerProps) {
  return (
    <aside id="painel-contexto" className={`${styles.panel} ${overlay ? styles.panelOverlay : ''}`} aria-label={title}>
      <div className={styles.panelHead}>
        <h2 className={styles.panelTitle}>{title}</h2>
        <IconButton aria-label={`Recolher ${title.toLowerCase()}`} onClick={onClose}>
          <Glyph name="x" size={15} />
        </IconButton>
      </div>
      <div className={styles.panelBody}>{children}</div>
    </aside>
  );
}
