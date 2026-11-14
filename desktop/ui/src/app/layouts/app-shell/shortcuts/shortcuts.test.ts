import { describe, expect, it } from 'vitest';

import { shortcutLabel, shortcutOf } from './shortcuts';

const press = (over: Partial<KeyboardEvent>) =>
  shortcutOf({ ctrlKey: true, metaKey: false, altKey: false, shiftKey: false, key: '', code: '', ...over });

describe('atalhos do shell', () => {
  it('reconhece cada atalho pela letra, com Ctrl ou ⌘', () => {
    expect(press({ key: 'k' })).toBe('palette');
    expect(press({ key: 'B' })).toBe('rail');
    expect(press({ key: 'j' })).toBe('panel');
    expect(press({ key: '/' })).toBe('shortcuts');
    expect(press({ ctrlKey: false, metaKey: true, key: 'k' })).toBe('palette');
  });

  it('o terminal abre pela tecla física, mesmo no ABNT2, onde a crase é tecla morta', () => {
    expect(press({ key: 'Dead', code: 'Backquote' })).toBe('terminal');
    expect(press({ key: '`', code: 'Backquote' })).toBe('terminal');
  });

  it('sem o modificador, ou com Shift ou Alt, não é atalho do shell', () => {
    expect(press({ ctrlKey: false, key: 'k' })).toBeNull();
    expect(press({ shiftKey: true, key: 'k' })).toBeNull();
    expect(press({ altKey: true, key: 'j' })).toBeNull();
    expect(press({ key: 'x' })).toBeNull();
  });

  it('o rótulo mostra a combinação que a pessoa aperta', () => {
    expect(shortcutLabel('panel')).toMatch(/^(Ctrl|⌘) J$/);
  });
});
