import type { GlyphState } from '@/shared/components/ui';

export const stepStatusGlyphs: Record<string, GlyphState> = {
  pending: 'pending',
  running: 'running',
  completed: 'done',
  failed: 'failed'
};
