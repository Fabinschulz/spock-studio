import { describe, expect, it } from 'vitest';

import type { DiagnosticCheck } from './diagnostic-checks';
import { buildDiagnostics, formatDiagnostics } from './diagnostics-report';

describe('buildDiagnostics', () => {
  it('traz a versão e o navegador interno, o que só o suporte usa', () => {
    expect(buildDiagnostics({ appName: 'Spock Studio', version: '1.2.3', userAgent: 'WebView2/130' })).toEqual([
      { label: 'Aplicativo', value: 'Spock Studio v1.2.3' },
      { label: 'Navegador interno', value: 'WebView2/130' }
    ]);
  });
});

describe('formatDiagnostics', () => {
  const engine: DiagnosticCheck = { id: 'engine', title: 'Motor', state: 'ok', summary: 'Pronto.' };

  it('põe as verificações primeiro, depois uma linha por informação no formato "rótulo: valor"', () => {
    expect(
      formatDiagnostics({ checks: [engine], lines: [{ label: 'Aplicativo', value: 'Spock Studio v1.2.3' }] })
    ).toBe('Verificações:\nMotor: Certo. Pronto.\n\nAplicativo: Spock Studio v1.2.3');
  });

  it('acrescenta o registro recente no fim, com a hora em UTC', () => {
    const text = formatDiagnostics({
      lines: [{ label: 'Aplicativo', value: 'Spock Studio v1.2.3' }],
      log: [{ atMs: Date.UTC(2026, 10, 13, 22, 0, 0), source: 'motor', message: 'falhou', count: 2 }]
    });

    expect(text).toBe(
      'Aplicativo: Spock Studio v1.2.3\n\nRegistro recente:\n2026-11-13T22:00:00.000Z [motor] falhou (×2)'
    );
  });
});
