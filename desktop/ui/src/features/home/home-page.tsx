import { RequestComposer, type ComposerShortcut } from '@/features/ask';
import { RecentShortcuts, useProjectSelectionContext } from '@/features/project-selection';
import { isTauriRuntime, type EngineClient } from '@/shared/api';
import { useEngineStatus } from '@/shared/hooks';
import { readPreference, writePreference } from '@/shared/utils';
import { Alert, Button } from '@mariliz/ui-kit';
import { useState } from 'react';
import { useNavigate } from 'react-router';

import { Hero } from './hero';
import styles from './home-page.module.scss';
import { JourneyGrid, journeysFor, type Journey } from './journey-grid';
import { RecentActivity } from './recent-activity';
import { countDone, SETUP_STEPS, SetupChecklist, useSetupStatus } from './setup-checklist';

const SETUP_KEY = 'spock.setup';
const HIDDEN = 'hidden';
const SHOWN = 'shown';

interface HomePageProps {
  client: EngineClient;
  /** Telas que combinam com o texto da entrada de pedidos; quem conhece o menu é a camada de cima. */
  findShortcuts?: (text: string) => readonly ComposerShortcut[];
}

export function HomePage({ client, findShortcuts }: HomePageProps) {
  const navigate = useNavigate();
  const { selected, recents, pick, error } = useProjectSelectionContext();
  const engine = useEngineStatus();
  const engineReady = isTauriRuntime() && engine.status === 'ready';
  const setup = useSetupStatus(client, selected?.projectId ?? null, engineReady);
  const [setupHidden, setSetupHidden] = useState(() => readPreference(SETUP_KEY) === HIDDEN);

  const showSetup = (visible: boolean) => {
    setSetupHidden(!visible);
    writePreference(SETUP_KEY, visible ? SHOWN : HIDDEN);
  };

  const startJourney = async (journey: Journey) => {
    if (journey.needsProject && !selected && !(await pick())) return;
    navigate(journey.to);
  };

  const pickAndConfigure = async () => {
    if (await pick()) navigate(SETUP_STEPS[0]!.to);
  };

  const setupPending = countDone(setup) < SETUP_STEPS.length;

  return (
    <div className={styles.page}>
      <Hero />
      {error ? <Alert severity="error" title="Não foi possível abrir o projeto" description={error} /> : null}
      <RequestComposer findShortcuts={findShortcuts} />
      <JourneyGrid
        journeys={journeysFor({ hasProject: selected !== null, hasRecents: recents.length > 0 })}
        onStartJourney={(journey) => void startJourney(journey)}
      />
      {setupPending && !setupHidden ? (
        <SetupChecklist
          status={setup}
          onPickProject={() => void pickAndConfigure()}
          onDismiss={() => showSetup(false)}
        />
      ) : null}
      {engineReady ? <RecentActivity client={client} /> : null}
      <RecentShortcuts />
      {setupPending && setupHidden ? (
        <p className={styles.restore}>
          <Button variant="text" color="inherit" size="small" onClick={() => showSetup(true)}>
            Mostrar os primeiros passos
          </Button>
        </p>
      ) : null}
    </div>
  );
}
