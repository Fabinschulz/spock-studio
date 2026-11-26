import type { Tone } from '@/shared/components/ui';
import type {
  ComponentDecision,
  ConfigurationPlan,
  EvalResult,
  FileChange,
  PlannedCommand,
  StackFinding
} from '@/shared/schema';

type ObservationKind = ConfigurationPlan['observations'][number]['kind'];

export const confidenceLabel: Record<StackFinding['confidence'], string> = {
  high: 'alta',
  medium: 'média',
  low: 'baixa'
};

export const specificationStateLabel = {
  not_installed: 'Não instalado',
  installed: 'Instalado e íntegro',
  drifted: 'Instalado, mas diferente do registro do instalador',
  invalid: 'Instalação incompleta'
} as const;

export const actionLabel: Record<FileChange['action'], string> = {
  create: 'Criar',
  update: 'Atualizar',
  preserve: 'Preservar',
  remove: 'Remover',
  conflict: 'Conflito'
};

export const actionTone: Record<FileChange['action'], Tone> = {
  create: 'success',
  update: 'info',
  preserve: 'neutral',
  remove: 'danger',
  conflict: 'warning'
};

export const componentKindLabel: Record<ComponentDecision['kind'], string> = {
  specification: 'Estrutura de especificação',
  agent: 'Agente',
  skill: 'Skill',
  hook: 'Hook',
  rule: 'Regra',
  tool: 'Ferramenta ou MCP',
  eval: 'Eval',
  config: 'Configuração'
};

/** Título do grupo de arquivos na Revisão. */
export const componentKindGroupLabel: Record<ComponentDecision['kind'], string> = {
  specification: 'Estrutura de especificação',
  agent: 'Agentes',
  skill: 'Skills',
  hook: 'Hooks',
  rule: 'Regras',
  tool: 'Ferramentas e MCP',
  eval: 'Evals',
  config: 'Configuração'
};

export const toolKindLabel: Record<string, string> = {
  build: 'Build',
  test: 'Teste',
  lint: 'Lint',
  typecheck: 'Tipos',
  run: 'Executar'
};

export const decisionLabel: Record<string, string> = {
  enable: 'Habilitar',
  keep: 'Manter',
  skip: 'Dispensar',
  blocked: 'Bloqueado'
};

export const decisionTone: Record<string, Tone> = {
  enable: 'success',
  keep: 'info',
  skip: 'neutral',
  blocked: 'warning'
};

export const originLabel: Record<string, string> = {
  'baseline-rules': 'Regras-base',
  'harness-engineer': 'Harness Engineer',
  'mariliz.yaml': 'Sua declaração do projeto',
  existing: 'Já existente no projeto',
  architecture: 'Arquitetura aprovada em Da ideia ao projeto'
};

export const effectLabel: Record<PlannedCommand['effects'][number], string> = {
  process: 'Executa um programa',
  network: 'Acessa a rede',
  write_local: 'Grava arquivos no projeto'
};

export const evalStatusLabel: Record<EvalResult['status'], string> = {
  passed: 'Aprovado',
  failed: 'Reprovado',
  blocked: 'Bloqueado',
  not_run: 'Não executado'
};

export const evalStatusTone: Record<EvalResult['status'], Tone> = {
  passed: 'success',
  failed: 'danger',
  blocked: 'warning',
  not_run: 'neutral'
};

export const observationLabel: Record<ObservationKind, string> = {
  fact: 'Fato',
  inference: 'Inferência',
  hypothesis: 'Hipótese',
  unverified: 'Não verificado'
};

export const runKindLabel = { apply: 'Aplicação', revert: 'Reversão', validate: 'Validação' } as const;

const stepStatusLabels: Record<string, string> = {
  running: 'em andamento',
  pending: 'aguardando',
  completed: 'concluída'
};

/** Estado de uma etapa; um estado novo do motor aparece legível (`a_b` vira `a b`), nunca some. */
export const stepStatusLabel = (status: string): string => stepStatusLabels[status] ?? status.replace(/_/g, ' ');

const LEGACY_ID_PREFIX = 'mariliz.';

/**
 * O id dos componentes ainda começa pelo nome antigo do produto. Só a tela tira o prefixo: o motor, o lock e os
 * contratos seguem com o id inteiro, e o que se mostra continua sendo um trecho exato dele, que dá para buscar.
 */
export const componentIdLabel = (id: string): string =>
  id.startsWith(LEGACY_ID_PREFIX) ? id.slice(LEGACY_ID_PREFIX.length) : id;
