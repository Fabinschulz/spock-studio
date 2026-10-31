import type { GlyphState } from './types';

export const stateText: Record<GlyphState, string> = {
  pending: 'aguardando',
  running: 'em andamento',
  done: 'concluída',
  warning: 'com ressalva',
  failed: 'falhou',
  skipped: 'não executada'
};
