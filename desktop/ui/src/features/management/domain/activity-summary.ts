import { STATUS_CATEGORY_LABEL } from '@/features/integrations';
import type { ProjectIntegration } from '@/shared/schema';

export type ActivityCount = { category: string; label: string; count: number };

export type ActivitySummary = {
  total: number;
  /** Na ordem do fluxo; categorias que o motor mandar fora da lista conhecida vêm depois, com o próprio nome. */
  byCategory: ActivityCount[];
  /** A leitura mais antiga entre as fontes com dados: o resumo é tão atual quanto ela. */
  oldestReadAt: string | null;
};

/**
 * Soma o que já foi sincronizado das fontes do projeto. É só contagem do que o motor observou: nenhum indicador é
 * derivado aqui, e fonte sem sincronização não entra como zero atividade.
 */
export function summarizeActivity(integrations: ProjectIntegration[]): ActivitySummary {
  const totals = new Map<string, number>();
  for (const integration of integrations) {
    for (const [category, count] of Object.entries(integration.items.byStatusCategory)) {
      totals.set(category, (totals.get(category) ?? 0) + count);
    }
  }

  const known = Object.keys(STATUS_CATEGORY_LABEL);
  const categories = [
    ...known.filter((category) => totals.has(category)),
    ...[...totals.keys()].filter((c) => !known.includes(c)).sort()
  ];
  const readAt = integrations
    .map((integration) => integration.items.lastSyncedAt)
    .filter((at): at is string => at !== null)
    .sort((a, b) => Date.parse(a) - Date.parse(b));

  return {
    total: [...totals.values()].reduce((sum, count) => sum + count, 0),
    byCategory: categories.map((category) => ({
      category,
      label: STATUS_CATEGORY_LABEL[category] ?? category,
      count: totals.get(category) ?? 0
    })),
    oldestReadAt: readAt[0] ?? null
  };
}
