import { EXPECTED_APPLY_STEPS } from '@/prototype/constants/apply-steps';
import { createScriptedEvent as ev } from '@/prototype/domain/create-scripted-event';
import type { ScriptedEvent } from '@/prototype/types/event';

import { applyResult } from '../fixtures';
import { APPLY_CORE_EVENTS, VALIDATE_START } from './apply-core';

const evals = applyResult.validation?.results ?? [];
const EVAL_INTERVAL = 340;
const EVAL_DURATION = 280;
const evalsEnd = VALIDATE_START + 200 + evals.length * EVAL_INTERVAL;

const evalEvents = evals.flatMap((e, i): ScriptedEvent[] => {
  const start = VALIDATE_START + 200 + i * EVAL_INTERVAL;
  return [
    ev(start, 'eval.started', { id: e.id, name: e.name, kind: e.kind }, 'proposed'),
    ev(start + EVAL_DURATION, 'eval.completed', { id: e.id, status: e.status }, 'proposed')
  ];
});

// Contrato proposto: o início declara as etapas e cada Eval publica início e fim.
export const applyScriptProposed: ScriptedEvent[] = [
  ev(0, 'run.plan', { steps: EXPECTED_APPLY_STEPS }, 'proposed'),
  ...APPLY_CORE_EVENTS,
  ...evalEvents,
  ev(evalsEnd + 200, 'step.completed', { id: 'validate', status: 'completed', detail: 'Passed' }),
  ev(evalsEnd + 400, 'run.completed', { state: 'completed' })
];
