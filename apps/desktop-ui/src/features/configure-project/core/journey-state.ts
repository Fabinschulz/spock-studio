import type { ConfigurationPlan, Discovery, RunEvent, RunResult } from '@/shared/schemas';

export type JourneyStepId = 'project' | 'method' | 'plan' | 'review' | 'run' | 'result';

export const journeySteps = [
  { id: 'project', label: 'Projeto' },
  { id: 'method', label: 'Método e ambiente' },
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
  status: string;
  detail: string | null;
}

/** O que a tela mostra de uma execução em andamento. Tudo vem de eventos do motor; nada é simulado. */
export interface RunView {
  kind: RunKind;
  runId: string | null;
  state: string;
  steps: RunStepView[];
  files: string[];
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
  allowTlcReinstall: boolean;
  updateTlc: boolean;
  plan: Loadable<ConfigurationPlan>;
  approvedChanges: string[];
  approvedCommands: string[];
  resolutions: Record<string, 'keep' | 'overwrite'>;
  run: RunView | null;
  results: JourneyResults;
}

export const initialJourneyState: JourneyState = {
  step: 'project',
  discovery: { status: 'idle' },
  aiEnvironment: null,
  allowTlcReinstall: false,
  updateTlc: false,
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
  | { type: 'resolution.set'; id: string; value: 'keep' | 'overwrite' }
  | { type: 'step.go'; step: JourneyStepId }
  | { type: 'run.start'; kind: RunKind }
  | { type: 'run.id'; runId: string }
  | { type: 'run.event'; event: RunEvent }
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

function applyEvent(run: RunView, event: RunEvent): RunView {
  const data = event.data as Record<string, unknown>;
  const text = (key: string) => (typeof data[key] === 'string' ? (data[key] as string) : null);

  switch (event.type) {
    case 'run.state':
      return { ...run, state: text('state') ?? run.state };
    case 'step.started': {
      const id = text('id') ?? `step-${run.steps.length}`;
      const step: RunStepView = { id, title: text('title') ?? id, status: 'running', detail: null };
      return { ...run, steps: [...run.steps.filter((s) => s.id !== id), step] };
    }
    case 'step.completed': {
      const id = text('id');
      return {
        ...run,
        steps: run.steps.map((s) =>
          s.id === id ? { ...s, status: text('status') ?? 'completed', detail: text('detail') } : s
        )
      };
    }
    case 'file.applied':
    case 'file.reverted':
      return { ...run, files: [...run.files, `${text('op') ?? text('status') ?? ''} ${text('path') ?? ''}`.trim()] };
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
      return { ...state, allowTlcReinstall: action.value };
    case 'update.set':
      return { ...state, updateTlc: action.value };
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
        run: { kind: action.kind, runId: null, state: 'running', steps: [], files: [], error: null }
      };
    case 'run.id':
      return state.run ? { ...state, run: { ...state.run, runId: action.runId } } : state;
    case 'run.event':
      return state.run ? { ...state, run: applyEvent(state.run, action.event) } : state;
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
