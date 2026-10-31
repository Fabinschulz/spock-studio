import { useNavigate, useParams } from 'react-router';

import { JourneyBar } from '@/prototype/components/journey-bar';
import { Stage } from '@/prototype/components/stage';
import { JUDGE_EVAL_ID } from '@/prototype/constants/harness';
import { NEXT_HINT } from '@/prototype/constants/journey-steps';
import { applyResult, plan } from '@/prototype/data/fixtures';
import { countApprovals } from '@/prototype/domain/count-approvals';
import { deriveParticipants } from '@/prototype/domain/derive-participants';
import { deriveSummary } from '@/prototype/domain/derive-summary';
import { stepIndex, stepPath, stepSlugAt } from '@/prototype/domain/step-navigation';
import { useConfigureReplays } from '@/prototype/hooks/use-configure-replays';
import { useConfigureState } from '@/prototype/hooks/use-configure-state';
import { usePrototypeParams } from '@/prototype/hooks/use-prototype-params';
import type { StepSlug } from '@/prototype/types/journey';

import { ConfigureContext } from './components/configure-context';
import { StepActions } from './components/step-actions';
import { StepBody } from './components/step-body';

export function ConfigureScreen() {
  const { step } = useParams();
  const navigate = useNavigate();
  const { contract, fixedAt, forced, keepSearch, setContract } = usePrototypeParams();

  const index = stepIndex(step);
  const slug = stepSlugAt(index);
  const go = (to: StepSlug) => navigate(stepPath(to, keepSearch));

  const [state, actions] = useConfigureState({ stepIndex: index, forced, go });
  const { applyReplay, operation, planning, feed, live } = useConfigureReplays({ slug, contract, fixedAt });

  const approvedCount = countApprovals(state);
  const judge = applyResult.validation?.results.find((r) => r.id === JUDGE_EVAL_ID);
  const participants = deriveParticipants({ slug, stepIndex: index, contract, plan, operation, planning, judge });
  const summary = deriveSummary({
    env: state.env,
    stepIndex: index,
    changeCount: plan.changes.length,
    approvedCount
  });
  const showActivity = slug === 'aplicacao' || (slug === 'plano' && contract === 'proposed');

  return (
    <Stage
      bar={<JourneyBar current={index} onGo={go} />}
      panelTitle="Contexto"
      live={live}
      panel={
        <ConfigureContext
          summary={summary}
          participants={participants}
          feed={feed}
          showActivity={showActivity}
          live={live}
        />
      }
      actions={
        <StepActions
          slug={slug}
          state={state}
          actions={actions}
          approvedCount={approvedCount}
          operationFinished={operation.finished}
          go={go}
          onConclude={() => navigate('/projeto')}
        />
      }
      actionsNote={NEXT_HINT[slug]}
    >
      <StepBody
        slug={slug}
        state={state}
        actions={actions}
        contract={contract}
        onContract={setContract}
        operation={operation}
        applyReplay={applyReplay}
      />
    </Stage>
  );
}
