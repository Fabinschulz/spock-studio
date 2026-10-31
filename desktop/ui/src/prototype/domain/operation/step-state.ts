import type { StepState } from '@/prototype/types/operation';

export const stepStateFromStatus = (status: string | null): StepState =>
  status === 'completed' ? 'done' : status === 'failed' ? 'failed' : status ? 'warning' : 'done';
