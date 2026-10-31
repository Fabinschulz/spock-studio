import { EVAL_STATUS_TEXT } from '@/prototype/constants/evals';
import type { ScriptedEvent } from '@/prototype/types/event';
import type { FeedItem } from '@/prototype/types/feed';
import type { OperationView } from '@/prototype/types/operation';

import { readString } from './operation/read-event-data';

export function describeEvent(event: ScriptedEvent, view: OperationView, index: number): FeedItem | null {
  const { data } = event;
  const stepTitle = (id: string | null) => view.steps.find((s) => s.id === id)?.title ?? id ?? 'etapa';
  const base = { key: `${index}-${event.type}`, at: event.t, proposed: event.source === 'proposed' };

  switch (event.type) {
    case 'step.started':
      return { ...base, text: `Iniciou: ${stepTitle(readString(data, 'id'))}` };
    case 'step.completed':
      return { ...base, text: `Concluiu: ${stepTitle(readString(data, 'id'))}` };
    case 'file.applied':
      return { ...base, text: `Gravou ${readString(data, 'path') ?? 'arquivo'}` };
    case 'eval.completed': {
      const id = readString(data, 'id');
      const name = view.steps.find((s) => s.id === 'validate')?.evals.find((e) => e.id === id)?.name ?? id;
      const status = EVAL_STATUS_TEXT[readString(data, 'status') as keyof typeof EVAL_STATUS_TEXT] ?? 'concluída';
      return { ...base, text: `Avaliação “${name}”: ${status}` };
    }
    case 'agent.state':
      return { ...base, text: `${readString(data, 'name') ?? 'Agente'}: ${readString(data, 'activity') ?? ''}`.trim() };
    default:
      return null;
  }
}
