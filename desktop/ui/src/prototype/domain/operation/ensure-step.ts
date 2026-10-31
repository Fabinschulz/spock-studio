import type { OperationDraft, OperationStep } from '@/prototype/types/operation';

export function ensureStep(draft: OperationDraft, id: string, title: string, expected: boolean): OperationStep {
  const existing = draft.steps.get(id);
  if (existing) return existing;

  const step: OperationStep = {
    id,
    title,
    state: 'pending',
    detail: null,
    startedAt: null,
    endedAt: null,
    expected,
    files: [],
    evals: []
  };
  draft.steps.set(id, step);
  draft.order.push(id);
  return step;
}
