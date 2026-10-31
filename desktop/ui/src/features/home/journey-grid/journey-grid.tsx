import { ActionCard, FolderIcon } from '@/shared/components/ui';
import { BookUploadIcon, UpdateIcon, UserConnectionsIcon } from '@synthra.io/ui-kit';
import type { ReactNode } from 'react';
import styles from './journey-grid.module.scss';
import { journeys, type JourneyId } from './journeys';

const icons: Record<JourneyId, ReactNode> = {
  'configure-project': <FolderIcon />,
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
            variant={journey.id === 'configure-project' ? 'primary' : 'default'}
            unavailableReason={journey.unavailableReason}
            onClick={() => onStartJourney?.(journey.id)}
          />
        </li>
      ))}
    </ul>
  );
}
