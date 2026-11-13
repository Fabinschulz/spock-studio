import type { RunSummary } from '@/shared/schema';
import { runEffectLabel, runStateLabel } from '@/shared/utils';

export function runRowText(run: RunSummary): string {
  const effect = runEffectLabel(run);
  return [runStateLabel(run.state), effect].filter(Boolean).join(' · ');
}
