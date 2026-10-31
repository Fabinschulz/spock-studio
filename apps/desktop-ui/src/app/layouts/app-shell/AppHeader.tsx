import { app } from '@/shared/config/app';
import type { EngineStatus } from '@/shared/hooks';
import { cn } from '@/shared/utils';

import styles from './AppShell.module.scss';
import { dotClass } from './EngineStatus';
import { headerStatus } from './header-status';

interface AppHeaderProps {
  engine: { status: EngineStatus; hosted: boolean };
}

export function AppHeader({ engine }: AppHeaderProps) {
  const { label, tone } = headerStatus(engine);

  return (
    <header className={styles.topbar}>
      <p className={styles.topTagline}>{app.tagline}</p>
      <p className={styles.topStatus}>
        <span className={styles.topStatusItem}>
          <span className={cn(styles.dot, dotClass[tone])} aria-hidden="true" />
          {label}
        </span>
        <span className={styles.topVersion}>
          <span className="visually-hidden">Versão </span>v{app.version}
        </span>
      </p>
    </header>
  );
}
