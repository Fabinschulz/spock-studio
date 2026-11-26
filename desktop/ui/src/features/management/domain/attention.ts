import type { ProjectMetrics, ProjectRisks } from '@/shared/schema';
import { plural } from '@/shared/utils';

/** O mesmo prazo do Portfólio: dados mais antigos que isso deixam de descrever o andamento atual. */
export const STALE_AFTER_DAYS = 7;
const DAY_MS = 86_400_000;

export interface AttentionItem {
  id: 'risks' | 'overdue' | 'incomplete' | 'stale';
  text: string;
}

export interface Attention {
  /** Sem nenhum dado sincronizado, "nada pede atenção" seria falso: o que falta é dado. */
  hasData: boolean;
  items: AttentionItem[];
}

interface AttentionInput {
  risks: ProjectRisks;
  metrics: ProjectMetrics;
  oldestReadAt: string | null;
  /** O teste fixa o relógio. */
  now?: number;
}

/** Só junta o que o motor já calculou: riscos por regra, prazo vencido e a confiabilidade dos dados. Nada é derivado aqui. */
export function summarizeAttention({ risks, metrics, oldestReadAt, now = Date.now() }: AttentionInput): Attention {
  const items: AttentionItem[] = [];

  const active = risks.hasData ? risks.risks : [];
  if (active.length > 0) {
    const high = active.filter((risk) => risk.severity === 'high').length;
    items.push({
      id: 'risks',
      text: `${plural(active.length, 'risco ativo', 'riscos ativos')}${high > 0 ? `, ${plural(high, 'de gravidade alta', 'de gravidade alta')}` : ''}.`
    });
  }

  const overdue = metrics.kinds.reduce((sum, kind) => sum + (kind.overdue.value ?? 0), 0);
  if (overdue > 0)
    items.push({
      id: 'overdue',
      text: `${plural(overdue, 'item aberto com prazo vencido', 'itens abertos com prazo vencido')}.`
    });

  if (!metrics.complete)
    items.push({
      id: 'incomplete',
      text: 'Dados incompletos: ao menos uma fonte teve a última sincronização parcial ou com falha, então podem faltar itens.'
    });

  const readAt = oldestReadAt ? Date.parse(oldestReadAt) : Number.NaN;
  if (!Number.isNaN(readAt) && now - readAt > STALE_AFTER_DAYS * DAY_MS)
    items.push({ id: 'stale', text: `Dados lidos há ${Math.floor((now - readAt) / DAY_MS)} dias.` });

  return { hasData: risks.hasData || metrics.kinds.length > 0, items };
}
