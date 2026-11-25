import { useProjectSelectionContext } from '@/features/project-selection';
import { CountBadge, Tooltip } from '@mariliz/ui-kit';
import { Link, useLocation } from 'react-router';
import styles from './AppShell.module.scss';
import { areaDestination, SETTINGS_AREA, WORK_AREAS, type AreaId, type NavArea, type NavItem } from './navigation';

interface ActivityBarProps {
  current: AreaId | null;
  /** Vai na área Atividade: o que espera a pessoa aparece de qualquer tela. */
  activity?: NavItem['activity'];
  /** Janela estreita: os nomes saem da tela e viram dica ao lado, sem deixar de ser o nome acessível. */
  compact: boolean;
}

/** As áreas do aplicativo. Cada uma leva à tela inicial dela; as telas da área aparecem no painel ao lado. */
export function ActivityBar({ current, activity, compact }: ActivityBarProps) {
  const entry = (area: NavArea) => (
    <li key={area.id}>
      <AreaLink
        area={area}
        active={area.id === current}
        compact={compact}
        activity={area.id === 'activity' ? activity : undefined}
      />
    </li>
  );

  return (
    <nav aria-label="Navegação principal" className={styles.areasNav}>
      <ul className={styles.areas} role="list">
        {WORK_AREAS.map(entry)}
      </ul>
      <ul className={styles.areas} role="list">
        {entry(SETTINGS_AREA)}
      </ul>
    </nav>
  );
}

function AreaLink({
  area,
  active,
  compact,
  activity
}: {
  area: NavArea;
  active: boolean;
  compact: boolean;
  activity?: NavItem['activity'];
}) {
  const { selected } = useProjectSelectionContext();
  const { pathname } = useLocation();
  const to = areaDestination(area, selected !== null);

  return (
    <Tooltip title={area.label} placement="right" disabled={!compact}>
      <Link
        to={to}
        className={styles.area}
        // Dentro da área mas noutra tela dela, o link marca o conjunto, não a página.
        aria-current={active ? (pathname === to ? 'page' : 'true') : undefined}
      >
        <span className={styles.areaIcon} aria-hidden="true">
          {area.icon}
        </span>
        <span className={styles.areaLabel}>{area.label}</span>
        {activity ? (
          <CountBadge className={styles.areaBadge} count={activity.text} label={activity.label} tone={activity.tone} />
        ) : null}
      </Link>
    </Tooltip>
  );
}
