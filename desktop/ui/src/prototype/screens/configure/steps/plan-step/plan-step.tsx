import { StepHeader } from '@/prototype/components/step-header';
import { NO_PROPOSAL_CODE } from '@/prototype/constants/harness';

import { DecisionsSection } from './components/decisions-section';
import { ProposalNotice } from './components/proposal-notice';
import { RisksSection } from './components/risks-section';
import type { PlanStepProps } from './types';

export function PlanStep({ plan, demoPlanning }: PlanStepProps) {
  const noProposal = plan.limitations.some((l) => l.code === NO_PROPOSAL_CODE);

  return (
    <>
      <StepHeader
        title="Composição do Harness"
        lead="Cada componente tem uma decisão, a origem dela e a justificativa. O plano é uma proposta: nada é aplicado antes da revisão."
      />
      {noProposal ? <ProposalNotice demoPlanning={demoPlanning} /> : null}
      <DecisionsSection decisions={plan.decisions} />
      <RisksSection risks={plan.risks} />
    </>
  );
}
