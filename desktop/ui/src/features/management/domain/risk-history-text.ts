import type { RiskChange } from '@/shared/schema';

import { severityLabel } from './delivery-labels';

const severity = (value: string | null): string => (value === null ? '' : severityLabel(value).label);

const ids = (list: string[]) => list.map((id) => `#${id}`).join(', ');

function itemsText(change: RiskChange): string {
  return [
    change.itemsAdded.length > 0 ? `entraram ${ids(change.itemsAdded)}` : null,
    change.itemsRemoved.length > 0 ? `saíram ${ids(change.itemsRemoved)}` : null
  ]
    .filter((part): part is string => part !== null)
    .join('; ');
}

/** "Já estava ativo" diz o que se sabe: o primeiro registro não revela desde quando o risco existia. */
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
