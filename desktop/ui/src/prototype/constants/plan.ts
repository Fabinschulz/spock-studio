import type { ConfigurationPlan } from '@/shared/schema';

import type { Tone } from '@/prototype/types/tag';

export const COMPONENT_KIND_TEXT: Record<ConfigurationPlan['decisions'][number]['kind'], string> = {
  specification: 'Estrutura de especificação',
  agent: 'Agente',
  skill: 'Skill',
  hook: 'Hook',
  rule: 'Regra',
  tool: 'Ferramenta ou MCP',
  eval: 'Eval',
  config: 'Configuração'
};

export const DECISION_TEXT: Record<string, string> = {
  enable: 'habilitar',
  keep: 'manter',
  skip: 'dispensar',
  blocked: 'bloqueado'
};

export const DECISION_TONE: Record<string, Tone> = {
  enable: 'success',
  keep: 'accent',
  skip: 'neutral',
  blocked: 'danger'
};
