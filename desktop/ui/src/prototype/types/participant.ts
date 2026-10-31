import type { Actor, OperationView } from './operation';
import type { Contract, StepSlug } from './journey';
import type { ConfigurationPlan, EvalResult } from '@/shared/schema';

export interface Participant extends Pick<Actor, 'id' | 'name' | 'state' | 'activity'> {
  note?: string;
  demo?: boolean;
}

export interface ParticipantsInput {
  slug: StepSlug;
  stepIndex: number;
  contract: Contract;
  plan: ConfigurationPlan;
  operation: OperationView;
  planning: OperationView;
  judge: EvalResult | undefined;
}
