export type RunKind = 'apply' | 'revert' | 'validate';

export interface PlannedStep {
  id: string;
  title: string;
}

export interface RunStepView {
  id: string;
  title: string;
  status: string;
  detail: string | null;
  startedAt: number | null;
  endedAt: number | null;
}

export interface RunFeedItem {
  at: number;
  text: string;
}

export interface RunView {
  kind: RunKind;
  runId: string | null;
  state: string;
  steps: RunStepView[];
  files: string[];
  feed: RunFeedItem[];
  startedAt: number;
  error: string | null;
}

export interface RunProgress {
  done: number;
  total: number;
}
