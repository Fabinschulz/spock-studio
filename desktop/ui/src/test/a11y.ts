import axe from 'axe-core';
import { expect } from 'vitest';

/**
 * Regras de acessibilidade do axe (ARIA, nomes, rótulos, semântica, landmarks) sobre o DOM renderizado.
 * O contraste fica de fora porque o jsdom não calcula estilos: ele é validado nos tokens (tokens.contrast.test.ts) e
 * conferido à mão na interface. Uma execução sem violações NÃO prova que a tela é acessível.
 */
export async function expectNoA11yViolations(root: Element = document.body): Promise<void> {
  const results = await axe.run(root, {
    rules: { 'color-contrast': { enabled: false }, region: { enabled: false } }
  });
  const found = results.violations.map(
    (v) => `${v.id}: ${v.help} -> ${v.nodes.map((n) => n.target.join(' ')).join(' | ')}`
  );
  expect(found).toEqual([]);
}
