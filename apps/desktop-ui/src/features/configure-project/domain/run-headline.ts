import type { GlyphState } from '@/shared/components/ui';

import { stepStatusGlyphs } from '../constants/step-status-glyphs';
import type { RunView } from '../types/run';
import { runningStep, stepProgress } from './run-progress';

export const glyphOfStatus = (status: string): GlyphState => stepStatusGlyphs[status] ?? 'warning';

export function runHeadline(run: RunView): string {
  const running = runningStep(run);
  if (!running) return run.error === null ? 'Aguardando o motor…' : 'A execução não pôde ser acompanhada';

  const { done, total } = stepProgress(run);
  return total > 0 ? `${running.title} · etapa ${Math.min(done + 1, total)} de ${total}` : `${running.title}…`;
}
