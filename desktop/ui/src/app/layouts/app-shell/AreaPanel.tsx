import { cn } from '@/shared/utils';
import { useId, type KeyboardEvent } from 'react';
import styles from './AppShell.module.scss';
import { NavEntry } from './NavEntry';
import type { NavArea, NavItem } from './navigation';

export const AREA_PANEL_ID = 'painel-da-area';

interface AreaPanelProps {
  area: NavArea;
  /** Vai no item Execuções. */
  activity?: NavItem['activity'];
  /** Janela estreita: o painel abre por cima do conteúdo e Esc o fecha. */
  overlay: boolean;
  onDismiss: () => void;
}

/** As telas da área aberta. O nome do painel é o da área, para o leitor de tela dizer onde a pessoa está. */
export function AreaPanel({ area, activity, overlay, onDismiss }: AreaPanelProps) {
  const labelId = useId();
  const items = area.items.map((item) => (item.id === 'runs' && activity ? { ...item, activity } : item));

  const onKeyDown = (event: KeyboardEvent) => {
    if (overlay && event.key === 'Escape') onDismiss();
  };

  return (
    // O Esc aqui só fecha a gaveta da janela estreita; quem navega são os links, que já são interativos.
    // eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions
    <nav
      id={AREA_PANEL_ID}
      aria-labelledby={labelId}
      className={cn(styles.panel, overlay && styles.panelOverlay)}
      onKeyDown={onKeyDown}
    >
      <p id={labelId} className={styles.panelTitle}>
        {area.label}
      </p>
      <ul className={styles.nav} role="list">
        {items.map((item) => (
          <li key={item.id}>
            <NavEntry item={item} />
          </li>
        ))}
      </ul>
    </nav>
  );
}
