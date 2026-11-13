import type { EngineClient } from '@/shared/api';
import { projectAuditTrailSchema, type ProjectAuditTrail } from '@/shared/schema';

export const PAGE_SIZE = 30;

/** `before` é o corte que a página anterior devolveu; sem ele, vem a primeira. */
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
