import { STEPS } from '@/prototype/constants/journey-steps';
import type { StepSlug } from '@/prototype/types/journey';

export const stepIndex = (slug: string | undefined): number => {
  const index = STEPS.findIndex((s) => s.slug === slug);
  return index < 0 ? 0 : index;
};

export const stepSlugAt = (index: number): StepSlug => STEPS[index]?.slug ?? 'projeto';

export const stepPath = (slug: StepSlug, search = ''): string => `/configurar/${slug}${search}`;
