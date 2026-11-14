import { describe, expect, it } from 'vitest';

import { collapseRepeats, formatLogLine } from './diagnostic-log';

const entry = (atMs: number, message: string, source: 'motor' | 'host' = 'motor') => ({ atMs, source, message });

describe('collapseRepeats', () => {
  it('junta linhas iguais em sequência e conta quantas foram', () => {
    const lines = collapseRepeats([entry(1, 'falhou'), entry(2, 'falhou'), entry(3, 'falhou')]);

    expect(lines).toEqual([{ atMs: 3, source: 'motor', message: 'falhou', count: 3 }]);
  });

  it('uma linha igual mas separada por outra não se junta', () => {
    const lines = collapseRepeats([entry(1, 'a'), entry(2, 'b'), entry(3, 'a')]);

    expect(lines.map((line) => line.message)).toEqual(['a', 'b', 'a']);
  });

  it('a mesma mensagem de origens diferentes continua separada', () => {
    const lines = collapseRepeats([entry(1, 'caiu', 'motor'), entry(2, 'caiu', 'host')]);

    expect(lines).toHaveLength(2);
  });

  it('não altera a lista recebida', () => {
    const entries = [entry(1, 'a'), entry(2, 'a')];

    collapseRepeats(entries);

    expect(entries).toEqual([entry(1, 'a'), entry(2, 'a')]);
  });
});

describe('formatLogLine', () => {
  it('traz a hora em UTC, a origem e a mensagem', () => {
    expect(formatLogLine({ ...entry(Date.UTC(2026, 10, 13, 22, 15, 6), 'motor pronto', 'host'), count: 1 })).toBe(
      '2026-11-13T22:15:06.000Z [host] motor pronto'
    );
  });

  it('diz quantas vezes a linha se repetiu', () => {
    expect(formatLogLine({ ...entry(0, 'falhou'), count: 4 })).toContain('falhou (×4)');
  });
});
