import { describe, expect, it } from 'vitest';

import { isWithin } from './paths';

describe('isWithin', () => {
  it('vale para o próprio caminho e para tudo abaixo dele', () => {
    expect(isWithin('/gestao', '/gestao')).toBe(true);
    expect(isWithin('/gestao/assistente', '/gestao')).toBe(true);
  });

  it('não confunde um caminho que só começa com as mesmas letras', () => {
    expect(isWithin('/gestao-antiga', '/gestao')).toBe(false);
  });
});
