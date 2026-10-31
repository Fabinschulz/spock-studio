export type StepState = 'pending' | 'running' | 'done' | 'warning' | 'failed';
export type ActorState = 'not_started' | 'waiting' | 'working' | 'done' | 'failed';
export type EvalProgressState = 'running' | 'passed' | 'failed' | 'blocked' | 'not_run';

export interface EvalProgress {
  id: string;
  name: string;
  kind: string;
  state: EvalProgressState;
}

export interface OperationStep {
  id: string;
  title: string;
  state: StepState;
  detail: string | null;
  startedAt: number | null;
  endedAt: number | null;
  expected: boolean;
  files: string[];
  evals: EvalProgress[];
}

export interface Actor {
  id: string;
  name: string;
  state: ActorState;
  activity: string | null;
  source: 'engine' | 'proposed';
}

export interface OperationView {
  runState: string;
  steps: OperationStep[];
  actors: Actor[];
  finished: boolean;
  headline: string;
}

export interface ExpectedStep {
  id: string;
  title: string;
}

export interface EngineIdentity {
  id: string;
  name: string;
}

export interface OperationDraft {
  steps: Map<string, OperationStep>;
  order: string[];
  actors: Map<string, Actor>;
  runState: string;
  currentStep: string | null;
}
