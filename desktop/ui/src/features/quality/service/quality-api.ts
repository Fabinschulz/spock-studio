import type { EngineClient } from '@/shared/api';
import { reviewListSchema, reviewResponseSchema, type Review, type ReviewSummary } from '@/shared/schema';

export interface QualityApi {
  reviewWorkingTree(projectId: string): Promise<Review>;
  reviewBranch(projectId: string, base: string, head?: string): Promise<Review>;
  list(projectId: string): Promise<ReviewSummary[]>;
  detail(projectId: string, reviewId: string): Promise<Review>;
}

export function createQualityApi(client: EngineClient): QualityApi {
  return {
    reviewWorkingTree: async (projectId) =>
      (await client.request('quality.review.start', { projectId, mode: 'working_tree' }, reviewResponseSchema)).review,
    reviewBranch: async (projectId, base, head) =>
      (
        await client.request(
          'quality.review.start',
          { projectId, mode: 'branch', base, ...(head ? { head } : {}) },
          reviewResponseSchema
        )
      ).review,
    list: async (projectId) => (await client.request('quality.review.list', { projectId }, reviewListSchema)).reviews,
    detail: async (projectId, reviewId) =>
      (await client.request('quality.review.detail', { projectId, reviewId }, reviewResponseSchema)).review
  };
}
