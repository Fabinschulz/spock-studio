import { Modal } from '@mariliz/ui-kit';

import styles from './ShortcutSheet.module.scss';
import { SHORTCUTS, shortcutLabel } from './shortcuts';

export function ShortcutSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  return (
    <Modal open={open} onClose={onClose} title="Atalhos de teclado" closeLabel="Fechar" sizeModal="small">
      <dl className={styles.list}>
        {SHORTCUTS.map((shortcut) => (
          <div key={shortcut.id} className={styles.row}>
            <dt>
              <kbd className={styles.keys}>{shortcutLabel(shortcut.id)}</kbd>
            </dt>
            <dd>{shortcut.description}</dd>
          </div>
        ))}
      </dl>
    </Modal>
  );
}
