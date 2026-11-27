import { useAgentInstalled } from '@/features/agents';
import { RequestComposer, type AskKind, type ComposerRequest, type ComposerShortcut } from '@/features/ask';
import { BootResultCard, createBootApi, useBoot } from '@/features/boot';
import { RecentShortcuts, useProjectSelectionContext } from '@/features/project-selection';
import { isTauriRuntime, type EngineClient } from '@/shared/api';
import { handoffState } from '@/shared/routing';
import { InlineNotice } from '@/shared/components';
import { useEngineStatus } from '@/shared/hooks';
import { readPreference, writePreference } from '@/shared/utils';
import { Alert, Button } from '@mariliz/ui-kit';
import { useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router';

import { Hero } from './hero';
import styles from './home-page.module.scss';
import { JourneyGrid, journeysFor, type Journey } from './journey-grid';
import { RecentActivity } from './recent-activity';
import { countDone, SETUP_STEPS, SetupChecklist, useSetupStatus } from './setup-checklist';

const SETUP_KEY = 'spock.setup';
// O tipo de atividade que a pessoa marcou de propósito vira a capacidade do Spock direto, sem interpretar o texto.
const CAPABILITY_BY_KIND: Record<AskKind, string> = {
  request: 'engineering.request',
  agent: 'engineering.code_question',
  assistant: 'management.assistant',
  idea: 'ideas.idea_to_project'
};
const PROJECT_JOURNEY = 'project-overview';
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
  const agentInstalled = useAgentInstalled(client, engineReady);
  const [setupHidden, setSetupHidden] = useState(() => readPreference(SETUP_KEY) === HIDDEN);
  const bootApi = useMemo(() => (engineReady ? createBootApi(client) : null), [client, engineReady]);
  const boot = useBoot(bootApi);
  const lastProject = useRef<string | null>(null);

  const showSetup = (visible: boolean) => {
    setSetupHidden(!visible);
    writePreference(SETUP_KEY, visible ? SHOWN : HIDDEN);
  };

  const startJourney = async (journey: Journey) => {
    const project = journey.needsProject ? (selected ?? (await pick())) : selected;
    if (journey.needsProject && !project) return;
    // "Analisar o projeto" é o Project Boot: reúne o contexto e diz o que está pronto. Se o motor não responder, abre a tela.
    if (journey.id === PROJECT_JOURNEY && project) {
      if (boot.isRunning()) return;
      lastProject.current = project.projectId;
      if (await boot.project(project.projectId)) return;
    }
    navigate(journey.to);
  };

  const prepareRequest = async (request: ComposerRequest) => {
    // Já há um pedido em preparo: este é descartado, e a tela não navega no lugar do primeiro.
    if (boot.isRunning()) return true;
    lastProject.current = request.projectId;
    const record = await boot.request({
      projectId: request.projectId,
      text: request.text,
      ...(request.explicit ? { capabilityId: CAPABILITY_BY_KIND[request.kind] } : { hint: request.kind })
    });
    return record !== null;
  };

  const chooseCapability = (capabilityId: string) => {
    if (boot.state.status !== 'ready') return;
    void boot.request({ projectId: lastProject.current ?? undefined, text: boot.state.text, capabilityId });
  };

  const pickAndConfigure = async () => {
    if (await pick()) navigate(SETUP_STEPS[0]!.to);
  };

  const setupPending = countDone(setup) < SETUP_STEPS.length;

  return (
    <div className={styles.page}>
      <Hero />
      {error ? <Alert severity="error" title="Não foi possível abrir o projeto" description={error} /> : null}
      <RequestComposer
        findShortcuts={findShortcuts}
        onRequest={boot.available ? prepareRequest : undefined}
        busy={boot.state.status === 'running'}
        readiness={{ modelConfigured: setup.model, agentInstalled }}
      />
      {boot.state.status === 'running' ? (
        <p role="status" className={styles.restore}>
          {boot.state.label}
        </p>
      ) : null}
      {boot.state.status === 'failed' ? <InlineNotice tone="danger">{boot.state.message}</InlineNotice> : null}
      {boot.state.status === 'ready' ? (
        <BootResultCard
          record={boot.state.record}
          onOpen={(route) =>
            navigate(route, { state: handoffState(boot.state.status === 'ready' ? boot.state.text : '') })
          }
          onChoose={chooseCapability}
          onDismiss={boot.dismiss}
        />
      ) : null}
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
