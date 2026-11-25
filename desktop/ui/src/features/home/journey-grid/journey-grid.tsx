import { ActionCard, FolderIcon } from '@/shared/components/ui';
import {
  BoardIcon,
  BriefcaseIcon,
  ChartBarIcon,
  GridIcon,
  ListViewRectangleIcon,
  UserConnectionsIcon
} from '@mariliz/ui-kit';
import { useId, type ReactNode } from 'react';
import styles from './journey-grid.module.scss';
import type { Journey, JourneyId } from './journeys';

const icons: Record<JourneyId, ReactNode> = {
  'open-project': <FolderIcon />,
  'project-overview': <BriefcaseIcon />,
  'configure-project': <ListViewRectangleIcon />,
  'follow-delivery': <ChartBarIcon />,
  'follow-projects': <GridIcon />,
  idea: <BoardIcon />,
  'connect-tools': <UserConnectionsIcon />
};

interface JourneyGridProps {
  journeys: readonly Journey[];
  onStartJourney: (journey: Journey) => void;
}

export function JourneyGrid({ journeys, onStartJourney }: JourneyGridProps) {
  const titleId = useId();

  return (
    <section className={styles.section} aria-labelledby={titleId}>
      <h2 id={titleId} className={styles.title}>
        Sugestões
      </h2>
      <ul className={styles.grid} role="list">
        {journeys.map((journey) => (
          <li key={journey.id} className={styles.item}>
            <ActionCard
              title={journey.title}
              description={journey.description}
              icon={icons[journey.id]}
              variant={journey.primary ? 'primary' : 'default'}
              onClick={() => onStartJourney(journey)}
            />
          </li>
        ))}
      </ul>
    </section>
  );
}
