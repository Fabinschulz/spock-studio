import { describe, expect, it } from 'vitest';

import { plural } from './plural';

describe('plural', () => {
  it('usa o singular só para um', () => {
    expect(plural(1, 'arquivo', 'arquivos')).toBe('1 arquivo');
    expect(plural(0, 'arquivo', 'arquivos')).toBe('0 arquivos');
    expect(plural(3, 'alteração local', 'alterações locais')).toBe('3 alterações locais');
  });
});
