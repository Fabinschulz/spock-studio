import type { EngineStatus } from '@/shared/hooks';

import type { Tone } from './EngineStatus';

export interface HeaderStatus {
  label: string;
  tone: Tone;
}

export function headerStatus({ status, hosted }: { status: EngineStatus; hosted: boolean }): HeaderStatus {
  if (!hosted) return { label: 'Prévia', tone: 'idle' };
  if (status === 'ready') return { label: 'Online', tone: 'ok' };
  if (status === 'crashed') return { label: 'Indisponível', tone: 'bad' };
  return { label: 'Iniciando…', tone: 'pending' };
}
