export type EventSource = 'engine' | 'proposed';

export interface ScriptedEvent {
  t: number;
  type: string;
  source: EventSource;
  data: Record<string, unknown>;
}
