import { applyResult, plan } from '@/prototype/data/fixtures';
import { ModelStep } from '../../steps/model-step';
import { OperationStep } from '../../steps/operation-step';
import { PlanStep } from '../../steps/plan-step';
import { ProjectStep } from '../../steps/project-step';
import { ResultStep } from '../../steps/result-step';
import { ReviewStep } from '../../steps/review-step';
import type { StepBodyProps } from './types';

export function StepBody({ slug, state, actions, contract, onContract, operation, applyReplay }: StepBodyProps) {
  switch (slug) {
    case 'projeto':
      return <ProjectStep analysis={state.analysis} onAnalyze={actions.analyze} />;
    case 'modelo':
      return <ModelStep env={state.env} onEnv={actions.setEnv} planning={state.planning} />;
    case 'plano':
      return <PlanStep plan={plan} demoPlanning={contract === 'proposed'} />;
    case 'revisao':
      return (
        <ReviewStep
          plan={plan}
          approved={state.approved}
          commands={state.commands}
          resolutions={state.resolutions}
          onToggle={actions.toggleChange}
          onToggleCommand={actions.toggleCommand}
          onResolve={actions.resolve}
        />
      );
    case 'aplicacao':
      return <OperationStep view={operation} replay={applyReplay} contract={contract} onContract={onContract} />;
    case 'resultado':
      return <ResultStep result={applyResult} />;
  }
}
