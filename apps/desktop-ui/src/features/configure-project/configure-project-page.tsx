import { Alert, Button } from '@synthra.io/ui-kit';
import { useMemo } from 'react';
import { useNavigate } from 'react-router';

import { useProjectSelectionContext } from '@/features/project-selection';
import { WizardSteps } from '@/shared/components/ui';
import { pathTo } from '@/shared/routing';
import { createHarnessApi, tauriEngineClient, type HarnessApi } from '@/shared/services';

import styles from './configure-project-page.module.scss';
import { countApprovals, journeySteps } from './core/journey-state';
import { useConfigureJourney } from './hooks/use-configure-journey';
import { MethodStep } from './steps/method-step';
import { PlanStep } from './steps/plan-step';
import { ProjectStep } from './steps/project-step';
import { ResultStep } from './steps/result-step';
import { ReviewStep } from './steps/review-step';
import { RunStep } from './steps/run-step';

interface ConfigureProjectPageProps {
  /** Padrão: o motor real, pelo host Tauri. Os testes injetam um dublê. */
  api?: HarnessApi;
  pollMs?: number;
}

export function ConfigureProjectPage({ api, pollMs }: ConfigureProjectPageProps) {
  const { selected } = useProjectSelectionContext();
  const navigate = useNavigate();
  const resolved = useMemo(() => api ?? createHarnessApi(tauriEngineClient), [api]);

  if (!selected) {
    return (
      <div className={styles.page}>
        <Alert
          severity="info"
          title="Selecione um projeto"
          description="A configuração começa por uma pasta de projeto. Volte ao início e escolha a pasta."
        />
        <div>
          <Button variant="contained" onClick={() => navigate(pathTo('home'))}>
            Voltar ao início
          </Button>
        </div>
      </div>
    );
  }

  return (
    <Journey
      key={selected.projectId}
      api={resolved}
      projectId={selected.projectId}
      projectName={selected.displayName}
      pollMs={pollMs}
    />
  );
}

interface JourneyProps {
  api: HarnessApi;
  projectId: string;
  projectName: string;
  pollMs?: number;
}

function Journey({ api, projectId, projectName, pollMs }: JourneyProps) {
  const navigate = useNavigate();
  const { state, actions } = useConfigureJourney(api, projectId, pollMs);
  const home = () => navigate(pathTo('home'));

  return (
    <div className={styles.page}>
      <WizardSteps steps={journeySteps} currentId={state.step} />

      {state.step === 'project' ? (
        <ProjectStep
          projectName={projectName}
          discovery={state.discovery}
          onRetry={() => void actions.discover()}
          onContinue={() => actions.goTo('method')}
        />
      ) : null}

      {state.step === 'method' && state.discovery.status === 'ready' ? (
        <MethodStep
          discovery={state.discovery.data}
          aiEnvironment={state.aiEnvironment}
          allowReinstall={state.allowTlcReinstall}
          plan={state.plan}
          onEnvironment={actions.setEnvironment}
          onReinstall={actions.setReinstall}
          onBack={() => actions.goTo('project')}
          onPlan={() => void actions.generatePlan()}
        />
      ) : null}

      {state.step === 'plan' && state.plan.status === 'ready' ? (
        <PlanStep
          plan={state.plan.data}
          onBack={() => actions.goTo('method')}
          onContinue={() => actions.goTo('review')}
        />
      ) : null}

      {state.step === 'review' && state.plan.status === 'ready' ? (
        <ReviewStep
          plan={state.plan.data}
          approvedChanges={state.approvedChanges}
          approvedCommands={state.approvedCommands}
          resolutions={state.resolutions}
          approvalCount={countApprovals(state, state.plan.data)}
          onToggleChange={actions.toggleChange}
          onToggleCommand={actions.toggleCommand}
          onResolution={actions.setResolution}
          onBack={() => actions.goTo('plan')}
          onCancel={home}
          onApply={() => void actions.apply()}
        />
      ) : null}

      {state.step === 'run' && state.run ? (
        <RunStep run={state.run} onCancel={actions.cancelRun} onBack={() => actions.goTo('review')} />
      ) : null}

      {state.step === 'result' ? (
        <ResultStep
          results={state.results}
          run={state.run}
          onRevert={(runId) => void actions.revert(runId)}
          onValidate={() => void actions.validate()}
          onDone={home}
        />
      ) : null}
    </div>
  );
}
