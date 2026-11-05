import type { GlyphState } from '@/shared/components/ui';

/** `running` é o estado entre `eval.started` e `eval.completed`; os demais são os de `EvalResult`. */
export const evalStatusGlyphs: Record<string, GlyphState> = {
  running: 'running',
  passed: 'done',
  failed: 'failed',
  blocked: 'warning',
  not_run: 'skipped'
};
