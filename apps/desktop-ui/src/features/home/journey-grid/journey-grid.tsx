import { BookUploadIcon, ListViewRectangleIcon, UpdateIcon, UserConnectionsIcon } from '@synthra.io/ui-kit';
import type { ReactNode } from 'react';

import { ActionCard } from '@/shared/components/ui';

import styles from './journey-grid.module.scss';
import { journeys, type JourneyId } from './journeys';

// A Synthra não tem ícones de pasta, código ou documento (docs/experience-blueprint.md, seção 11.2): ícones provisórios.
const icons: Record<JourneyId, ReactNode> = {
  'configure-project': <ListViewRectangleIcon />,
  'import-requirements': <BookUploadIcon />,
  'connect-tools': <UserConnectionsIcon />,
  'modernize-system': <UpdateIcon />
};

interface JourneyGridProps {
  onStartJourney?: (id: JourneyId) => void;
}

export function JourneyGrid({ onStartJourney }: JourneyGridProps) {
  return (
    <ul className={styles.grid} role="list" aria-label="Atividades">
      {journeys.map((journey) => (
        <li key={journey.id} className={styles.item}>
          <ActionCard
            title={journey.title}
            description={journey.description}
            icon={icons[journey.id]}
            unavailableReason={journey.unavailableReason}
            onClick={() => onStartJourney?.(journey.id)}
          />
        </li>
      ))}
    </ul>
  );
}
