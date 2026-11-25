import { describe, expect, it } from 'vitest';

import { ACTIVITY_WIDTH, clampActivityWidth, maxActivityWidth } from './use-activity-panel';

describe('largura do painel de atividade', () => {
  it('nunca fica menor que o mínimo nem maior que a metade da janela', () => {
    expect(clampActivityWidth(10, 1440)).toBe(ACTIVITY_WIDTH.min);
    expect(clampActivityWidth(5000, 1440)).toBe(720);
    expect(clampActivityWidth(400.4, 1440)).toBe(400);
  });

  it('numa janela estreita, o mínimo ainda vale', () => {
    expect(maxActivityWidth(400)).toBe(ACTIVITY_WIDTH.min);
    expect(clampActivityWidth(600, 400)).toBe(ACTIVITY_WIDTH.min);
  });
});
