import type { ScriptedEvent } from '@/prototype/types/event';
import type { EngineIdentity, ExpectedStep, OperationDraft, OperationView } from '@/prototype/types/operation';

import { applyEvent } from './apply-event';
import { deriveView } from './derive-view';
import { ensureStep } from './ensure-step';

// A visão sai só dos eventos; a única exceção são as etapas esperadas (marcadas `expected`).
export function buildOperation(
  events: ScriptedEvent[],
  expected: readonly ExpectedStep[],
  identity: EngineIdentity
): OperationView {
  const draft: OperationDraft = {
    steps: new Map(),
    order: [],
    actors: new Map(),
    runState: 'pending',
    currentStep: null
  };

  for (const step of expected) ensureStep(draft, step.id, step.title, true);
  for (const event of events) applyEvent(draft, event);

  return deriveView(draft, events.length, identity);
}
