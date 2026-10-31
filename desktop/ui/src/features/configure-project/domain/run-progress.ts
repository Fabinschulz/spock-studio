import type { RunProgress, RunStepView, RunView } from '../types/run';

const isFinished = (step: RunStepView) => step.status !== 'pending' && step.status !== 'running';

export const runningStep = (run: RunView): RunStepView | null => run.steps.find((s) => s.status === 'running') ?? null;

export function stepProgress(run: RunView): RunProgress {
  return { done: run.steps.filter(isFinished).length, total: run.steps.length };
}
