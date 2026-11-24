import { describe, expect, it } from 'vitest';

import { formatEngineDate, formatRecentDate, formatRecentDateTime } from './format-date';

describe('datas do motor e dos recentes', () => {
  const now = new Date(2026, 10, 10, 18, 0).getTime();

  it('escreve a data do motor para o meio de uma frase e não inventa data para texto que não é data', () => {
    expect(formatEngineDate(new Date(2026, 10, 10, 9, 5).toISOString(), now)).toBe('hoje às 09:05');
    expect(formatEngineDate(new Date(2026, 10, 9, 16, 20).toISOString(), now)).toBe('ontem às 16:20');
    expect(formatEngineDate(new Date(2026, 9, 23, 10, 15).toISOString(), now)).toBe('23/10/2026 às 10:15');
    expect(formatEngineDate('sem data', now)).toBe('sem data');
  });

  it('chama de hoje e ontem o que é recente e mostra a data do resto', () => {
    expect(formatRecentDate(new Date(2026, 10, 10, 0, 1).getTime(), now)).toBe('Hoje');
    expect(formatRecentDate(new Date(2026, 10, 9, 23, 59).getTime(), now)).toBe('Ontem');
    expect(formatRecentDateTime(new Date(2026, 9, 23, 10, 15).getTime(), now)).toBe('23/10/2026 · 10:15');
  });
});
