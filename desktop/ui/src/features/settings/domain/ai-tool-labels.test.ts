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

  it('não instalada fica neutra: faltar uma ferramenta não é erro', () => {
    expect(describeAiTool(tool({ installed: false, version: null }))).toMatchObject({
      state: 'Não encontrada',
      tone: 'neutral'
    });
  });

  it('diz se o Spock configura o Harness da ferramenta', () => {
    expect(describeAiTool(tool({ id: 'claude-code', harnessSupport: true })).harness).toBe(
      'O Spock configura o Harness para esta ferramenta.'
    );
    expect(describeAiTool(tool({ harnessSupport: false })).harness).toContain('ainda não configura');
    expect(describeAiTool(tool({ id: 'codex', harnessSupport: true })).harness).toContain(
      'ainda são só do Claude Code'
    );
    expect(describeAiTool(tool({ id: 'cursor', harnessSupport: true })).harness).toContain('.cursor/rules');
  });
});
