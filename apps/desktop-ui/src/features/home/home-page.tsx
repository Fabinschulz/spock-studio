import { ProjectSelection, type ProjectPickerPort } from '@/features/project-selection';

import { Hero } from './hero';
import styles from './home-page.module.scss';
import { JourneyGrid, type JourneyId } from './journey-grid';

interface HomePageProps {
  picker?: ProjectPickerPort;
  unavailableReason?: string;
  onStartJourney?: (id: JourneyId) => void;
}

export function HomePage({ picker, unavailableReason, onStartJourney }: HomePageProps) {
  return (
    <div className={styles.page}>
      <Hero />
      <JourneyGrid onStartJourney={onStartJourney} />
      <ProjectSelection picker={picker} unavailableReason={unavailableReason} />
    </div>
  );
}
