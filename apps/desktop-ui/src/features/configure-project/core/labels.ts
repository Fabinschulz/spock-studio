import type { Tone } from '@/shared/components/ui';
import type {
  ComponentDecision,
  EvalResult,
  FileChange,
  PlannedCommand,
  RunResult,
  StackFinding
} from '@/shared/schemas';

/** Textos da jornada em português e o tom visual de cada estado. O estado em si sempre vem do motor. */

export const confidenceLabel: Record<StackFinding['confidence'], string> = {
  high: 'alta',
  medium: 'média',
  low: 'baixa'
};

export const confidenceTone = (c: StackFinding['confidence']): Tone =>
  c === 'high' ? 'success' : c === 'medium' ? 'info' : 'warning';

export const tlcStateLabel = {
  not_installed: 'Não instalado',
  installed: 'Instalado e íntegro',
  drifted: 'Instalado, mas diferente do registro do instalador',
  invalid: 'Instalação incompleta'
} as const;

export const tlcStateTone = (state: keyof typeof tlcStateLabel): Tone =>
  state === 'installed' ? 'success' : state === 'not_installed' ? 'neutral' : 'danger';

export const actionLabel: Record<FileChange['action'], string> = {
  create: 'Criar',
  update: 'Atualizar',
  preserve: 'Preservar',
  remove: 'Remover',
  conflict: 'Conflito'
};

export const actionTone = (a: FileChange['action']): Tone =>
  a === 'create'
    ? 'success'
    : a === 'update'
      ? 'info'
      : a === 'preserve'
        ? 'neutral'
        : a === 'remove'
          ? 'warning'
          : 'danger';

export const kindLabel: Record<ComponentDecision['kind'], string> = {
  sdd: 'Método SDD',
  agent: 'Agente',
  skill: 'Skill',
  hook: 'Hook',
  rule: 'Regra',
  tool: 'Ferramenta ou MCP',
  eval: 'Eval',
  config: 'Configuração'
};

export const decisionLabel: Record<string, string> = {
  enable: 'Habilitar',
  keep: 'Manter',
  skip: 'Dispensar',
  blocked: 'Bloqueado'
};

export const decisionTone = (d: string): Tone =>
  d === 'enable' ? 'success' : d === 'keep' ? 'info' : d === 'blocked' ? 'danger' : 'neutral';

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

export const evalStatusTone = (s: EvalResult['status']): Tone =>
  s === 'passed' ? 'success' : s === 'failed' ? 'danger' : s === 'blocked' ? 'warning' : 'neutral';

export const outcomeLabel: Record<RunResult['outcome'], string> = {
  completed: 'Concluído',
  completed_with_limitations: 'Concluído com limitações',
  failed: 'Falhou',
  requires_intervention: 'Requer intervenção',
  cancelled: 'Cancelado'
};

export const outcomeTone = (o: RunResult['outcome']): Tone =>
  o === 'completed'
    ? 'success'
    : o === 'completed_with_limitations'
      ? 'warning'
      : o === 'cancelled'
        ? 'neutral'
        : 'danger';

export const observationLabel = {
  fact: 'Fato',
  inference: 'Inferência',
  hypothesis: 'Hipótese',
  unverified: 'Não verificado'
} as const;

export const aiEnvironmentNames: Record<string, string> = {
  'claude-code': 'Claude Code',
  cursor: 'Cursor',
  codex: 'OpenAI Codex'
};

export const runKindLabel = { apply: 'Aplicação', revert: 'Reversão', validate: 'Validação' } as const;
