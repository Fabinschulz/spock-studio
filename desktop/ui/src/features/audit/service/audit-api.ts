import type { EngineClient } from '@/shared/api';
import { projectAuditTrailSchema, type ProjectAuditTrail } from '@/shared/schema';

export const PAGE_SIZE = 30;

/** Uma página da trilha de auditoria. `before` é o que a página anterior devolveu; sem ele, a primeira. Só leitura. */
export function readAudit(
  client: EngineClient,
  projectId: string,
  categories: string[],
  before: string | null
): Promise<ProjectAuditTrail> {
  return client.request(
    'audit.read',
    { projectId, ...(categories.length > 0 ? { categories } : {}), limit: PAGE_SIZE, ...(before ? { before } : {}) },
    projectAuditTrailSchema
  );
}
