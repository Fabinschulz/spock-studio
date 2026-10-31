import { Glyph } from '@/shared/components/ui';

import { IconButton } from '@/prototype/components/icon-button';

import styles from './panel-toggle.module.scss';
import type { PanelToggleProps } from './types';

export function PanelToggle({ open, title, live, onToggle }: PanelToggleProps) {
  const name = title.toLowerCase();

  return (
    <IconButton
      aria-expanded={open}
      aria-controls="painel-contexto"
      aria-label={open ? `Recolher ${name}` : `Abrir ${name}`}
      onClick={onToggle}
    >
      <Glyph name="panel-right" size={17} />
      {live && !open ? <span className={styles.livePip} aria-hidden="true" /> : null}
    </IconButton>
  );
}
