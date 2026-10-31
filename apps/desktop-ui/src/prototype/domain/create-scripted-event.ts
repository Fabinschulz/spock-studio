import type { EventSource, ScriptedEvent } from '@/prototype/types/event';

export const createScriptedEvent = (
  t: number,
  type: string,
  data: Record<string, unknown>,
  source: EventSource = 'engine'
): ScriptedEvent => ({ t, type, source, data });
