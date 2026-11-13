import type { EngineClient } from '@/shared/api';
import { projectTimelineSchema, type ProjectTimeline } from '@/shared/schema';

export const PAGE_SIZE = 30;

/** `before` é o corte que a página anterior devolveu; sem ele, vem a primeira. */
export function readTimeline(
  client: EngineClient,
  projectId: string,
  kinds: string[],
  before: string | null
): Promise<ProjectTimeline> {
  return client.request(
    'timeline.read',
    { projectId, ...(kinds.length > 0 ? { kinds } : {}), limit: PAGE_SIZE, ...(before ? { before } : {}) },
    projectTimelineSchema
  );
}
