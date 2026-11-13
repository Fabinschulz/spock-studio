import type { Tone } from '@/shared/components';
import type { ConnectionStatus, Connector, ProjectIntegration, SyncExecution } from '@/shared/schema';

export const CONNECTION_STATUS: Record<ConnectionStatus, { label: string; tone: Tone }> = {
  connected: { label: 'Conectada', tone: 'success' },
  error: { label: 'Com erro', tone: 'danger' },
  disconnected: { label: 'Sem credencial', tone: 'neutral' }
};

export const SYNC_STATUS: Record<SyncExecution['status'], { label: string; tone: Tone }> = {
  running: { label: 'Sincronizando', tone: 'info' },
  succeeded: { label: 'Concluída', tone: 'success' },
  partial: { label: 'Parcial', tone: 'warning' },
  failed: { label: 'Falhou', tone: 'danger' }
};

/** O que o motor mandar fora desta lista aparece com o próprio nome. */
export const STATUS_CATEGORY_LABEL: Record<string, string> = {
  todo: 'A fazer',
  inProgress: 'Em andamento',
  done: 'Concluídas'
};

/** Um valor que a tela não conhece não vira "só lê". */
export function impactText(impact: string, provider: string): string {
  if (impact === 'read') return `Leitura: o Spock Studio só lê do ${provider}. Não cria, altera nem apaga nada lá.`;
  if (impact === 'write') return `Altera dados: o Spock Studio pode alterar dados no ${provider}.`;
  return 'Esta versão do Spock Studio não reconhece o conector desta conexão, então não afirma o que ela pode fazer.';
}

export function dataKindLabel(connector: Connector | undefined, kind: string): string {
  return connector?.dataKinds.find((entry) => entry.id === kind)?.label ?? kind;
}

export function syncCounts(sync: SyncExecution): string {
  const parts = [
    sync.created > 0 ? `${sync.created} ${sync.created === 1 ? 'novo' : 'novos'}` : null,
    sync.updated > 0 ? `${sync.updated} ${sync.updated === 1 ? 'atualizado' : 'atualizados'}` : null,
    sync.unchanged > 0 ? `${sync.unchanged} sem mudança` : null
  ].filter((part): part is string => part !== null);
  return parts.length > 0 ? parts.join(', ') : 'nada novo';
}

export function itemsSummary(integration: ProjectIntegration, connector: Connector | undefined): string {
  if (integration.items.total === 0) {
    return integration.lastSync ? 'Nenhum item no recorte' : 'Ainda não sincronizada';
  }
  return kindCounts(integration, connector).join(' · ');
}

/** O rótulo é o do conector, sem tentar flexionar um nome que vem de fora. */
export function kindCounts(integration: ProjectIntegration, connector: Connector | undefined): string[] {
  return Object.entries(integration.items.byKind).map(([kind, count]) => `${dataKindLabel(connector, kind)}: ${count}`);
}
