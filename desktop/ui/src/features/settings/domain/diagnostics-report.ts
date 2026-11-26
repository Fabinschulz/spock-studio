import type { EngineReport } from '@/shared/hooks';

import type { GlossaryId } from '@/shared/components';

import { formatLogLine, type LogLine } from './diagnostic-log';

export interface DiagnosticsLine {
  label: string;
  value: string;
  /** O termo do glossário que explica o rótulo na tela; o texto copiado leva só o rótulo. */
  term?: GlossaryId;
}

interface DiagnosticsInput {
  appName: string;
  version: string;
  userAgent: string;
  engine: EngineReport & { hosted: boolean };
}

const ENGINE: Record<EngineReport['status'], string> = {
  unknown: 'Verificando',
  starting: 'Iniciando',
  ready: 'Pronto',
  crashed: 'Indisponível. Ele parou e não voltou sozinho: feche e abra o aplicativo.'
};

const AI_RUNTIME: Record<EngineReport['aiRuntime'], string> = {
  unknown: 'Verificando',
  ready: 'Pronta',
  stopped: 'Parada. É o normal: ela liga no primeiro pedido que precisa dela.',
  unavailable: 'Indisponível'
};

const NO_HOST = 'Indisponível na prévia do navegador';

// Com o motor parado ninguém consulta a inteligência local: "verificando" ficaria na tela para sempre.
function aiRuntimeOf(engine: DiagnosticsInput['engine']): string {
  if (!engine.hosted) return NO_HOST;
  if (engine.status === 'crashed') return 'Depende do motor, que está parado.';
  return AI_RUNTIME[engine.aiRuntime];
}

export function buildDiagnostics({ appName, version, userAgent, engine }: DiagnosticsInput): DiagnosticsLine[] {
  return [
    { label: 'Aplicativo', value: `${appName} v${version}` },
    { label: 'Motor local', value: engine.hosted ? ENGINE[engine.status] : NO_HOST, term: 'engine' },
    { label: 'Inteligência local', value: aiRuntimeOf(engine), term: 'localIntelligence' },
    { label: 'Navegador interno', value: userAgent }
  ];
}

export function formatDiagnostics(lines: readonly DiagnosticsLine[], log: readonly LogLine[] = []): string {
  const report = lines.map(({ label, value }) => `${label}: ${value}`).join('\n');
  return log.length === 0 ? report : `${report}\n\nRegistro recente:\n${log.map(formatLogLine).join('\n')}`;
}
