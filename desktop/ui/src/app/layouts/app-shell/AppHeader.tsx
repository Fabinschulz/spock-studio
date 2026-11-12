import { ProjectSwitcher } from '@/features/project-selection';

import styles from './AppShell.module.scss';
import type { Environment } from './environment';
import { EnvironmentSwitch } from './EnvironmentSwitch';

/**
 * O que a pessoa precisa saber em qualquer tela: qual projeto está aberto e em qual ambiente está. O estado do motor
 * fica só no rodapé do menu, para não aparecer duas vezes.
 */
export function AppHeader({ environment }: { environment: Environment }) {
  return (
    <header className={styles.topbar}>
      <ProjectSwitcher />
      <EnvironmentSwitch environment={environment} />
    </header>
  );
}
