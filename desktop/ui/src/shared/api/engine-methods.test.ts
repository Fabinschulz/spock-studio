import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { describe, expect, it } from 'vitest';

import { ENGINE_METHODS } from './engine-client';

/** A lista da interface e a do host Rust são a mesma: uma divergência aparece aqui, não como erro em produção. */
describe('ENGINE_METHODS × FRONTEND_METHODS do host', () => {
  it('pede exatamente os métodos que o host deixa passar', () => {
    const host = readFileSync(resolve(process.cwd(), '../host/src-tauri/src/protocol.rs'), 'utf8');
    const block = /FRONTEND_METHODS: &\[&str\] = &\[([^\]]*)\]/.exec(host)?.[1] ?? '';
    const allowed = [...block.matchAll(/"([a-z.]+)"/g)].map((match) => match[1]);

    expect(allowed.length).toBeGreaterThan(0);
    expect([...ENGINE_METHODS].sort()).toEqual([...allowed].sort());
  });
});
