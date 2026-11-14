import { ProjectSelection, RecentShortcuts, useProjectSelectionContext } from '@/features/project-selection';
import { pathTo } from '@/shared/routing';
import { useNavigate } from 'react-router';
import { Hero } from './hero';
import styles from './home-page.module.scss';
import { JourneyGrid, type JourneyId } from './journey-grid';

// Conexões são do computador e o portfólio cobre todos os projetos: as duas abrem sem exigir uma pasta.
const WITHOUT_PROJECT: Partial<Record<JourneyId, Parameters<typeof pathTo>[0]>> = {
  'connect-tools': 'integrations',
  'follow-projects': 'portfolio'
};

export function HomePage() {
  const navigate = useNavigate();
  const { selected, pick } = useProjectSelectionContext();

  const startJourney = async (id: JourneyId) => {
    const route = WITHOUT_PROJECT[id];
    if (route) {
      navigate(pathTo(route));
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
