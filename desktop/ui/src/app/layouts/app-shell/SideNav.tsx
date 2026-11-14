import type { RunActivity } from '@/features/runs';
import styles from './AppShell.module.scss';
import type { Environment } from './environment';
import { NavGroupList } from './NavGroupList';
import { APP_GROUP, ENVIRONMENT_GROUP, PLATFORM_GROUP, type NavGroup, type NavItem } from './navigation';

export function SideNav({
  environment,
  runActivity = null
}: {
  environment: Environment;
  runActivity?: RunActivity | null;
}) {
  const groups = [APP_GROUP, ENVIRONMENT_GROUP[environment], withRunActivity(PLATFORM_GROUP, runActivity)];

  return (
    <nav aria-label="Navegação principal" className={styles.groups}>
      {groups.map((group) => (
        <NavGroupList key={group.id} group={group} />
      ))}
    </nav>
  );
}

/**
 * O que espera a pessoa vem antes do que só está andando: com aprovação pendente o aviso mostra esse número, em tom de
 * atenção; senão, o de execuções em andamento. O nome acessível do link diz os dois.
 */
export function describeRunActivity(activity: RunActivity | null): NavItem['activity'] {
  if (!activity) return undefined;
  const { running, awaitingApproval } = activity;
  if (running === 0 && awaitingApproval === 0) return undefined;

  const label = [
    awaitingApproval > 0 ? `${awaitingApproval} aguardando aprovação` : null,
    running > 0 ? `${running} em andamento` : null
  ]
    .filter(Boolean)
    .join(', ');
  return awaitingApproval > 0
    ? { text: String(awaitingApproval), label, tone: 'warning' }
    : { text: String(running), label, tone: 'info' };
}

function withRunActivity(group: NavGroup, runActivity: RunActivity | null): NavGroup {
  const activity = describeRunActivity(runActivity);
  if (!activity) return group;
  return { ...group, items: group.items.map((item) => (item.id === 'runs' ? { ...item, activity } : item)) };
}
