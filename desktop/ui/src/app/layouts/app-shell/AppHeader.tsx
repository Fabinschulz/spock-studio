import { ProjectSwitcher } from '@/features/project-selection';
import { Button, SearchIcon } from '@mariliz/ui-kit';

import styles from './AppShell.module.scss';
import { shortcutLabel } from './shortcuts/shortcuts';
import type { Environment } from './environment';
import { EnvironmentSwitch } from './EnvironmentSwitch';

/** O estado do motor fica só na barra de status, para não aparecer duas vezes. */
export function AppHeader({ environment, onSearch }: { environment: Environment; onSearch: () => void }) {
  return (
    <header className={styles.topbar}>
      <ProjectSwitcher />
      <EnvironmentSwitch environment={environment} />
      <Button
        variant="outlined"
        color="inherit"
        size="small"
        className={styles.search}
        startIcon={<SearchIcon fontSize="small" />}
        onClick={onSearch}
      >
        Buscar
        <kbd className={styles.shortcut}>{shortcutLabel('palette')}</kbd>
      </Button>
    </header>
  );
}
