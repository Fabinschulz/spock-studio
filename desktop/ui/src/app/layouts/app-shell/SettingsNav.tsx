import { cn } from '@/shared/utils';
import { ArrowBackIcon } from '@mariliz/ui-kit';
import { NavLink } from 'react-router';
import styles from './AppShell.module.scss';
import { ENVIRONMENTS, type Environment } from './environment';
import { NavGroupList } from './NavGroupList';
import { SETTINGS_GROUP } from './navigation';

export function SettingsNav({ environment }: { environment: Environment }) {
  const back = ENVIRONMENTS.find((entry) => entry.id === environment) ?? ENVIRONMENTS[0]!;

  return (
    <nav aria-label="Configurações" className={styles.groups}>
      <NavLink to={back.to} className={cn(styles.item, styles.back)}>
        <span className={styles.itemIcon} aria-hidden="true">
          <ArrowBackIcon fontSize="small" />
        </span>
        <span className={styles.itemLabel}>Voltar para {back.label}</span>
      </NavLink>
      <NavGroupList group={SETTINGS_GROUP} />
    </nav>
  );
}
