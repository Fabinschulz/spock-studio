import type { RunStepView, RunView } from './journey-state';

const finished = (step: RunStepView) => step.status !== 'pending' && step.status !== 'running';

export const runningStep = (run: RunView): RunStepView | null => run.steps.find((s) => s.status === 'running') ?? null;

/** Etapas concluídas e total conhecido (as planejadas mais as que o motor acrescentou). */
export function stepProgress(run: RunView): { done: number; total: number } {
  return { done: run.steps.filter(finished).length, total: run.steps.length };
}

export const formatElapsed = (ms: number): string => {
  const total = Math.floor(ms / 1000);
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`;
};
