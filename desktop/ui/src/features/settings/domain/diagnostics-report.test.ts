import { describe, expect, it } from 'vitest';

import { buildDiagnostics, formatDiagnostics } from './diagnostics-report';

const base = { appName: 'Spock Studio', version: '1.2.3', userAgent: 'WebView2/130' };

describe('buildDiagnostics', () => {
  it('descreve o aplicativo, o motor e a inteligência local com as palavras da tela', () => {
    const lines = buildDiagnostics({
      ...base,
      engine: { hosted: true, status: 'ready', aiRuntime: 'stopped' }
    });

    expect(lines).toEqual([
      { label: 'Aplicativo', value: 'Spock Studio v1.2.3' },
      { label: 'Motor local', value: 'Pronto' },
      { label: 'Inteligência local', value: 'Parada' },
      { label: 'Navegador interno', value: 'WebView2/130' }
    ]);
  });

  it('um motor que caiu aparece como indisponível, nunca como pronto', () => {
    const lines = buildDiagnostics({ ...base, engine: { hosted: true, status: 'crashed', aiRuntime: 'unknown' } });

    expect(lines.find((line) => line.label === 'Motor local')?.value).toBe('Indisponível');
  });

  it('fora do aplicativo desktop não finge que há motor', () => {
    const lines = buildDiagnostics({ ...base, engine: { hosted: false, status: 'unknown', aiRuntime: 'unknown' } });

    expect(lines.filter((line) => line.value === 'Indisponível na prévia do navegador')).toHaveLength(2);
  });
});

describe('formatDiagnostics', () => {
  it('põe uma linha por informação, no formato "rótulo: valor"', () => {
    expect(
      formatDiagnostics([
        { label: 'Aplicativo', value: 'Spock Studio v1.2.3' },
        { label: 'Motor local', value: 'Pronto' }
      ])
    ).toBe('Aplicativo: Spock Studio v1.2.3\nMotor local: Pronto');
  });
});
