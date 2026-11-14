import type { DiagnosticLogEntry } from '../service/diagnostic-log-api';

export interface LogLine extends DiagnosticLogEntry {
  count: number;
}

/** Uma falha que se repete a cada segundo não pode empurrar as outras linhas para fora da tela. */
export function collapseRepeats(entries: readonly DiagnosticLogEntry[]): LogLine[] {
  return entries.reduce<LogLine[]>((lines, entry) => {
    const last = lines.at(-1);
    if (last && last.source === entry.source && last.message === entry.message) {
      last.count += 1;
      last.atMs = entry.atMs;
    } else {
      lines.push({ ...entry, count: 1 });
    }
    return lines;
  }, []);
}

export function formatLogLine(line: LogLine): string {
  const repeated = line.count > 1 ? ` (×${line.count})` : '';
  return `${new Date(line.atMs).toISOString()} [${line.source}] ${line.message}${repeated}`;
}
