import { describe, expect, it } from 'vitest';

import type { AiTool } from '@/shared/schema';

import { describeAiTool } from './ai-tool-labels';

const tool = (overrides: Partial<AiTool>): AiTool => ({
  id: 'codex',
  name: 'Codex CLI',
  binary: 'codex',
  installed: true,
  version: '0.46.0',
  harnessSupport: false,
  ...overrides
});

describe('describeAiTool', () => {
  it('instalada mostra a versão', () => {
    expect(describeAiTool(tool({}))).toMatchObject({ state: 'Instalada', tone: 'success', detail: 'Versão 0.46.0.' });
  });

  it('instalada sem versão diz isso, em vez de inventar um número', () => {
    expect(describeAiTool(tool({ version: null })).detail).toBe('Instalada, mas não informou a versão.');
  });

  it('não encontrada fica neutra e diz onde o Spock procurou, sem afirmar que não está instalada', () => {
    const missing = describeAiTool(tool({ installed: false, version: null }));
    expect(missing).toMatchObject({ state: 'Não encontrada', tone: 'neutral' });
    expect(missing.detail).toBe('O Spock procurou o comando codex nas pastas de programas do sistema e não achou.');
  });

  it('diz até onde o Spock configura a ferramenta', () => {
    expect(describeAiTool(tool({ id: 'claude-code', harnessSupport: true })).harness).toBe('full');
    expect(describeAiTool(tool({ harnessSupport: false })).harness).toBe('none');
    expect(describeAiTool(tool({ id: 'codex', harnessSupport: true })).harness).toBe('partial');
    expect(describeAiTool(tool({ id: 'cursor', harnessSupport: true })).harness).toBe('partial');
  });
});
