import type { EngineClient } from '@/shared/api';
import {
  connectionListSchema,
  connectionReachListSchema,
  connectionResultSchema,
  integrationCatalogSchema,
  integrationListSchema,
  integrationResultSchema,
  removedSchema,
  syncHistorySchema,
  syncEventDataSchema,
  syncResultSchema,
  SYNC_EVENT_TYPES,
  type Connection,
  type ConnectionReach,
  type IntegrationCatalog,
  type ProjectIntegration,
  type SyncEvent,
  type SyncExecution
} from '@/shared/schema';

export type NewConnection = { provider: string; name: string; settings: Record<string, string>; secret: string };
export type NewLink = { projectId: string; connectionId: string; resource: string; dataKinds: string[] };
export type ScopeChange = { projectId: string; integrationId: string; dataKinds?: string[]; feedsKnowledge?: boolean };

/** A central de integrações, como o motor a expõe. A credencial vai para o motor e nunca volta. */
export type IntegrationApi = {
  catalog(): Promise<IntegrationCatalog>;
  connections(): Promise<Connection[]>;
  /** Onde cada conexão é usada, o que se lê de lá e o que o conector pode fazer. Só leitura. */
  reach(): Promise<ConnectionReach[]>;
  connect(input: NewConnection): Promise<Connection>;
  test(connectionId: string): Promise<Connection>;
  rotate(connectionId: string, secret: string): Promise<Connection>;
  disconnect(connectionId: string): Promise<Connection>;
  remove(connectionId: string): Promise<void>;
  integrations(projectId: string): Promise<ProjectIntegration[]>;
  link(input: NewLink): Promise<ProjectIntegration>;
  configure(input: ScopeChange): Promise<ProjectIntegration>;
  unlink(projectId: string, integrationId: string): Promise<void>;
  sync(projectId: string, integrationId: string): Promise<SyncExecution>;
  history(projectId: string, integrationId: string): Promise<SyncExecution[]>;
  /** Avisos de sincronização do motor, para a tela não precisar perguntar de tempos em tempos. Devolve como parar de ouvir. */
  onSyncEvent(handler: (event: SyncEvent) => void): Promise<() => void>;
};

export function createIntegrationApi(client: EngineClient): IntegrationApi {
  const connection = (method: 'connection.test' | 'connection.disconnect', connectionId: string) =>
    client.request(method, { connectionId }, connectionResultSchema).then((result) => result.connection);

  return {
    catalog: () => client.request('integration.catalog', {}, integrationCatalogSchema),
    connections: () => client.request('connection.list', {}, connectionListSchema).then((r) => r.connections),
    reach: () => client.request('connection.reach', {}, connectionReachListSchema).then((r) => r.connections),
    connect: (input) => client.request('connection.create', input, connectionResultSchema).then((r) => r.connection),
    test: (connectionId) => connection('connection.test', connectionId),
    rotate: (connectionId, secret) =>
      client.request('connection.rotate', { connectionId, secret }, connectionResultSchema).then((r) => r.connection),
    disconnect: (connectionId) => connection('connection.disconnect', connectionId),
    remove: async (connectionId) => {
      await client.request('connection.remove', { connectionId }, removedSchema);
    },
    integrations: (projectId) =>
      client.request('integration.list', { projectId }, integrationListSchema).then((r) => r.integrations),
    link: (input) => client.request('integration.link', input, integrationResultSchema).then((r) => r.integration),
    configure: (input) =>
      client.request('integration.configure', input, integrationResultSchema).then((r) => r.integration),
    unlink: async (projectId, integrationId) => {
      await client.request('integration.unlink', { projectId, integrationId }, removedSchema);
    },
    sync: (projectId, integrationId) =>
      client.request('integration.sync', { projectId, integrationId }, syncResultSchema).then((r) => r.sync),
    history: (projectId, integrationId) =>
      client.request('integration.history', { projectId, integrationId }, syncHistorySchema).then((r) => r.syncs),
    onSyncEvent: (handler) =>
      client.onEvent((event) => {
        const type = SYNC_EVENT_TYPES.find((known) => known === event.type);
        if (!type) return;
        // Um aviso fora do formato é desvio de contrato: ignorado, e a leitura de fallback da tela cobre o que ele traria.
        const data = syncEventDataSchema.safeParse(event.data);
        if (data.success) handler({ type, syncId: event.runId, data: data.data });
      })
  };
}
