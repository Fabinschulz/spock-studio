import { useEffect, useMemo, type ReactNode } from 'react';
import { useNavigate } from 'react-router';

import { useProjectSelectionContext } from '@/features/project-selection';
import { PageHeader, WizardSteps, type StepIssue } from '@/shared/components/ui';
import { pathTo } from '@/shared/routing';
import { createHarnessApi, tauriEngineClient, type HarnessApi } from '@/shared/services';

import styles from './configure-project-page.module.scss';
import { countApprovals, journeySteps, stepIssue, type JourneyStepId } from './core/journey-state';
import { useConfigureJourney } from './hooks/use-configure-journey';
import { MethodStep } from './steps/method-step';
import { PlanStep } from './steps/plan-step';
import { ProjectStep } from './steps/project-step';
import { ResultStep } from './steps/result-step';
import { ReviewStep } from './steps/review-step';
import { RunStep } from './steps/run-step';
import { MethodBadge } from './ui/method-badge';

interface ConfigureProjectPageProps {
  /** Padrão: o motor real, pelo host Tauri. Os testes injetam um dublê. */
  api?: HarnessApi;
  pollMs?: number;
}

export function ConfigureProjectPage({ api, pollMs }: ConfigureProjectPageProps) {
  const { selected } = useProjectSelectionContext();
  const resolved = useMemo(() => api ?? createHarnessApi(tauriEngineClient), [api]);

  // Sem pasta, a jornada ainda não existe: só a primeira etapa, para escolher uma (ou reabrir uma recente).
  if (!selected) {
    return (
      <Frame step="project">
        <ProjectChoice />
      </Frame>
    );
  }

  return (
    <Journey
      key={selected.projectId}
      api={resolved}
      projectId={selected.projectId}
      projectName={selected.displayName}
      projectPath={selected.displayPath}
      pollMs={pollMs}
    />
  );
}

/** Cabeçalho da jornada: trilha, progresso, título e o método. Igual em todas as etapas. */
function Frame({ step, issue, children }: { step: JourneyStepId; issue?: StepIssue; children: ReactNode }) {
  return (
    <div className={styles.page}>
      <PageHeader
        breadcrumb={[{ label: 'Início', to: pathTo('home') }, { label: 'Configurar projeto' }]}
        lead={<WizardSteps steps={journeySteps} currentId={step} issue={issue} />}
        title="Configurar projeto"
        actions={<MethodBadge />}
        description="Vamos analisar o seu projeto e preparar o ambiente de engenharia com o TLC."
      />
      {children}
    </div>
  );
}

/** A primeira etapa quando ainda não há pasta escolhida. */
function ProjectChoice() {
  const { busy, error, cancelled, recents, pick, openRecent, unavailableReason } = useProjectSelectionContext();
  return (
    <ProjectStep
      project={null}
      discovery={{ status: 'idle' }}
      recents={recents}
      busy={busy}
      error={error}
      cancelled={cancelled}
      unavailableReason={unavailableReason}
      onPick={() => void pick()}
      onOpenRecent={(id) => void openRecent(id)}
      onRetry={() => undefined}
      onContinue={() => undefined}
    />
  );
}

interface JourneyProps {
  api: HarnessApi;
  projectId: string;
  projectName: string;
  projectPath: string;
  pollMs?: number;
}

function Journey({ api, projectId, projectName, projectPath, pollMs }: JourneyProps) {
  const navigate = useNavigate();
  const { busy, error, cancelled, recents, pick, openRecent, unavailableReason } = useProjectSelectionContext();
  const { state, actions } = useConfigureJourney(api, projectId, pollMs);
  const home = () => navigate(pathTo('home'));

  // Cada etapa abre no topo: sem isso, a tela nova herdava a rolagem da anterior (visto no aplicativo instalado).
  useEffect(() => {
    (document.scrollingElement ?? document.documentElement).scrollTo?.({ top: 0 });
  }, [state.step]);

  return (
    <Frame step={state.step} issue={stepIssue(state)}>
      {state.step === 'project' ? (
        <ProjectStep
          project={{ name: projectName, path: projectPath }}
          discovery={state.discovery}
          recents={recents}
          busy={busy}
          error={error}
          cancelled={cancelled}
          unavailableReason={unavailableReason}
          onPick={() => void pick()}
          onOpenRecent={(id) => void openRecent(id)}
          onRetry={() => void actions.discover()}
          onContinue={() => actions.goTo('method')}
        />
      ) : null}

      {state.step === 'method' && state.discovery.status === 'ready' ? (
        <MethodStep
          discovery={state.discovery.data}
          aiEnvironment={state.aiEnvironment}
          allowReinstall={state.allowTlcReinstall}
          updateTlc={state.updateTlc}
          plan={state.plan}
          onEnvironment={actions.setEnvironment}
          onReinstall={actions.setReinstall}
          onUpdateTlc={actions.setUpdateTlc}
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
          onReplan={() => void actions.generatePlan()}
          replan={state.plan}
          onDone={home}
        />
      ) : null}
    </Frame>
  );
}
