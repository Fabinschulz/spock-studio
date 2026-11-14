import type { EngineReport } from '@/shared/hooks';

export interface DiagnosticsLine {
  label: string;
  value: string;
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
  crashed: 'Indisponível'
};

const AI_RUNTIME: Record<EngineReport['aiRuntime'], string> = {
  unknown: 'Verificando',
  ready: 'Pronta',
  stopped: 'Parada',
  unavailable: 'Indisponível'
};

const NO_HOST = 'Indisponível na prévia do navegador';

export function buildDiagnostics({ appName, version, userAgent, engine }: DiagnosticsInput): DiagnosticsLine[] {
  return [
    { label: 'Aplicativo', value: `${appName} v${version}` },
    { label: 'Motor local', value: engine.hosted ? ENGINE[engine.status] : NO_HOST },
    { label: 'Inteligência local', value: engine.hosted ? AI_RUNTIME[engine.aiRuntime] : NO_HOST },
    { label: 'Navegador interno', value: userAgent }
  ];
}

export function formatDiagnostics(lines: readonly DiagnosticsLine[]): string {
  return lines.map(({ label, value }) => `${label}: ${value}`).join('\n');
}
