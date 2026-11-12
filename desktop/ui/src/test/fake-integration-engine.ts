import catalogFixture from '@contracts/fixtures/integrations/catalog.json';
import connectionsFixture from '@contracts/fixtures/integrations/connections.json';
import integrationsFixture from '@contracts/fixtures/integrations/integrations.json';
import answerFixture from '@contracts/fixtures/management/assistant-answer.json';
import evidenceFixture from '@contracts/fixtures/management/assistant-evidence.json';
import forecastFixture from '@contracts/fixtures/management/forecast.json';
import metricsFixture from '@contracts/fixtures/management/metrics.json';
import portfolioFixture from '@contracts/fixtures/management/portfolio.json';
import milestonesFixture from '@contracts/fixtures/management/milestones.json';
import risksFixture from '@contracts/fixtures/management/risks.json';

import { EngineError, type EngineClient } from '@/shared/api';
import type { Connection, ProjectIntegration, RunEvent, SyncExecution } from '@/shared/schema';

type Call = { method: string; params: Record<string, unknown> };

/** Uma conexão e um vínculo já sincronizado, exatamente como o motor real os devolve (fixtures do contrato). */
export const githubConnection = connectionsFixture.connections[0] as Connection;
export const syncedIntegration = integrationsFixture.integrations[0] as ProjectIntegration;

const NEVER_SYNCED = { total: 0, byKind: {}, byStatusCategory: {}, lastSyncedAt: null };

/**
 * O motor de mentira para as telas de integração: guarda conexões e vínculos em memória e responde no formato do
 * contrato. A sincronização fica "em andamento" até a lista ser relida, como no motor real, que roda em segundo plano.
 */
/** Respostas do assistente: a do motor real (fixture), uma com afirmações descartadas, ou um erro com código. */
type AssistantReply = 'answer' | 'rejected' | { code: string; message: string };

export function fakeIntegrationEngine(
  initial: {
    connections?: Connection[];
    vaultAvailable?: boolean;
    modelConfigured?: boolean;
    assistant?: AssistantReply;
    /** O provedor recusa a credencial ao testar. O motor real não falha: marca a conexão com erro. */
    testRefused?: boolean;
  } = {}
) {
  const calls: Call[] = [];
  let connections = [...(initial.connections ?? [])];
  let integrations: ProjectIntegration[] = [];
  let sequence = 0;
  let linkedItems: unknown[] = [];
  const listeners = new Set<(event: RunEvent) => void>();
  let eventSeq = 0;
  const publish = (type: string, runId: string, data: Record<string, unknown>) => {
    const event: RunEvent = { v: 1, kind: 'event', runId, type, seq: ++eventSeq, data };
    for (const listener of listeners) listener(event);
  };
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
    'connection.test': (params) => {
      const connection = connections.find((entry) => entry.id === params.connectionId);
      if (!connection) throw new EngineError('CONNECTION_NOT_FOUND', 'Conexão não encontrada.');
      const tested: Connection = initial.testRefused
        ? {
            ...connection,
            status: 'error',
            lastError: {
              code: 'INTEGRATION_AUTH_FAILED',
              message: 'O provedor recusou a credencial.',
              at: '2026-11-12T12:00:00+00:00'
            }
          }
        : { ...connection, status: 'connected', lastError: null };
      connections = connections.map((entry) => (entry.id === tested.id ? tested : entry));
      return { connection: tested };
    },
    'connection.disconnect': (params) => {
      const connection = connections.find((entry) => entry.id === params.connectionId);
      if (!connection) throw new EngineError('CONNECTION_NOT_FOUND', 'Conexão não encontrada.');
      const updated: Connection = { ...connection, status: 'disconnected', scopes: [] };
      connections = connections.map((entry) => (entry.id === updated.id ? updated : entry));
      return { connection: updated };
    },
    'connection.remove': (params) => {
      connections = connections.filter((entry) => entry.id !== params.connectionId);
      return { removed: true };
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
      // Como o motor real: a sincronização anda em segundo plano e avisa quando termina, sem esperar ser perguntado.
      setTimeout(() => {
        const done = syncedIntegration.lastSync!;
        publish('sync.finished', sync.id, {
          projectId: params.projectId,
          integrationId: params.integrationId,
          status: 'succeeded',
          incremental: false,
          pages: done.pages,
          fetched: done.fetched,
          created: done.created,
          updated: done.updated,
          unchanged: done.unchanged,
          error: null
        });
      }, 300);
      return { sync };
    },
    'integration.unlink': (params) => {
      integrations = integrations.filter((integration) => integration.id !== params.integrationId);
      return { removed: true };
    },
    'run.linked': () => ({ items: linkedItems }),
    'integration.history': () => ({ syncs: [syncedIntegration.lastSync] }),
    // Com fonte vinculada, os indicadores que o motor real calculou; sem fonte, a resposta vazia que ele dá.
    'management.metrics': (params) =>
      integrations.some((integration) => integration.projectId === params.projectId)
        ? { ...metricsFixture, projectId: params.projectId }
        : { ...metricsFixture, projectId: params.projectId, complete: false, sources: [], kinds: [] },
    'management.portfolio': () => portfolioFixture,
    'model.status': () => ({
      configured: initial.modelConfigured ?? true,
      provider: initial.modelConfigured === false ? '' : 'openai'
    }),
    'management.evidence': (params) => ({ ...evidenceFixture, projectId: params.projectId }),
    'management.ask': (params) => {
      const reply = initial.assistant ?? 'answer';
      if (typeof reply === 'object') throw new EngineError(reply.code, reply.message);
      return reply === 'answer'
        ? { ...answerFixture, projectId: params.projectId }
        : {
            ...answerFixture,
            projectId: params.projectId,
            summary: '',
            rejected: [
              {
                section: 'Cálculos',
                text: 'A média é de 3,5 entregas.',
                reason: 'Traz o número 3.5, que não está nos fatos citados.'
              }
            ]
          };
    },
    // Com fonte vinculada, os marcos que o motor real calculou; sem fonte, a resposta vazia que ele dá.
    'management.milestones': (params) =>
      integrations.some((integration) => integration.projectId === params.projectId)
        ? { ...milestonesFixture, projectId: params.projectId }
        : { ...milestonesFixture, projectId: params.projectId, complete: false, milestones: [], unassigned: [] },
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
    async onEvent(handler) {
      listeners.add(handler);
      return () => {
        listeners.delete(handler);
      };
    }
  };

  return {
    client,
    calls,
    /** Coloca um vínculo já sincronizado num projeto, sem passar pela tela. */
    /** Coloca itens com execuções ligadas, como `run.linked` os devolveria. */
    seedLinked(items: unknown[]) {
      linkedItems = items;
    },
    /** Publica um aviso do motor, como se ele o tivesse enviado. */
    emit: publish,
    seed(projectId: string, integration: ProjectIntegration = syncedIntegration) {
      integrations = [...integrations, { ...integration, projectId }];
    }
  };
}
