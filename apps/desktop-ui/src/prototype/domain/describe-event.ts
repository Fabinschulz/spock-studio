import type { ScriptedEvent } from '../data/demo-scripts';

import type { OperationView } from './operation-model';

export interface FeedItem {
  key: string;
  at: number;
  text: string;
  /** Eventos que o motor atual ainda não publica. */
  proposed: boolean;
}

const str = (data: Record<string, unknown>, key: string): string | null =>
  typeof data[key] === 'string' ? data[key] : null;

const evalStatusText: Record<string, string> = {
  passed: 'aprovado',
  failed: 'reprovado',
  blocked: 'bloqueado',
  not_run: 'não executado'
};

/** Texto de uma linha do feed de atividade, em linguagem de pessoa: sem códigos e sem logs. */
export function describeEvent(e: ScriptedEvent, view: OperationView, index: number): FeedItem | null {
  const stepTitle = (id: string | null) => view.steps.find((s) => s.id === id)?.title ?? id ?? 'etapa';
  const key = `${index}-${e.type}`;
  const base = { key, at: e.t, proposed: e.source === 'proposed' };

  switch (e.type) {
    case 'step.started':
      return { ...base, text: `Iniciou: ${stepTitle(str(e.data, 'id'))}` };
    case 'step.completed':
      return { ...base, text: `Concluiu: ${stepTitle(str(e.data, 'id'))}` };
    case 'file.applied':
      return { ...base, text: `Gravou ${str(e.data, 'path') ?? 'arquivo'}` };
    case 'eval.completed': {
      const id = str(e.data, 'id');
      const name = view.steps.find((s) => s.id === 'validate')?.evals.find((x) => x.id === id)?.name ?? id;
      return { ...base, text: `Avaliação “${name}”: ${evalStatusText[str(e.data, 'status') ?? ''] ?? 'concluída'}` };
    }
    case 'agent.state':
      return { ...base, text: `${str(e.data, 'name') ?? 'Agente'}: ${str(e.data, 'activity') ?? ''}`.trim() };
    default:
      return null;
  }
}

export const formatClock = (ms: number) => {
  const total = Math.floor(ms / 1000);
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`;
};
