import type { ScriptedEvent } from '@/prototype/types/event';

export const scriptDuration = (script: ScriptedEvent[]): number => script[script.length - 1]?.t ?? 0;
