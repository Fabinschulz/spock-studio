import type { ScriptedEvent } from '@/prototype/types/event';
import type { ActorState, ExpectedStep, OperationDraft } from '@/prototype/types/operation';
import { ensureStep } from './ensure-step';
import { readString } from './read-event-data';
import { stepStateFromStatus } from './step-state';

const EVAL_STATUSES = ['passed', 'failed', 'blocked', 'not_run'] as const;
const AGENT_STATES = ['working', 'done', 'failed', 'waiting'] as const;

const isOneOf = <T extends string>(options: readonly T[], value: string | null): value is T =>
  value !== null && (options as readonly string[]).includes(value);

export function applyEvent(draft: OperationDraft, event: ScriptedEvent): void {
  const { data } = event;

  switch (event.type) {
    case 'run.plan': {
      const steps = Array.isArray(data['steps']) ? (data['steps'] as ExpectedStep[]) : [];
      for (const step of steps) ensureStep(draft, step.id, step.title, true);
      return;
    }
    case 'run.state':
      draft.runState = readString(data, 'state') ?? draft.runState;
      return;
    case 'step.started': {
      const id = readString(data, 'id') ?? `step-${draft.order.length}`;
      const title = readString(data, 'title');
      const step = ensureStep(draft, id, title ?? id, false);
      step.title = title ?? step.title;
      step.state = 'running';
      step.startedAt = event.t;
      draft.currentStep = id;
      return;
    }
    case 'step.completed': {
      const step = draft.steps.get(readString(data, 'id') ?? '');
      if (!step) return;
      step.state = stepStateFromStatus(readString(data, 'status'));
      step.detail = readString(data, 'detail');
      step.endedAt = event.t;
      if (draft.currentStep === step.id) draft.currentStep = null;
      return;
    }
    case 'file.applied': {
      const step = draft.currentStep ? draft.steps.get(draft.currentStep) : undefined;
      const path = readString(data, 'path');
      if (step && path) step.files.push(path);
      return;
    }
    case 'eval.started': {
      const step = draft.steps.get('validate');
      const id = readString(data, 'id');
      if (!step || !id) return;
      step.evals.push({
        id,
        name: readString(data, 'name') ?? id,
        kind: readString(data, 'kind') ?? '',
        state: 'running'
      });
      return;
    }
    case 'eval.completed': {
      const item = draft.steps.get('validate')?.evals.find((e) => e.id === readString(data, 'id'));
      const status = readString(data, 'status');
      if (item && isOneOf(EVAL_STATUSES, status)) item.state = status;
      return;
    }
    case 'agent.state': {
      const id = readString(data, 'agent');
      if (!id) return;
      const raw = readString(data, 'state');
      const state: ActorState = isOneOf(AGENT_STATES, raw) ? raw : 'not_started';
      draft.actors.set(id, {
        id,
        name: readString(data, 'name') ?? id,
        state,
        activity: readString(data, 'activity'),
        source: 'proposed'
      });
      return;
    }
    case 'run.completed':
      draft.runState = readString(data, 'state') ?? 'completed';
      return;
  }
}
