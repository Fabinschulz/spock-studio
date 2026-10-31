import type { JourneyState, StepIssue } from '../types/journey';

export function stepIssue(state: JourneyState): StepIssue | undefined {
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
