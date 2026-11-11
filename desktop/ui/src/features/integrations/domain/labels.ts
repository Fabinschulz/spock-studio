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

/** Categorias de situação na ordem do fluxo. O que o motor mandar fora desta lista aparece com o próprio nome. */
export const STATUS_CATEGORY_LABEL: Record<string, string> = {
  todo: 'A fazer',
  inProgress: 'Em andamento',
  done: 'Concluídas'
};

export function connectorName(connectors: Connector[], provider: string): string {
  return connectors.find((connector) => connector.provider === provider)?.name ?? provider;
}

export function dataKindLabel(connector: Connector | undefined, kind: string): string {
  return connector?.dataKinds.find((entry) => entry.id === kind)?.label ?? kind;
}

/** "3 criados, 1 atualizado, 5 sem mudança": só as parcelas que existem. */
export function syncCounts(sync: SyncExecution): string {
  const parts = [
    sync.created > 0 ? `${sync.created} ${sync.created === 1 ? 'novo' : 'novos'}` : null,
    sync.updated > 0 ? `${sync.updated} ${sync.updated === 1 ? 'atualizado' : 'atualizados'}` : null,
    sync.unchanged > 0 ? `${sync.unchanged} sem mudança` : null
  ].filter((part): part is string => part !== null);
  return parts.length > 0 ? parts.join(', ') : 'nada novo';
}

/** O que dizer do recorte antes da primeira sincronização e depois dela. */
export function itemsSummary(integration: ProjectIntegration, connector: Connector | undefined): string {
  if (integration.items.total === 0) {
    return integration.lastSync ? 'Nenhum item no recorte' : 'Ainda não sincronizada';
  }
  return kindCounts(integration, connector).join(' · ');
}

/** "Issues: 96", "Pull requests: 52": o rótulo é o do conector, sem tentar flexionar um nome que vem de fora. */
export function kindCounts(integration: ProjectIntegration, connector: Connector | undefined): string[] {
  return Object.entries(integration.items.byKind).map(([kind, count]) => `${dataKindLabel(connector, kind)}: ${count}`);
}
