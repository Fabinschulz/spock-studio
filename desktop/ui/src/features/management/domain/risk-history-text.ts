import type { RiskChange } from '@/shared/schema';

import { SEVERITY_LABEL } from './delivery-labels';

const severity = (value: string | null): string =>
  value !== null && value in SEVERITY_LABEL
    ? SEVERITY_LABEL[value as keyof typeof SEVERITY_LABEL].label
    : (value ?? '');

const ids = (list: string[]) => list.map((id) => `#${id}`).join(', ');

/** O que mudou nos itens citados, em uma frase: "entraram #3; saíram #2". Vazio quando nada mudou nos itens. */
function itemsText(change: RiskChange): string {
  return [
    change.itemsAdded.length > 0 ? `entraram ${ids(change.itemsAdded)}` : null,
    change.itemsRemoved.length > 0 ? `saíram ${ids(change.itemsRemoved)}` : null
  ]
    .filter((part): part is string => part !== null)
    .join('; ');
}

/**
 * Uma mudança na trajetória de um risco, em uma frase. Um tipo novo do motor aparece como veio, em vez de sumir.
 * "Já estava ativo" diz o que se sabe: o primeiro registro não revela desde quando o risco existia.
 */
export function describeRiskChange(change: RiskChange): string {
  const items = itemsText(change);
  switch (change.kind) {
    case 'observed':
      return `Já estava ativo no primeiro registro, com gravidade ${severity(change.to)}.`;
    case 'appeared':
      return `Apareceu com gravidade ${severity(change.to)}.`;
    case 'raised':
      return `Subiu de ${severity(change.from)} para ${severity(change.to)}${items ? `; ${items}` : ''}.`;
    case 'lowered':
      return `Baixou de ${severity(change.from)} para ${severity(change.to)}${items ? `; ${items}` : ''}.`;
    case 'items_changed':
      return `Mudaram os itens: ${items}.`;
    case 'resolved':
      return 'Resolvido: deixou de disparar.';
    default:
      return change.kind;
  }
}
