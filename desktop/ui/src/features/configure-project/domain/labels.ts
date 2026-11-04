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

export const confidenceTone: Record<StackFinding['confidence'], Tone> = {
  high: 'success',
  medium: 'info',
  low: 'warning'
};

export const specificationStateLabel = {
  not_installed: 'Não instalado',
  installed: 'Instalado e íntegro',
  drifted: 'Instalado, mas diferente do registro do instalador',
  invalid: 'Instalação incompleta'
} as const;

export const specificationStateTone: Record<keyof typeof specificationStateLabel, Tone> = {
  not_installed: 'neutral',
  installed: 'success',
  drifted: 'danger',
  invalid: 'danger'
};

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
  remove: 'warning',
  conflict: 'danger'
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
  blocked: 'danger'
};

export const originLabel: Record<string, string> = {
  'baseline-rules': 'Regras-base',
  'harness-engineer': 'Harness Engineer',
  'mariliz.yaml': 'Sua declaração (mariliz.yaml)',
  existing: 'Já existente no projeto'
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

export const observationTone: Record<ObservationKind, Tone> = {
  fact: 'success',
  inference: 'info',
  hypothesis: 'warning',
  unverified: 'warning'
};

export const runKindLabel = { apply: 'Aplicação', revert: 'Reversão', validate: 'Validação' } as const;

export const stepStatusLabel = (status: string): string =>
  status === 'running'
    ? 'em andamento'
    : status === 'pending'
      ? 'aguardando'
      : status === 'completed'
        ? 'concluída'
        : status.replace(/_/g, ' ');
