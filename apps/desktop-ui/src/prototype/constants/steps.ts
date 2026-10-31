import type { GlyphState } from '@/shared/components/ui';

import type { StepState } from '@/prototype/types/operation';

export const STEP_GLYPH: Record<StepState, GlyphState> = {
  pending: 'pending',
  running: 'running',
  done: 'done',
  warning: 'warning',
  failed: 'failed'
};
