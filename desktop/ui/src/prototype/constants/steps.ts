import type { StepState } from '@/prototype/types/operation';
import type { GlyphState } from '@/shared/components/ui';

export const STEP_GLYPH: Record<StepState, GlyphState> = {
  pending: 'pending',
  running: 'running',
  done: 'done',
  warning: 'warning',
  failed: 'failed'
};
