import { z } from 'zod';

const connectorFieldSchema = z.object({ name: z.string(), label: z.string(), hint: z.string() });

/** O que o motor diz sobre um conector: é dele que a interface tira campos e rótulos, sem conhecer o provedor. */
export const connectorSchema = z.object({
  provider: z.string(),
  name: z.string(),
  version: z.string(),
  settings: z.array(connectorFieldSchema),
  secret: connectorFieldSchema,
  resource: connectorFieldSchema,
  dataKinds: z.array(z.object({ id: z.string(), label: z.string() })),
  scopesNote: z.string(),
  /** O que o conector pode fazer no provedor: `read` (só lê) ou `write` (altera dados). */
  impact: z.string()
});

export const integrationCatalogSchema = z.object({
  vaultAvailable: z.boolean(),
  /** Como o cofre do sistema se chama para a pessoa (Windows, Chaveiro do macOS, serviço de segredos do Linux). */
  vaultName: z.string(),
  /** Sem cofre, o que falta e o que fazer neste sistema. Nulo se o cofre está disponível. */
  vaultHint: z.string().nullable(),
  connectors: z.array(connectorSchema)
});

const integrationErrorSchema = z.object({ code: z.string(), message: z.string(), at: z.string() });

export const connectionStatusSchema = z.enum(['connected', 'error', 'disconnected']);

/** Uma conexão como o motor a expõe. A credencial nunca faz parte da resposta. */
export const connectionSchema = z.object({
  id: z.string(),
  provider: z.string(),
  name: z.string(),
  account: z.string(),
  status: connectionStatusSchema,
  scopes: z.array(z.string()),
  settings: z.record(z.string(), z.string()),
  connectedAt: z.string(),
  checkedAt: z.string(),
  lastSyncAt: z.string().nullable(),
  lastError: integrationErrorSchema.nullable(),
  connectorVersion: z.string(),
  projects: z.number()
});

export const connectionListSchema = z.object({ connections: z.array(connectionSchema) });
export const connectionResultSchema = z.object({ connection: connectionSchema });
export const removedSchema = z.object({ removed: z.boolean() });

export const syncExecutionSchema = z.object({
  id: z.string(),
  startedAt: z.string(),
  finishedAt: z.string().nullable(),
  status: z.enum(['running', 'succeeded', 'partial', 'failed']),
  incremental: z.boolean(),
  pages: z.number(),
  fetched: z.number(),
  created: z.number(),
  updated: z.number(),
  unchanged: z.number(),
  error: integrationErrorSchema.nullable()
});

/** O vínculo de uma conexão com um projeto: recorte, política e o resumo do que já foi sincronizado. */
export const projectIntegrationSchema = z.object({
  id: z.string(),
  projectId: z.string(),
  connectionId: z.string(),
  provider: z.string(),
  scope: z.object({ resource: z.string(), resourceName: z.string(), dataKinds: z.array(z.string()) }),
  policy: z.object({ feedsKnowledge: z.boolean() }),
  linkedAt: z.string(),
  connectionName: z.string(),
  connectionStatus: connectionStatusSchema,
  checkpoint: z.string().nullable(),
  syncing: z.boolean(),
  lastSync: syncExecutionSchema.nullable(),
  items: z.object({
    total: z.number(),
    byKind: z.record(z.string(), z.number()),
    byStatusCategory: z.record(z.string(), z.number()),
    lastSyncedAt: z.string().nullable()
  })
});

/** Um projeto que usa a conexão. `projectName` é nulo quando o projeto não está autorizado neste computador. */
export const reachProjectSchema = z.object({
  projectId: z.string(),
  projectName: z.string().nullable(),
  resource: z.string(),
  resourceName: z.string(),
  dataKinds: z.array(z.object({ id: z.string(), label: z.string() })),
  feedsKnowledge: z.boolean(),
  linkedAt: z.string()
});

/** O que uma conexão alcança: onde é usada, o que se lê de lá e o que o conector pode fazer (`read`, `write` ou `unknown`). */
export const connectionReachSchema = z.object({
  connectionId: z.string(),
  impact: z.string(),
  projects: z.array(reachProjectSchema)
});

export const connectionReachListSchema = z.object({ connections: z.array(connectionReachSchema) });

export const integrationListSchema = z.object({ integrations: z.array(projectIntegrationSchema) });
export const integrationResultSchema = z.object({ integration: projectIntegrationSchema });
/**
 * O que o motor avisa de uma sincronização (`sync.started`, `sync.progress`, `sync.finished`): só estado e contagens, nunca
 * itens, títulos ou credenciais. O `runId` do aviso é o id da sincronização.
 */
export const syncEventDataSchema = z.object({
  projectId: z.string(),
  integrationId: z.string(),
  status: z.string(),
  incremental: z.boolean(),
  pages: z.number(),
  fetched: z.number(),
  created: z.number(),
  updated: z.number(),
  unchanged: z.number(),
  error: z.string().nullable()
});

export const SYNC_EVENT_TYPES = ['sync.started', 'sync.progress', 'sync.finished'] as const;

export type SyncEvent = {
  type: (typeof SYNC_EVENT_TYPES)[number];
  syncId: string;
  data: z.infer<typeof syncEventDataSchema>;
};

export const syncResultSchema = z.object({ sync: syncExecutionSchema });
export const syncHistorySchema = z.object({ syncs: z.array(syncExecutionSchema) });

export type ConnectionReach = z.infer<typeof connectionReachSchema>;
export type ReachProject = z.infer<typeof reachProjectSchema>;
export type Connector = z.infer<typeof connectorSchema>;
export type IntegrationCatalog = z.infer<typeof integrationCatalogSchema>;
export type Connection = z.infer<typeof connectionSchema>;
export type ConnectionStatus = z.infer<typeof connectionStatusSchema>;
export type ProjectIntegration = z.infer<typeof projectIntegrationSchema>;
export type SyncExecution = z.infer<typeof syncExecutionSchema>;
