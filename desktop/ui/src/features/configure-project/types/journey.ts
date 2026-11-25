import type { ConfigurationPlan, ConflictResolution, Discovery, RunResult, TimelineEvent } from '@/shared/schema';
import type { ComponentRequest } from '../domain/component-request';
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
  /** C3: as IDEs escolhidas, na ordem de `AI_ENVIRONMENTS`. */
  aiEnvironments: string[];
  allowSpecificationReinstall: boolean;
  updateSpecification: boolean;
  /** A escolha da etapa 2 quando a estrutura de especificação não está instalada: o comando vem marcado na revisão. */
  installSpecification: boolean;
  componentRequest: ComponentRequest;
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
  | { type: 'environment.toggle'; id: string }
  | { type: 'reinstall.set'; value: boolean }
  | { type: 'update.set'; value: boolean }
  | { type: 'install.set'; value: boolean }
  | { type: 'request.clear' }
  | { type: 'plan.start' }
  | { type: 'plan.ok'; plan: ConfigurationPlan }
  | { type: 'plan.error'; message: string }
  | { type: 'change.toggle'; id: string }
  | { type: 'command.toggle'; id: string }
  | { type: 'resolution.set'; id: string; value: ConflictResolution }
  | { type: 'step.go'; step: JourneyStepId }
  | { type: 'run.start'; kind: RunKind; at?: number; planned?: readonly PlannedStep[] }
  | { type: 'run.id'; runId: string }
  | { type: 'run.event'; event: TimelineEvent; at?: number }
  | { type: 'run.done'; result: RunResult }
  | { type: 'run.error'; message: string };

export interface StepIssue {
  stepId: JourneyStepId;
  kind: 'error' | 'blocked';
}
