import catalogFixture from '@contracts/fixtures/integrations/catalog.json';
import connectionsFixture from '@contracts/fixtures/integrations/connections.json';
import integrationsFixture from '@contracts/fixtures/integrations/integrations.json';
import forecastFixture from '@contracts/fixtures/management/forecast.json';
import metricsFixture from '@contracts/fixtures/management/metrics.json';
import portfolioFixture from '@contracts/fixtures/management/portfolio.json';
import risksFixture from '@contracts/fixtures/management/risks.json';

import { EngineError, type EngineClient } from '@/shared/api';
import type { Connection, ProjectIntegration, SyncExecution } from '@/shared/schema';

type Call = { method: string; params: Record<string, unknown> };

/** Uma conexão e um vínculo já sincronizado, exatamente como o motor real os devolve (fixtures do contrato). */
export const githubConnection = connectionsFixture.connections[0] as Connection;
export const syncedIntegration = integrationsFixture.integrations[0] as ProjectIntegration;

const NEVER_SYNCED = { total: 0, byKind: {}, byStatusCategory: {}, lastSyncedAt: null };

/**
 * O motor de mentira para as telas de integração: guarda conexões e vínculos em memória e responde no formato do
 * contrato. A sincronização fica "em andamento" até a lista ser relida, como no motor real, que roda em segundo plano.
 */
export function fakeIntegrationEngine(initial: { connections?: Connection[]; vaultAvailable?: boolean } = {}) {
  const calls: Call[] = [];
  let connections = [...(initial.connections ?? [])];
  let integrations: ProjectIntegration[] = [];
  let sequence = 0;
  const nextId = (prefix: string) => `${prefix}-${String(++sequence).padStart(12, '0')}`;

  const finishRunningSyncs = () => {
    integrations = integrations.map((integration) =>
      integration.syncing
        ? {
            ...integration,
            syncing: false,
            lastSync: syncedIntegration.lastSync,
            checkpoint: syncedIntegration.checkpoint,
            items: syncedIntegration.items
          }
        : integration
    );
  };

  const handlers: Record<string, (params: Record<string, unknown>) => unknown> = {
    'integration.catalog': () => ({ ...catalogFixture, vaultAvailable: initial.vaultAvailable ?? true }),
    'connection.list': () => ({ connections }),
    'connection.create': (params) => {
      if (params.secret === 'token-recusado') {
        throw new EngineError('INTEGRATION_AUTH_FAILED', 'O provedor recusou a credencial.');
      }
      const connection: Connection = {
        ...githubConnection,
        id: nextId('conn'),
        provider: String(params.provider),
        name: String(params.name),
        lastSyncAt: null,
        projects: 0
      };
      connections = [...connections, connection];
      return { connection };
    },
    'integration.list': (params) => {
      const listed = integrations.filter((integration) => integration.projectId === params.projectId);
      finishRunningSyncs();
      return { integrations: listed };
    },
    'integration.link': (params) => {
      const connection = connections.find((entry) => entry.id === params.connectionId);
      const integration: ProjectIntegration = {
        ...syncedIntegration,
        id: nextId('pint'),
        projectId: String(params.projectId),
        connectionId: String(params.connectionId),
        connectionName: connection?.name ?? '',
        scope: {
          resource: String(params.resource),
          resourceName: String(params.resource),
          dataKinds: params.dataKinds as string[]
        },
        checkpoint: null,
        lastSync: null,
        items: NEVER_SYNCED
      };
      integrations = [...integrations, integration];
      return { integration };
    },
    'integration.sync': (params) => {
      integrations = integrations.map((integration) =>
        integration.id === params.integrationId ? { ...integration, syncing: true } : integration
      );
      const sync: SyncExecution = { ...syncedIntegration.lastSync!, status: 'running', finishedAt: null };
      return { sync };
    },
    'integration.unlink': (params) => {
      integrations = integrations.filter((integration) => integration.id !== params.integrationId);
      return { removed: true };
    },
    'integration.history': () => ({ syncs: [syncedIntegration.lastSync] }),
    // Com fonte vinculada, os indicadores que o motor real calculou; sem fonte, a resposta vazia que ele dá.
    'management.metrics': (params) =>
      integrations.some((integration) => integration.projectId === params.projectId)
        ? { ...metricsFixture, projectId: params.projectId }
        : { ...metricsFixture, projectId: params.projectId, complete: false, sources: [], kinds: [] },
    'management.portfolio': () => portfolioFixture,
    'management.forecast': (params) =>
      integrations.some((integration) => integration.projectId === params.projectId)
        ? { ...forecastFixture, projectId: params.projectId }
        : { ...forecastFixture, projectId: params.projectId, complete: false, kinds: [] },
    'management.risks': (params) =>
      integrations.some((integration) => integration.projectId === params.projectId)
        ? { ...risksFixture, projectId: params.projectId }
        : { ...risksFixture, projectId: params.projectId, complete: false, hasData: false, risks: [] }
  };

  const client: EngineClient = {
    async request(method, params, schema) {
      calls.push({ method, params });
      const handler = handlers[method];
      if (!handler) throw new Error(`Método sem resposta no dublê: ${method}`);
      return schema.parse(handler(params));
    },
    async onEvent() {
      return () => undefined;
    }
  };

  return {
    client,
    calls,
    /** Coloca um vínculo já sincronizado num projeto, sem passar pela tela. */
    seed(projectId: string, integration: ProjectIntegration = syncedIntegration) {
      integrations = [...integrations, { ...integration, projectId }];
    }
  };
}
