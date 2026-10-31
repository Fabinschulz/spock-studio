import type { EvalResult } from '@/shared/schema';
import type { Participant } from '../types/participant';

export const judgeStates: Record<EvalResult['status'], Pick<Participant, 'glyph' | 'state'>> = {
  passed: { glyph: 'done', state: 'Concluído' },
  failed: { glyph: 'failed', state: 'Reprovou' },
  blocked: { glyph: 'warning', state: 'Bloqueado' },
  not_run: { glyph: 'pending', state: 'Não executado' }
};

export const ENGINE_NOTE = 'Executa localmente, no seu computador.';
export const HARNESS_ENGINEER_NOTE =
  'Roda no seu ambiente de IA, fora do Mariliz. O motor só lê .mariliz/proposal.json.';
