import { describe, expect, it } from 'vitest';

import { formatElapsed } from './format-elapsed';

describe('formatElapsed', () => {
  it.each([
    [0, '0:00'],
    [9_400, '0:09'],
    [75_000, '1:15']
  ])('%i ms → %s', (ms, text) => {
    expect(formatElapsed(ms)).toBe(text);
  });
});
