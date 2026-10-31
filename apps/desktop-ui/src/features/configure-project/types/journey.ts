import type { ConfigurationPlan, ConflictResolution, Discovery, RunEvent, RunResult } from '@/shared/schema';

import type { PlannedStep, RunKind, RunView } from './run';

export type JourneyStepId = 'project' | 'model' | 'plan' | 'review' | 'run' | 'result';

export type Loadable<T> =
  { status: 'idle' } | { status: 'loading' } | { status: 'ready'; data: T } | { status: 'error'; message: string };

export interface JourneyResults {
  apply: RunResult | null;
  revert: RunResult | null;
  validate: RunResult | null;
}

export interface JourneyState {
  step: JourneyStepId;
  discovery: Loadable<Discovery>;
  aiEnvironment: string | null;
  allowSpecificationReinstall: boolean;
  updateSpecification: boolean;
  plan: Loadable<ConfigurationPlan>;
  approvedChanges: string[];
  approvedCommands: string[];
  resolutions: Record<string, ConflictResolution>;
  run: RunView | null;
  results: JourneyResults;
}

export type JourneyAction =
  | { type: 'discover.start' }
  | { type: 'discover.ok'; discovery: Discovery }
  | { type: 'discover.error'; message: string }
  | { type: 'environment.set'; id: string }
  | { type: 'reinstall.set'; value: boolean }
  | { type: 'update.set'; value: boolean }
  | { type: 'plan.start' }
  | { type: 'plan.ok'; plan: ConfigurationPlan }
  | { type: 'plan.error'; message: string }
  | { type: 'change.toggle'; id: string }
  | { type: 'command.toggle'; id: string }
  | { type: 'resolution.set'; id: string; value: ConflictResolution }
  | { type: 'step.go'; step: JourneyStepId }
  | { type: 'run.start'; kind: RunKind; at?: number; planned?: readonly PlannedStep[] }
  | { type: 'run.id'; runId: string }
  | { type: 'run.event'; event: RunEvent; at?: number }
  | { type: 'run.done'; result: RunResult }
  | { type: 'run.error'; message: string };

export interface StepIssue {
  stepId: JourneyStepId;
  kind: 'error' | 'blocked';
}
