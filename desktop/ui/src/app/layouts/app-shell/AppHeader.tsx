import { ProjectSwitcher } from '@/features/project-selection';

import styles from './AppShell.module.scss';
import type { Environment } from './environment';
import { EnvironmentSwitch } from './EnvironmentSwitch';

/** O estado do motor fica só no rodapé do menu, para não aparecer duas vezes. */
export function AppHeader({ environment }: { environment: Environment }) {
  return (
    <header className={styles.topbar}>
      <ProjectSwitcher />
      <EnvironmentSwitch environment={environment} />
    </header>
  );
}
