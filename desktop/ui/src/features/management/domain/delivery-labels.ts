import type { Tone } from '@/shared/components';

export const CONFIDENCE_LABEL: Record<'low' | 'medium' | 'high', { label: string; tone: Tone }> = {
  high: { label: 'Confiança alta', tone: 'success' },
  medium: { label: 'Confiança média', tone: 'warning' },
  low: { label: 'Confiança baixa', tone: 'danger' }
};

export const SEVERITY_LABEL: Record<'low' | 'medium' | 'high', { label: string; tone: Tone }> = {
  high: { label: 'alta', tone: 'danger' },
  medium: { label: 'média', tone: 'warning' },
  low: { label: 'baixa', tone: 'neutral' }
};
