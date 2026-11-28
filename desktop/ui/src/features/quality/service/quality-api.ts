import type { EngineClient } from '@/shared/api';
import {
  correctionBriefSchema,
  gatesListSchema,
  reviewListSchema,
  reviewResponseSchema,
  type CorrectionBrief,
  type ReviewSummary,
  type ReviewView
} from '@/shared/schema';
import type { z } from 'zod';

export type GatesInfo = z.infer<typeof gatesListSchema>;

/** O que a pessoa pede além da revisão simples. Nada disso vai sem ela marcar. */
export interface ReviewRequest {
  useModel: boolean;
  requirements: string;
  gates: string[];
  gatesApproved: boolean;
}

export const SIMPLE_REVIEW: ReviewRequest = { useModel: false, requirements: '', gates: [], gatesApproved: false };

export interface QualityApi {
  reviewWorkingTree(projectId: string, request: ReviewRequest): Promise<ReviewView>;
  reviewBranch(projectId: string, base: string, request: ReviewRequest): Promise<ReviewView>;
  gates(projectId: string): Promise<GatesInfo>;
  list(projectId: string): Promise<ReviewSummary[]>;
  detail(projectId: string, reviewId: string): Promise<ReviewView>;
  decideFinding(
    projectId: string,
    reviewId: string,
    findingId: string,
    decision: string,
    justification?: string
  ): Promise<ReviewView>;
  decideDelivery(projectId: string, reviewId: string, decision: string, justification: string): Promise<ReviewView>;
  brief(projectId: string, reviewId: string): Promise<CorrectionBrief>;
}

function options(request: ReviewRequest) {
  return {
    ...(request.useModel ? { useModel: true } : {}),
    ...(request.requirements.trim() ? { requirements: request.requirements.trim() } : {}),
    ...(request.gates.length > 0 ? { gates: request.gates, gatesApproved: request.gatesApproved } : {})
  };
}

export function createQualityApi(client: EngineClient): QualityApi {
  return {
    reviewWorkingTree: (projectId, request) =>
      client.request(
        'quality.review.start',
        { projectId, mode: 'working_tree', ...options(request) },
        reviewResponseSchema
      ),
    reviewBranch: (projectId, base, request) =>
      client.request(
        'quality.review.start',
        { projectId, mode: 'branch', base, ...options(request) },
        reviewResponseSchema
      ),
    gates: (projectId) => client.request('quality.gates.list', { projectId }, gatesListSchema),
    list: async (projectId) => (await client.request('quality.review.list', { projectId }, reviewListSchema)).reviews,
    detail: (projectId, reviewId) =>
      client.request('quality.review.detail', { projectId, reviewId }, reviewResponseSchema),
    decideFinding: (projectId, reviewId, findingId, decision, justification) =>
      client.request(
        'quality.finding.decide',
        { projectId, reviewId, findingId, decision, ...(justification ? { justification } : {}) },
        reviewResponseSchema
      ),
    decideDelivery: (projectId, reviewId, decision, justification) =>
      client.request('quality.delivery.decide', { projectId, reviewId, decision, justification }, reviewResponseSchema),
    brief: async (projectId, reviewId) =>
      (await client.request('quality.review.brief', { projectId, reviewId }, correctionBriefSchema)).brief
  };
}
