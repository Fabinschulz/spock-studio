import type { JourneyAction, JourneyState } from '../types/journey';
import { defaultApprovals, toggleId } from './approvals';
import { applyRunEvent } from './run-events';

export function journeyReducer(state: JourneyState, action: JourneyAction): JourneyState {
  switch (action.type) {
    case 'discover.start':
      return { ...state, discovery: { status: 'loading' } };
    case 'discover.ok': {
      const supported = action.discovery.aiEnvironments.filter((e) => e.supported);
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
      return { ...state, approvedChanges: toggleId(state.approvedChanges, action.id) };
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
