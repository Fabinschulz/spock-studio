import { ProjectSelection, RecentShortcuts, useProjectSelectionContext } from '@/features/project-selection';
import { pathTo } from '@/shared/routing';
import { useNavigate } from 'react-router';
import { Hero } from './hero';
import styles from './home-page.module.scss';
import { JourneyGrid, type JourneyId } from './journey-grid';

export function HomePage() {
  const navigate = useNavigate();
  const { selected, pick } = useProjectSelectionContext();

  const startJourney = async (id: JourneyId) => {
    // As conexões são do computador, não de um projeto: a central abre sem exigir uma pasta.
    if (id === 'connect-tools') {
      navigate(pathTo('integrations'));
      return;
    }
    if (id !== 'configure-project' && id !== 'engineering-request') return;
    const project = selected ?? (await pick());
    if (!project) return;
    navigate(pathTo(id === 'engineering-request' ? 'engineeringRequest' : 'configure'));
  };

  return (
    <div className={styles.page}>
      <Hero />
      <JourneyGrid onStartJourney={(id) => void startJourney(id)} />
      <ProjectSelection />
      <RecentShortcuts />
    </div>
  );
}
