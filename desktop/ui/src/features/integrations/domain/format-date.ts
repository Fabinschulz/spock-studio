import { formatRecentDate } from '@/features/project-selection';

const time = new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' });

/** Um valor que não é data aparece como veio, em vez de "Invalid Date". `now` existe para o teste fixar o relógio. */
export function formatEngineDate(iso: string, now: number = Date.now()): string {
  const ms = Date.parse(iso);
  return Number.isNaN(ms) ? iso : `${formatRecentDate(ms, now).toLowerCase()} às ${time.format(ms)}`;
}
