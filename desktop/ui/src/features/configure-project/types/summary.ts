import type { ConfigurationPlan } from '@/shared/schema';
import type { JourneyStepId, Loadable } from './journey';

export type SummaryFact = readonly [label: string, value: string];

export interface SummaryInput {
  step: JourneyStepId;
  projectName: string;
  aiEnvironments: readonly string[];
  plan: Loadable<ConfigurationPlan>;
  approvalCount: number;
}
