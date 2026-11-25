import type { JourneyAction, JourneyState } from '../types/journey';
import { initialEnvironments, toggleEnvironment } from './ai-environments';
import { defaultApprovals, toggleId } from './approvals';
import { applyRunEvent } from './run-events';

export function journeyReducer(state: JourneyState, action: JourneyAction): JourneyState {
  switch (action.type) {
    case 'discover.start':
      return { ...state, discovery: { status: 'loading' } };
    case 'discover.ok':
      return {
        ...state,
        discovery: { status: 'ready', data: action.discovery },
        aiEnvironments: state.aiEnvironments.length > 0 ? state.aiEnvironments : initialEnvironments(action.discovery)
      };
    case 'discover.error':
      return { ...state, discovery: { status: 'error', message: action.message } };
    case 'environment.toggle':
      return { ...state, aiEnvironments: toggleEnvironment(state.aiEnvironments, action.id) };
    case 'reinstall.set':
      return { ...state, allowSpecificationReinstall: action.value };
    case 'update.set':
      return { ...state, updateSpecification: action.value };
    case 'install.set':
      return { ...state, installSpecification: action.value };
    case 'request.clear':
      return { ...state, componentRequest: { exclude: [], include: [] } };
    case 'plan.start':
      return { ...state, plan: { status: 'loading' } };
    case 'plan.ok':
      return {
        ...state,
        plan: { status: 'ready', data: action.plan },
        ...defaultApprovals(action.plan, state.installSpecification),
        step: 'review'
      };
    case 'plan.error':
      return { ...state, plan: { status: 'error', message: action.message } };
    case 'change.toggle':
      return { ...state, approvedChanges: toggleId(state.approvedChanges, action.id) };
    case 'changes.set': {
      const rest = state.approvedChanges.filter((id) => !action.ids.includes(id));
      return { ...state, approvedChanges: action.approved ? [...rest, ...action.ids] : rest };
    }
    case 'command.toggle':
      return { ...state, approvedCommands: toggleId(state.approvedCommands, action.id) };
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
          evals: [],
          feed: [],
          startedAt: action.at ?? 0,
          error: null
        }
      };
    case 'run.id':
      return state.run ? { ...state, run: { ...state.run, runId: action.runId } } : state;
    case 'run.event':
      return state.run ? { ...state, run: applyRunEvent(state.run, action.event, action.at ?? 0) } : state;
    case 'run.done':
      return {
        ...state,
        step: 'result',
        run: null,
        results: { ...state.results, [action.result.kind]: action.result }
      };
    case 'run.error':
      return state.run ? { ...state, run: { ...state.run, state: 'failed', error: action.message } } : state;
  }
}
