import { z } from 'zod';

const cloudFieldSchema = z.object({ name: z.string(), label: z.string(), hint: z.string(), required: z.boolean() });

/** Um jeito oficial do provedor de achar credencial; os campos são nomes (perfil, região, tenant), nunca segredo. */
export const cloudMechanismSchema = z.object({
  id: z.string(),
  label: z.string(),
  description: z.string(),
  fields: z.array(cloudFieldSchema)
});

export const cloudColumnSchema = z.object({ id: z.string(), label: z.string() });

export const cloudRegionUseSchema = z.enum(['none', 'required', 'filter']);

/** Uma leitura: as permissões são o que a identidade precisa ter; o que ela tem, só a verificação diz. */
export const cloudCapabilitySchema = z.object({
  id: z.string(),
  title: z.string(),
  description: z.string(),
  service: z.string(),
  permissions: z.array(z.string()),
  region: cloudRegionUseSchema,
  columns: z.array(cloudColumnSchema),
  impact: z.string()
});

export const cloudProviderSchema = z.object({
  provider: z.string(),
  name: z.string(),
  version: z.string(),
  mechanisms: z.array(cloudMechanismSchema),
  capabilities: z.array(cloudCapabilitySchema),
  identityNote: z.string()
});

export const cloudCatalogSchema = z.object({ providers: z.array(cloudProviderSchema) });

export const cloudConnectionStatusSchema = z.enum([
  'connected',
  'notAuthenticated',
  'expired',
  'invalidIdentity',
  'permissionDenied',
  'invalidRegion',
  'unavailable'
]);

export const cloudPermissionSchema = z.object({
  capability: z.string(),
  state: z.enum(['unchecked', 'allowed', 'denied', 'error']),
  code: z.string().nullable(),
  checkedAt: z.string()
});

/** Nenhuma credencial: a conexão guarda só nomes, e a credencial fica onde o SDK do provedor a procura. */
export const cloudConnectionSchema = z.object({
  id: z.string(),
  provider: z.string(),
  name: z.string(),
  settings: z.object({ mechanism: z.string(), values: z.record(z.string(), z.string()) }),
  status: cloudConnectionStatusSchema,
  identity: z
    .object({
      account: z.string(),
      accountName: z.string().nullable(),
      principal: z.string(),
      tenant: z.string().nullable()
    })
    .nullable(),
  permissions: z.array(cloudPermissionSchema),
  createdAt: z.string(),
  checkedAt: z.string(),
  lastError: z.object({ code: z.string(), message: z.string(), at: z.string() }).nullable(),
  providerVersion: z.string(),
  projects: z.number()
});

export const cloudConnectionListSchema = z.object({ connections: z.array(cloudConnectionSchema) });
export const cloudConnectionResultSchema = z.object({ connection: cloudConnectionSchema });

/** Nasce fechado: sem leitura liberada e sem agentes. */
export const cloudLinkSchema = z.object({
  id: z.string(),
  projectId: z.string(),
  connectionId: z.string(),
  provider: z.string(),
  capabilities: z.array(z.string()),
  agentAccess: z.boolean(),
  linkedAt: z.string()
});

export const cloudLinkListSchema = z.object({ links: z.array(cloudLinkSchema) });
export const cloudLinkResultSchema = z.object({ link: cloudLinkSchema });

export const cloudReadResultSchema = z.object({
  result: z.object({
    connectionId: z.string(),
    connectionName: z.string(),
    provider: z.string(),
    account: z.string().nullable(),
    capability: z.string(),
    region: z.string().nullable(),
    columns: z.array(cloudColumnSchema),
    rows: z.array(z.array(z.string().nullable())),
    truncated: z.boolean(),
    readAt: z.string()
  })
});

export type CloudProvider = z.infer<typeof cloudProviderSchema>;
export type CloudCapability = z.infer<typeof cloudCapabilitySchema>;
export type CloudMechanism = z.infer<typeof cloudMechanismSchema>;
export type CloudConnection = z.infer<typeof cloudConnectionSchema>;
export type CloudConnectionStatus = z.infer<typeof cloudConnectionStatusSchema>;
export type CloudLink = z.infer<typeof cloudLinkSchema>;
export type CloudRead = z.infer<typeof cloudReadResultSchema>['result'];
