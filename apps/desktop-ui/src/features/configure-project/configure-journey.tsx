import { useNavigate } from 'react-router';

import { useProjectSelectionContext } from '@/features/project-selection';
import { useScrollToTop } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';

import { ContextPanel } from './components/context-panel';
import { JourneyFrame } from './components/journey-frame';
import { countApprovals, stepIssue } from './domain/journey-state';
import { useConfigureJourney } from './hooks/use-configure-journey';
import type { HarnessApi } from './service';
import { ModelStep } from './steps/model-step';
import { PlanStep } from './steps/plan-step';
import { ProjectStep } from './steps/project-step';
import { ResultStep } from './steps/result-step';
import { ReviewStep } from './steps/review-step';
import { RunStep } from './steps/run-step';

interface ConfigureJourneyProps {
  api: HarnessApi;
  projectId: string;
  projectName: string;
  projectPath: string;
  pollMs?: number;
}

export function ConfigureJourney({ api, projectId, projectName, projectPath, pollMs }: ConfigureJourneyProps) {
  const navigate = useNavigate();
  const { busy, error, cancelled, recents, pick, openRecent, unavailableReason } = useProjectSelectionContext();
  const { state, actions } = useConfigureJourney(api, projectId, pollMs);
  const home = () => navigate(pathTo('home'));

  useScrollToTop(state.step);

  const approvalCount = state.plan.status === 'ready' ? countApprovals(state, state.plan.data) : 0;
  const live = state.run !== null && state.run.error === null;

  return (
    <JourneyFrame
      step={state.step}
      issue={stepIssue(state)}
      live={live}
      panel={
        <ContextPanel
          step={state.step}
          projectName={projectName}
          aiEnvironment={state.aiEnvironment}
          plan={state.plan}
          approvalCount={approvalCount}
          run={state.run}
          results={state.results}
        />
      }
    >
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
          aiEnvironment={state.aiEnvironment}
          onEnvironment={actions.setEnvironment}
          onAnalyze={() => void actions.discover()}
          onContinue={() => actions.goTo('model')}
        />
      ) : null}

      {state.step === 'model' && state.discovery.status === 'ready' ? (
        <ModelStep
          discovery={state.discovery.data}
          aiEnvironment={state.aiEnvironment}
          allowReinstall={state.allowSpecificationReinstall}
          updateSpecification={state.updateSpecification}
          plan={state.plan}
          onEnvironment={actions.setEnvironment}
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
    </JourneyFrame>
  );
}
