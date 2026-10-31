import type { ConfigurationPlan } from '@/shared/schema';
import type { JourneyStepId, Loadable } from './journey';

export type SummaryFact = readonly [label: string, value: string];

export interface SummaryInput {
  step: JourneyStepId;
  projectName: string;
  aiEnvironment: string | null;
  plan: Loadable<ConfigurationPlan>;
  approvalCount: number;
}
