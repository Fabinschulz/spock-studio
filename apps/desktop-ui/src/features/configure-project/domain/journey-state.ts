import type { ConfigurationPlan, ConflictResolution, Discovery, RunEvent, RunResult } from '@/shared/schema';

import type { PlannedStep } from './planned-steps';

export type JourneyStepId = 'project' | 'model' | 'plan' | 'review' | 'run' | 'result';

export const journeySteps = [
  { id: 'project', label: 'Projeto' },
  { id: 'model', label: 'Modelo e ambiente' },
  { id: 'plan', label: 'Plano' },
  { id: 'review', label: 'Revisão' },
  { id: 'run', label: 'Aplicação' },
  { id: 'result', label: 'Resultado' }
] as const satisfies readonly { id: JourneyStepId; label: string }[];

export type Loadable<T> =
  { status: 'idle' } | { status: 'loading' } | { status: 'ready'; data: T } | { status: 'error'; message: string };

export type RunKind = 'apply' | 'revert' | 'validate';

export interface RunStepView {
  id: string;
  title: string;
  /** `pending` só existe para etapas que o motor sempre executa (ver planned-steps.ts); as demais vêm dos eventos. */
  status: string;
  detail: string | null;
  /** Instantes medidos no cliente, na chegada de cada evento (o motor ainda não carimba os eventos). */
  startedAt: number | null;
  endedAt: number | null;
}

/** Uma linha do feed de atividade, em linguagem de pessoa. */
export interface RunFeedItem {
  at: number;
  text: string;
}

/** O que a tela mostra de uma execução em andamento. Tudo vem de eventos do motor; nada é simulado. */
export interface RunView {
  kind: RunKind;
  runId: string | null;
  state: string;
  steps: RunStepView[];
  files: string[];
  feed: RunFeedItem[];
  startedAt: number;
  error: string | null;
}

export interface JourneyResults {
  apply: RunResult | null;
  revert: RunResult | null;
  /** Última validação avulsa: substitui a validação mostrada do apply. */
  validate: RunResult | null;
}

export interface JourneyState {
  step: JourneyStepId;
  discovery: Loadable<Discovery>;
  aiEnvironment: string | null;
  allowSpecificationReinstall: boolean;
  updateSpecification: boolean;
  plan: Loadable<ConfigurationPlan>;
  approvedChanges: string[];
  approvedCommands: string[];
  resolutions: Record<string, ConflictResolution>;
  run: RunView | null;
  results: JourneyResults;
}

export const initialJourneyState: JourneyState = {
  step: 'project',
  discovery: { status: 'idle' },
  aiEnvironment: null,
  allowSpecificationReinstall: false,
  updateSpecification: false,
  plan: { status: 'idle' },
  approvedChanges: [],
  approvedCommands: [],
  resolutions: {},
  run: null,
  results: { apply: null, revert: null, validate: null }
};

export type JourneyAction =
  | { type: 'discover.start' }
  | { type: 'discover.ok'; discovery: Discovery }
  | { type: 'discover.error'; message: string }
  | { type: 'environment.set'; id: string }
  | { type: 'reinstall.set'; value: boolean }
  | { type: 'update.set'; value: boolean }
  | { type: 'plan.start' }
  | { type: 'plan.ok'; plan: ConfigurationPlan }
  | { type: 'plan.error'; message: string }
  | { type: 'change.toggle'; id: string }
  | { type: 'command.toggle'; id: string }
  | { type: 'resolution.set'; id: string; value: ConflictResolution }
  | { type: 'step.go'; step: JourneyStepId }
  | { type: 'run.start'; kind: RunKind; at?: number; planned?: readonly PlannedStep[] }
  | { type: 'run.id'; runId: string }
  | { type: 'run.event'; event: RunEvent; at?: number }
  | { type: 'run.done'; result: RunResult }
  | { type: 'run.error'; message: string };

/** Defaults seguros: criar e atualizar vêm marcados; remover, conflito e comandos exigem um gesto da pessoa. */
export function defaultApprovals(plan: ConfigurationPlan) {
  return {
    approvedChanges: plan.changes.filter((c) => c.action === 'create' || c.action === 'update').map((c) => c.id),
    approvedCommands: [] as string[],
    resolutions: Object.fromEntries(
      plan.changes.filter((c) => c.action === 'conflict').map((c) => [c.id, 'keep' as const])
    )
  };
}

const toggle = (list: string[], id: string) => (list.includes(id) ? list.filter((x) => x !== id) : [...list, id]);

const feedOf = (run: RunView, at: number, text: string): RunFeedItem[] => [...run.feed, { at, text }];

function applyEvent(run: RunView, event: RunEvent, at: number): RunView {
  const data = event.data as Record<string, unknown>;
  const text = (key: string) => (typeof data[key] === 'string' ? (data[key] as string) : null);

  switch (event.type) {
    case 'run.state':
      return { ...run, state: text('state') ?? run.state };
    case 'step.started': {
      const id = text('id') ?? `step-${run.steps.length}`;
      const title = text('title') ?? id;
      const started: RunStepView = { id, title, status: 'running', detail: null, startedAt: at, endedAt: null };
      // Uma etapa conhecida de antemão muda de lugar? Não: ganha o estado. Uma desconhecida entra no fim.
      const known = run.steps.some((s) => s.id === id);
      const steps = known ? run.steps.map((s) => (s.id === id ? started : s)) : [...run.steps, started];
      return { ...run, steps, feed: feedOf(run, at, `Iniciou: ${title}`) };
    }
    case 'step.completed': {
      const id = text('id');
      const step = run.steps.find((s) => s.id === id);
      return {
        ...run,
        steps: run.steps.map((s) =>
          s.id === id ? { ...s, status: text('status') ?? 'completed', detail: text('detail'), endedAt: at } : s
        ),
        feed: step ? feedOf(run, at, `Concluiu: ${step.title}`) : run.feed
      };
    }
    case 'file.applied':
    case 'file.reverted': {
      const line = `${text('op') ?? text('status') ?? ''} ${text('path') ?? ''}`.trim();
      return { ...run, files: [...run.files, line], feed: feedOf(run, at, `Gravou ${text('path') ?? 'arquivo'}`) };
    }
    default:
      return run;
  }
}

export function journeyReducer(state: JourneyState, action: JourneyAction): JourneyState {
  switch (action.type) {
    case 'discover.start':
      return { ...state, discovery: { status: 'loading' } };
    case 'discover.ok': {
      const supported = action.discovery.aiEnvironments.filter((e) => e.supported);
      // Um ambiente suportado e único é sugerido; qualquer outro caso exige a escolha da pessoa.
      const suggested =
        action.discovery.aiEnvironments.length === 1 && supported.length === 1 ? (supported[0]?.id ?? null) : null;
      return {
        ...state,
        discovery: { status: 'ready', data: action.discovery },
        aiEnvironment: state.aiEnvironment ?? suggested
      };
    }
    case 'discover.error':
      return { ...state, discovery: { status: 'error', message: action.message } };
    case 'environment.set':
      return { ...state, aiEnvironment: action.id };
    case 'reinstall.set':
      return { ...state, allowSpecificationReinstall: action.value };
    case 'update.set':
      return { ...state, updateSpecification: action.value };
    case 'plan.start':
      return { ...state, plan: { status: 'loading' } };
    case 'plan.ok':
      return { ...state, plan: { status: 'ready', data: action.plan }, ...defaultApprovals(action.plan), step: 'plan' };
    case 'plan.error':
      return { ...state, plan: { status: 'error', message: action.message } };
    case 'change.toggle':
      return { ...state, approvedChanges: toggle(state.approvedChanges, action.id) };
    case 'command.toggle':
      return { ...state, approvedCommands: toggle(state.approvedCommands, action.id) };
    case 'resolution.set':
      return { ...state, resolutions: { ...state.resolutions, [action.id]: action.value } };
    case 'step.go':
      return { ...state, step: action.step };
    case 'run.start':
      return {
        ...state,
        step: action.kind === 'apply' ? 'run' : state.step,
        run: {
          kind: action.kind,
          runId: null,
          state: 'running',
          steps: (action.planned ?? []).map((s) => ({
            ...s,
            status: 'pending',
            detail: null,
            startedAt: null,
            endedAt: null
          })),
          files: [],
          feed: [],
          startedAt: action.at ?? 0,
          error: null
        }
      };
    case 'run.id':
      return state.run ? { ...state, run: { ...state.run, runId: action.runId } } : state;
    case 'run.event':
      return state.run ? { ...state, run: applyEvent(state.run, action.event, action.at ?? 0) } : state;
    case 'run.done': {
      const kind = action.result.kind;
      return {
        ...state,
        step: 'result',
        run: null,
        results: { ...state.results, [kind]: action.result }
      };
    }
    case 'run.error':
      return state.run ? { ...state, run: { ...state.run, state: 'failed', error: action.message } } : state;
  }
}

/** Entrada do harness.apply a partir do que a pessoa aprovou. Substituir um conflito conta como aprovar a mudança. */
export function buildApplyInput(state: JourneyState, projectId: string, plan: ConfigurationPlan) {
  const conflicts = plan.changes.filter((c) => c.action === 'conflict');
  const overwrites = conflicts.filter((c) => state.resolutions[c.id] === 'overwrite').map((c) => c.id);
  const planned = new Set(plan.changes.map((c) => c.id));

  return {
    projectId,
    planId: plan.planId,
    approvedChangeIds: [...new Set([...state.approvedChanges.filter((id) => planned.has(id)), ...overwrites])],
    approvedCommandIds: state.approvedCommands,
    resolutions: Object.fromEntries(conflicts.map((c) => [c.id, state.resolutions[c.id] ?? 'keep'] as const))
  };
}

export function countApprovals(state: JourneyState, plan: ConfigurationPlan): number {
  const input = buildApplyInput(state, '', plan);
  return input.approvedChangeIds.length + input.approvedCommandIds.length;
}

/**
 * Problema da etapa atual, para o indicador de progresso. Sai do estado real da jornada: nada é inferido da aparência.
 * `error` = a operação da etapa falhou; `blocked` = há algo que o motor bloqueou e a pessoa precisa ver.
 */
export function stepIssue(state: JourneyState): { stepId: JourneyStepId; kind: 'error' | 'blocked' } | undefined {
  switch (state.step) {
    case 'project':
      return state.discovery.status === 'error' ? { stepId: 'project', kind: 'error' } : undefined;
    case 'model':
      return state.plan.status === 'error' ? { stepId: 'model', kind: 'error' } : undefined;
    case 'plan':
      return state.plan.status === 'ready' && state.plan.data.decisions.some((d) => d.decision === 'blocked')
        ? { stepId: 'plan', kind: 'blocked' }
        : undefined;
    case 'run':
      return state.run?.error ? { stepId: 'run', kind: 'error' } : undefined;
    case 'result': {
      const outcome = state.results.apply?.outcome;
      return outcome === 'failed' || outcome === 'requires_intervention'
        ? { stepId: 'result', kind: 'error' }
        : undefined;
    }
    default:
      return undefined;
  }
}
