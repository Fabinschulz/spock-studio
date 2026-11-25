import { describe, expect, it } from 'vitest';

import { handoffState, readHandoff } from './handoff';

describe('repasse do pedido entre telas', () => {
  it('o texto vai aparado e volta como foi escrito', () => {
    expect(readHandoff(handoffState('  trocar a autenticação  '))).toBe('trocar a autenticação');
  });

  it.each([null, undefined, 'texto solto', 42, {}, { requestText: 7 }, { requestText: '   ' }])(
    'um estado que não é um pedido (%j) é ignorado',
    (state) => {
      expect(readHandoff(state)).toBeNull();
    }
  );
});
