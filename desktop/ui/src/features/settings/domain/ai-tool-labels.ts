import type { Tone } from '@/shared/components';
import type { AiTool } from '@/shared/schema';

interface AiToolDescription {
  state: string;
  tone: Tone;
  detail: string;
  harness: string;
}

function stateOf(tool: AiTool): Pick<AiToolDescription, 'state' | 'tone' | 'detail'> {
  if (!tool.installed)
    return { state: 'Não encontrada', tone: 'neutral', detail: 'Não está instalada neste computador.' };
  return {
    state: 'Instalada',
    tone: 'success',
    detail: tool.version ? `Versão ${tool.version}.` : 'Instalada, mas não informou a versão.'
  };
}

// C3: no Cursor e no Codex o Harness grava a ligação com a IDE e a skill do contrato; prometer o Harness inteiro seria falso.
const partialHarness: Record<string, string> = {
  cursor:
    'O Spock liga esta ferramenta ao projeto (regra em .cursor/rules, MCP, hook de sessão e a skill do contrato em .agents/skills); agentes e regras ainda são só do Claude Code.',
  codex:
    'O Spock liga esta ferramenta ao projeto (bloco no AGENTS.md, MCP, hooks e a skill do contrato em .agents/skills); agentes e regras ainda são só do Claude Code.'
};

export function describeAiTool(tool: AiTool): AiToolDescription {
  return {
    ...stateOf(tool),
    harness: !tool.harnessSupport
      ? 'O Spock detecta esta ferramenta, mas ainda não configura o Harness dela.'
      : (partialHarness[tool.id] ?? 'O Spock configura o Harness para esta ferramenta.')
  };
}
