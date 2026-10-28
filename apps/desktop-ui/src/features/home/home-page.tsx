import { useState } from 'react';
import { useNavigate } from 'react-router';

import { ProjectSelection, useProjectSelectionContext } from '@/features/project-selection';
import { InlineNotice } from '@/shared/components/ui';
import { pathTo } from '@/shared/routing';

import { Hero } from './hero';
import styles from './home-page.module.scss';
import { JourneyGrid, journeys, type JourneyId } from './journey-grid';

export function HomePage() {
  const navigate = useNavigate();
  const { selected, pick } = useProjectSelectionContext();
  const [noticeDismissed, setNoticeDismissed] = useState(false);

  // "Configurar projeto" precisa de uma pasta: se ainda não há, o diálogo abre agora e a jornada começa ao escolher.
  const startJourney = async (id: JourneyId) => {
    if (id !== 'configure-project') return;
    const project = selected ?? (await pick());
    if (project) navigate(pathTo('configure'));
  };

  // O aviso lista só o que de fato está indisponível, pela mesma fonte dos cartões.
  const soon = journeys.filter((j) => j.unavailableReason).map((j) => j.title);

  return (
    <div className={styles.page}>
      <Hero />
      <JourneyGrid onStartJourney={(id) => void startJourney(id)} />
      {noticeDismissed || soon.length === 0 ? null : (
        <InlineNotice tone="info" title="Funcionalidades em breve" onDismiss={() => setNoticeDismissed(true)}>
          As opções {soon.slice(0, -1).join(', ')} e {soon[soon.length - 1]} ainda estão em desenvolvimento.
        </InlineNotice>
      )}
      <ProjectSelection />
    </div>
  );
}
