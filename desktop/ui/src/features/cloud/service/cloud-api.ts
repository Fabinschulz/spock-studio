import type { EngineClient } from '@/shared/api';
import {
  cloudCatalogSchema,
  cloudConnectionListSchema,
  cloudConnectionResultSchema,
  cloudLinkListSchema,
  cloudLinkResultSchema,
  cloudReadResultSchema,
  removedSchema,
  type CloudConnection,
  type CloudLink,
  type CloudProvider,
  type CloudRead
} from '@/shared/schema';

export type NewCloudConnection = {
  provider: string;
  name: string;
  mechanism: string;
  settings: Record<string, string>;
};
export type NewCloudLink = { projectId: string; connectionId: string; capabilities: string[]; agentAccess: boolean };
export type CloudLinkChange = { projectId: string; linkId: string; capabilities?: string[]; agentAccess?: boolean };
export type CloudReadInput = {
  connectionId: string;
  capability: string;
  region?: string;
  limit?: number;
  projectId?: string;
};

/** Nenhuma credencial passa por aqui: a conexão guarda nomes e a credencial fica onde o SDK do provedor a procura. */
export type CloudApi = {
  catalog(): Promise<CloudProvider[]>;
  connections(): Promise<CloudConnection[]>;
  create(input: NewCloudConnection): Promise<CloudConnection>;
  validate(connectionId: string): Promise<CloudConnection>;
  permissions(connectionId: string): Promise<CloudConnection>;
  remove(connectionId: string): Promise<void>;
  links(projectId: string): Promise<CloudLink[]>;
  link(input: NewCloudLink): Promise<CloudLink>;
  configure(input: CloudLinkChange): Promise<CloudLink>;
  unlink(projectId: string, linkId: string): Promise<void>;
  read(input: CloudReadInput): Promise<CloudRead>;
};

export function createCloudApi(client: EngineClient): CloudApi {
  const connection = (method: 'cloud.validate' | 'cloud.permissions', connectionId: string) =>
    client.request(method, { connectionId }, cloudConnectionResultSchema).then((result) => result.connection);

  return {
    catalog: () => client.request('cloud.catalog', {}, cloudCatalogSchema).then((r) => r.providers),
    connections: () => client.request('cloud.list', {}, cloudConnectionListSchema).then((r) => r.connections),
    create: (input) => client.request('cloud.create', input, cloudConnectionResultSchema).then((r) => r.connection),
    validate: (connectionId) => connection('cloud.validate', connectionId),
    permissions: (connectionId) => connection('cloud.permissions', connectionId),
    remove: async (connectionId) => {
      await client.request('cloud.remove', { connectionId }, removedSchema);
    },
    links: (projectId) => client.request('cloud.links', { projectId }, cloudLinkListSchema).then((r) => r.links),
    link: (input) => client.request('cloud.link', input, cloudLinkResultSchema).then((r) => r.link),
    configure: (input) => client.request('cloud.configure', input, cloudLinkResultSchema).then((r) => r.link),
    unlink: async (projectId, linkId) => {
      await client.request('cloud.unlink', { projectId, linkId }, removedSchema);
    },
    read: (input) => client.request('cloud.read', input, cloudReadResultSchema).then((r) => r.result)
  };
}
