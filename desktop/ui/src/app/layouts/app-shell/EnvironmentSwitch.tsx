import { cn } from '@/shared/utils';
import { Link } from 'react-router';
import styles from './AppShell.module.scss';
import { ENVIRONMENTS, type Environment } from './environment';

/** São links porque trocar leva a outra tela; o ambiente atual vem marcado por `aria-current` e por forma, não só por cor. */
export function EnvironmentSwitch({ environment }: { environment: Environment }) {
  return (
    <nav aria-label="Ambiente" className={styles.environments}>
      {ENVIRONMENTS.map((entry) => {
        const active = entry.id === environment;
        return (
          <Link
            key={entry.id}
            to={entry.to}
            className={cn(styles.environment, active && styles.environmentActive)}
            aria-current={active ? 'true' : undefined}
          >
            {entry.label}
          </Link>
        );
      })}
    </nav>
  );
}
