import type { EvalResult, RunResult } from '@/shared/schema';

export interface OutcomeNoticeProps {
  result: RunResult;
  evals: EvalResult[];
}
