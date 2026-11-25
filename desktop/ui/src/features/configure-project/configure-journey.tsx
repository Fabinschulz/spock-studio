import { EngineeringRequestShortcut } from '@/features/engineering-request';
import { useScrollToTop } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import { useNavigate } from 'react-router';
import { ComponentRequestNotice } from './components/component-request-notice';
import { ContextPanel } from './components/context-panel';
import { JourneyFrame } from './components/journey-frame';
import { useJourneyStore } from './context/journey-store';
import { countApprovals } from './domain/approvals';
import { namingOf } from './domain/harness-engineer-prompt';
import { hasComponentRequest, type ComponentRequest } from './domain/component-request';
import { canReopenEarlierSteps } from './domain/step-navigation';
import { stepIssue } from './domain/step-issue';
import { useConfigureJourney } from './hooks/use-configure-journey';
import { useConfiguredProject } from './hooks/use-configured-project';
import type { HarnessApi } from './service';
import { ModelStep } from './steps/model-step';
import { PlanStep } from './steps/plan-step';
import { ProjectStep } from './steps/project-step';
import { ResultStep } from './steps/result-step';
import { ReviewStep } from './steps/review-step';
import { RunStep } from './steps/run-step';
import type { JourneyStepId } from './types/journey';

const SHORTCUT_SKIPPED: readonly JourneyStepId[] = ['model', 'plan', 'review', 'run'];

interface ConfigureJourneyProps {
  api: HarnessApi;
  projectId: string;
  projectName: string;
  projectPath: string;
  pollMs?: number;
  componentRequest?: ComponentRequest;
}

export function ConfigureJourney({
  api,
  projectId,
  projectName,
  projectPath,
  pollMs,
  componentRequest
}: ConfigureJourneyProps) {
  const navigate = useNavigate();
  const store = useJourneyStore();
  const { state, actions } = useConfigureJourney(api, projectId, pollMs, componentRequest, store);
  // Concluir ou cancelar encerra a jornada: a próxima visita ao Configurar começa limpa.
  const home = () => {
    store?.clear(projectId);
    navigate(pathTo('home'));
  };

  // Relê depois de cada execução terminada, para o resumo da segunda visita não mostrar o estado de antes.
  const configured = useConfiguredProject(api, projectId, state.results);

  useScrollToTop(state.step, { focus: true, focusSelector: '[data-step-title]' });

  const approvalCount = state.plan.status === 'ready' ? countApprovals(state, state.plan.data) : 0;
  const naming = namingOf(state.discovery);
  const live = state.run !== null && state.run.error === null;
  // Validar ou reverter pelo atalho da etapa Projeto chega ao Resultado sem passar pelo plano nem aplicar nada.
  const skipped = state.step === 'result' && state.results.apply === null ? SHORTCUT_SKIPPED : undefined;

  return (
    <JourneyFrame
      step={state.step}
      issue={stepIssue(state)}
      skipped={skipped}
      live={live}
      onSelectStep={canReopenEarlierSteps(state.step) ? actions.goTo : undefined}
      panel={
        <ContextPanel
          step={state.step}
          projectName={projectName}
          aiEnvironments={state.aiEnvironments}
          plan={state.plan}
          approvalCount={approvalCount}
          run={state.run}
          results={state.results}
          naming={naming}
        />
      }
    >
      <EngineeringRequestShortcut />
      {hasComponentRequest(state.componentRequest) && ['project', 'model', 'plan'].includes(state.step) ? (
        <ComponentRequestNotice request={state.componentRequest} onClear={actions.clearComponentRequest} />
      ) : null}
      {state.step === 'project' ? (
        <ProjectStep
          project={{ name: projectName, path: projectPath }}
          discovery={state.discovery}
          onAnalyze={() => void actions.discover()}
          onContinue={() => actions.goTo('model')}
          configured={configured}
          run={state.run}
          onValidate={() => void actions.validate()}
          onRevert={(runId) => void actions.revert(runId)}
        />
      ) : null}

      {state.step === 'model' && state.discovery.status === 'ready' ? (
        <ModelStep
          discovery={state.discovery.data}
          aiEnvironments={state.aiEnvironments}
          allowReinstall={state.allowSpecificationReinstall}
          updateSpecification={state.updateSpecification}
          plan={state.plan}
          onToggleEnvironment={actions.toggleEnvironment}
          onReinstall={actions.setReinstall}
          onUpdateSpecification={actions.setUpdateSpecification}
          onBack={() => actions.goTo('project')}
          onPlan={() => void actions.generatePlan()}
        />
      ) : null}

      {state.step === 'plan' && state.plan.status === 'ready' ? (
        <PlanStep
          plan={state.plan.data}
          onBack={() => actions.goTo('model')}
          onContinue={() => actions.goTo('review')}
        />
      ) : null}

      {state.step === 'review' && state.plan.status === 'ready' ? (
        <ReviewStep
          plan={state.plan.data}
          approvedChanges={state.approvedChanges}
          approvedCommands={state.approvedCommands}
          resolutions={state.resolutions}
          approvalCount={approvalCount}
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
          naming={naming}
          onDone={home}
        />
      ) : null}
    </JourneyFrame>
  );
}
