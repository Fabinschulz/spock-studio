import type { EngineClient } from '@/shared/api';
import {
  connectionListSchema,
  connectionResultSchema,
  integrationCatalogSchema,
  integrationListSchema,
  integrationResultSchema,
  removedSchema,
  syncHistorySchema,
  syncResultSchema,
  type Connection,
  type IntegrationCatalog,
  type ProjectIntegration,
  type SyncExecution
} from '@/shared/schema';

export type NewConnection = { provider: string; name: string; settings: Record<string, string>; secret: string };
export type NewLink = { projectId: string; connectionId: string; resource: string; dataKinds: string[] };
export type ScopeChange = { projectId: string; integrationId: string; dataKinds?: string[]; feedsKnowledge?: boolean };

/** A central de integrações, como o motor a expõe. A credencial vai para o motor e nunca volta. */
export type IntegrationApi = {
  catalog(): Promise<IntegrationCatalog>;
  connections(): Promise<Connection[]>;
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
};

export function createIntegrationApi(client: EngineClient): IntegrationApi {
  const connection = (method: 'connection.test' | 'connection.disconnect', connectionId: string) =>
    client.request(method, { connectionId }, connectionResultSchema).then((result) => result.connection);

  return {
    catalog: () => client.request('integration.catalog', {}, integrationCatalogSchema),
    connections: () => client.request('connection.list', {}, connectionListSchema).then((r) => r.connections),
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
      client.request('integration.history', { projectId, integrationId }, syncHistorySchema).then((r) => r.syncs)
  };
}
