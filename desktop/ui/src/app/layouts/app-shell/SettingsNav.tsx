import { ArrowBackIcon } from '@mariliz/ui-kit';
import styles from './AppShell.module.scss';
import { ENVIRONMENTS, type Environment } from './environment';
import { NavEntry, NavGroupList } from './NavGroupList';
import { SETTINGS_GROUP } from './navigation';

export function SettingsNav({ environment }: { environment: Environment }) {
  const back = ENVIRONMENTS.find((entry) => entry.id === environment) ?? ENVIRONMENTS[0]!;

  return (
    <nav aria-label="Configurações" className={styles.groups}>
      <NavEntry
        className={styles.back}
        item={{
          id: 'back',
          label: `Voltar para ${back.label}`,
          icon: <ArrowBackIcon fontSize="small" />,
          to: back.to
        }}
      />
      <NavGroupList group={SETTINGS_GROUP} />
    </nav>
  );
}
