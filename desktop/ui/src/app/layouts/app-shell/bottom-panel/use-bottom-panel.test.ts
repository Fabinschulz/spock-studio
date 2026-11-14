import { describe, expect, it } from 'vitest';

import { clampHeight, PANEL_HEIGHT } from './use-bottom-panel';

describe('altura do painel inferior', () => {
  it('nunca fica menor que o mínimo nem maior que 65% da janela', () => {
    expect(clampHeight(10, 800)).toBe(PANEL_HEIGHT.min);
    expect(clampHeight(2000, 800)).toBe(520);
    expect(clampHeight(300.4, 800)).toBe(300);
  });

  it('numa janela muito baixa, o mínimo ainda vale', () => {
    expect(clampHeight(500, 150)).toBe(PANEL_HEIGHT.min);
  });
});
