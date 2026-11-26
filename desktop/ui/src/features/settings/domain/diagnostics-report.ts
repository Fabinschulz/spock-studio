import { formatChecks, type DiagnosticCheck } from './diagnostic-checks';
import { formatLogLine, type LogLine } from './diagnostic-log';

export interface DiagnosticsLine {
  label: string;
  value: string;
}

interface DiagnosticsInput {
  appName: string;
  version: string;
  userAgent: string;
}

/** O que só o suporte usa; o estado do motor e da configuração está nas verificações. */
export function buildDiagnostics({
  appName,
  version,
  userAgent
}: DiagnosticsInput): [version: DiagnosticsLine, browser: DiagnosticsLine] {
  return [
    { label: 'Aplicativo', value: `${appName} v${version}` },
    { label: 'Navegador interno', value: userAgent }
  ];
}

interface Report {
  checks?: readonly DiagnosticCheck[];
  lines: readonly DiagnosticsLine[];
  log?: readonly LogLine[];
}

export function formatDiagnostics({ checks = [], lines, log = [] }: Report): string {
  const parts = [
    checks.length > 0 ? `Verificações:\n${formatChecks(checks)}` : '',
    lines.map(({ label, value }) => `${label}: ${value}`).join('\n'),
    log.length > 0 ? `Registro recente:\n${log.map(formatLogLine).join('\n')}` : ''
  ];
  return parts.filter(Boolean).join('\n\n');
}
