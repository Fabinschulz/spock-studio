import { createScriptedEvent as ev } from '@/prototype/domain/create-scripted-event';
import type { ScriptedEvent } from '@/prototype/types/event';

import { applyResult } from '../fixtures';

const created = applyResult.files?.created ?? [];

function buildApplyCore(): ScriptedEvent[] {
  const events: ScriptedEvent[] = [
    ev(0, 'run.state', { state: 'running' }),
    ev(400, 'step.started', { id: 'precheck', title: 'Verificar pré-condições' }),
    ev(1500, 'step.completed', { id: 'precheck', status: 'completed', detail: null }),
    ev(1700, 'step.started', { id: 'backup', title: 'Guardar cópia do que será alterado' }),
    ev(2800, 'step.completed', { id: 'backup', status: 'completed', detail: null }),
    ev(3000, 'step.started', { id: 'write', title: 'Aplicar as alterações aprovadas' })
  ];

  let t = 3000;
  for (const path of created) {
    t += 320;
    events.push(ev(t, 'file.applied', { path, op: 'create' }));
  }
  t += 300;

  events.push(
    ev(t, 'step.completed', { id: 'write', status: 'completed', detail: `${created.length} operação(ões)` }),
    ev(t + 200, 'step.started', { id: 'lock', title: 'Registrar os arquivos gerenciados' }),
    ev(t + 1000, 'step.completed', { id: 'lock', status: 'completed', detail: null }),
    ev(t + 1200, 'run.state', { state: 'validating' }),
    ev(t + 1250, 'step.started', { id: 'validate', title: 'Validar a configuração aplicada' })
  );
  return events;
}

export const APPLY_CORE_EVENTS = buildApplyCore();
export const VALIDATE_START = APPLY_CORE_EVENTS[APPLY_CORE_EVENTS.length - 1]?.t ?? 0;
