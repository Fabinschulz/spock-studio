import { useProjectSelectionContext } from '@/features/project-selection';
import { paths } from '@/shared/routing';
import { cn } from '@/shared/utils';
import { Link } from 'react-router';
import styles from './AppShell.module.scss';
import { ENVIRONMENTS, type Environment, type EnvironmentEntry } from './environment';

// Sem projeto aberto, o Acompanhamento não tem o que mostrar; o Portfólio cobre todos os projetos e sempre tem.
function destination(entry: EnvironmentEntry, hasProject: boolean): string {
  return entry.id === 'management' && !hasProject ? paths.portfolio : entry.to;
}

/** São links porque trocar leva a outra tela; o ambiente atual vem marcado por `aria-current` e por forma, não só por cor. */
export function EnvironmentSwitch({ environment }: { environment: Environment }) {
  const { selected } = useProjectSelectionContext();

  return (
    <nav aria-label="Ambiente" className={styles.environments}>
      {ENVIRONMENTS.map((entry) => {
        const active = entry.id === environment;
        return (
          <Link
            key={entry.id}
            to={destination(entry, selected !== null)}
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
