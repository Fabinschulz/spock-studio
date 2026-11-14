import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { useMediaQuery } from './use-media-query';

const original = window.matchMedia;

afterEach(() => {
  window.matchMedia = original;
});

describe('useMediaQuery', () => {
  it('sem matchMedia responde que não casa', () => {
    const { result } = renderHook(() => useMediaQuery('(max-width: 800px)'));

    expect(result.current).toBe(false);
  });

  it('acompanha a janela ao ser redimensionada', () => {
    let narrow = false;
    window.matchMedia = vi.fn(() => ({ matches: narrow })) as unknown as typeof window.matchMedia;
    const { result } = renderHook(() => useMediaQuery('(max-width: 800px)'));
    expect(result.current).toBe(false);

    narrow = true;
    act(() => {
      window.dispatchEvent(new Event('resize'));
    });

    expect(result.current).toBe(true);
  });
});
