import type { FileChange, PlannedCommand } from '@/shared/schema';

import type { Tone } from '@/prototype/types/tag';

export const CHANGE_ACTION_TEXT: Record<FileChange['action'], string> = {
  create: 'criar',
  update: 'atualizar',
  preserve: 'preservar',
  remove: 'remover',
  conflict: 'conflito'
};

export const CHANGE_ACTION_TONE: Record<FileChange['action'], Tone> = {
  create: 'success',
  update: 'accent',
  preserve: 'neutral',
  remove: 'warning',
  conflict: 'danger'
};

export const COMMAND_EFFECT_TEXT: Record<PlannedCommand['effects'][number], string> = {
  process: 'executa um programa',
  network: 'acessa a rede',
  write_local: 'grava arquivos no projeto'
};
