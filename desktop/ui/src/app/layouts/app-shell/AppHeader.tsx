import { ProjectSwitcher } from '@/features/project-selection';
import { ACTIVITY_PANEL_ID } from '@/features/runs';
import { Button, CountBadge, LayoutRightLineIcon, SearchIcon } from '@mariliz/ui-kit';

import styles from './AppShell.module.scss';
import type { NavItem } from './navigation';
import { shortcutLabel } from './shortcuts/shortcuts';

export const ACTIVITY_TOGGLE_ID = 'alternar-atividade';

interface AppHeaderProps {
  onSearch: () => void;
  activity: { open: boolean; onToggle: () => void; notice?: NavItem['activity'] };
}

/** O estado do motor fica só na barra de status, para não aparecer duas vezes. */
export function AppHeader({ onSearch, activity }: AppHeaderProps) {
  return (
    <header className={styles.topbar}>
      <ProjectSwitcher />
      <Button
        variant="outlined"
        color="inherit"
        size="small"
        className={styles.search}
        startIcon={<SearchIcon fontSize="small" />}
        onClick={onSearch}
      >
        <span className={styles.searchText}>Buscar ou pedir algo</span>
        <kbd className={styles.shortcut}>{shortcutLabel('palette')}</kbd>
      </Button>
      <Button
        id={ACTIVITY_TOGGLE_ID}
        variant="text"
        color="inherit"
        size="small"
        className={styles.activityToggle}
        startIcon={<LayoutRightLineIcon fontSize="small" />}
        aria-expanded={activity.open}
        aria-controls={activity.open ? ACTIVITY_PANEL_ID : undefined}
        onClick={activity.onToggle}
      >
        Atividade
        {activity.notice ? (
          <CountBadge count={activity.notice.text} label={activity.notice.label} tone={activity.notice.tone} />
        ) : null}
      </Button>
    </header>
  );
}
