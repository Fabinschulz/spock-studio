import type { GlyphState } from '@/shared/components/ui';

import type { ActorState } from '@/prototype/types/operation';

export const ACTOR_LABEL: Record<ActorState, string> = {
  working: 'Em atividade',
  waiting: 'Aguardando',
  not_started: 'Não iniciado',
  done: 'Concluído',
  failed: 'Falhou'
};

export const ACTOR_GLYPH: Record<ActorState, GlyphState> = {
  working: 'running',
  waiting: 'pending',
  not_started: 'pending',
  done: 'done',
  failed: 'failed'
};
