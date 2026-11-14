import type { Tone } from '@/shared/components';

type Label = { label: string; tone: Tone };

export const CONFIDENCE_LABEL: Record<'low' | 'medium' | 'high', Label> = {
  high: { label: 'Confiança alta', tone: 'success' },
  medium: { label: 'Confiança média', tone: 'warning' },
  low: { label: 'Confiança baixa', tone: 'danger' }
};

const SEVERITY = new Map<string, Label>([
  ['high', { label: 'alta', tone: 'danger' }],
  ['medium', { label: 'média', tone: 'warning' }],
  ['low', { label: 'baixa', tone: 'neutral' }]
]);

const MILESTONE_OUTLOOK = new Map<string, Label>([
  ['done', { label: 'Concluído', tone: 'success' }],
  ['on_track', { label: 'No prazo', tone: 'success' }],
  ['at_risk', { label: 'Em risco', tone: 'warning' }],
  ['behind', { label: 'Atrasado em relação ao prazo', tone: 'danger' }],
  ['overdue', { label: 'Prazo vencido', tone: 'danger' }],
  ['no_forecast', { label: 'Sem previsão', tone: 'neutral' }],
  ['no_due_date', { label: 'Sem prazo', tone: 'neutral' }]
]);

// Um valor novo do motor aparece como veio, em vez de sumir da tela ou do relatório.
export const severityLabel = (value: string): Label => SEVERITY.get(value) ?? { label: value, tone: 'neutral' };

export const milestoneOutlookLabel = (value: string): Label =>
  MILESTONE_OUTLOOK.get(value) ?? { label: value, tone: 'neutral' };
