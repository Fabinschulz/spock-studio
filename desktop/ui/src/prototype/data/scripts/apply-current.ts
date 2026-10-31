import { createScriptedEvent as ev } from '@/prototype/domain/create-scripted-event';
import type { ScriptedEvent } from '@/prototype/types/event';

import { APPLY_CORE_EVENTS, VALIDATE_START } from './apply-core';

// Contrato atual: a validação é uma etapa só; os Evals não emitem eventos individuais.
export const applyScriptCurrent: ScriptedEvent[] = [
  ...APPLY_CORE_EVENTS,
  ev(VALIDATE_START + 5200, 'step.completed', { id: 'validate', status: 'completed', detail: 'Passed' }),
  ev(VALIDATE_START + 5400, 'run.completed', { state: 'completed' })
];
