import type { EvalProgressState } from '@/prototype/types/operation';
import type { GlyphState } from '@/shared/components/ui';
import type { EvalResult } from '@/shared/schema';

export const EVAL_STATUS_TEXT: Record<EvalResult['status'], string> = {
  passed: 'aprovado',
  failed: 'reprovado',
  blocked: 'bloqueado',
  not_run: 'não executado'
};

export const EVAL_STATUS_GLYPH: Record<EvalResult['status'], GlyphState> = {
  passed: 'done',
  failed: 'failed',
  blocked: 'warning',
  not_run: 'skipped'
};

export const EVAL_PROGRESS_TEXT: Record<EvalProgressState, string> = {
  running: 'em andamento',
  ...EVAL_STATUS_TEXT
};

export const EVAL_PROGRESS_GLYPH: Record<EvalProgressState, GlyphState> = {
  running: 'running',
  ...EVAL_STATUS_GLYPH
};
