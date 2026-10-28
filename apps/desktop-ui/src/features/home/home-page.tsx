import { useNavigate } from 'react-router';

import { ProjectSelection, useProjectSelectionContext } from '@/features/project-selection';
import { pathTo } from '@/shared/routing';

import { Hero } from './hero';
import styles from './home-page.module.scss';
import { JourneyGrid, type JourneyId } from './journey-grid';

export function HomePage() {
  const navigate = useNavigate();
  const { selected, pick } = useProjectSelectionContext();

  // "Configurar projeto" precisa de uma pasta: se ainda não há, o diálogo abre agora e a jornada começa ao escolher.
  const startJourney = async (id: JourneyId) => {
    if (id !== 'configure-project') return;
    const project = selected ?? (await pick());
    if (project) navigate(pathTo('configure'));
  };

  return (
    <div className={styles.page}>
      <Hero />
      <JourneyGrid onStartJourney={(id) => void startJourney(id)} />
      <ProjectSelection onAnalyze={() => navigate(pathTo('configure'))} />
    </div>
  );
}
