import type { Tone } from '@/shared/components';
import type { AiTool } from '@/shared/schema';

/** O que o Spock faz com a ferramenta: o Harness inteiro, só a ligação com o projeto ou nada ainda. */
export type HarnessReach = 'full' | 'partial' | 'none';

interface AiToolDescription {
  state: string;
  tone: Tone;
  detail: string;
  harness: HarnessReach;
}

function stateOf(tool: AiTool): Pick<AiToolDescription, 'state' | 'tone' | 'detail'> {
  // O motor só procura o comando nas pastas de programas do sistema; dizer "não está instalada" seria afirmar demais.
  if (!tool.installed)
    return {
      state: 'Não encontrada',
      tone: 'neutral',
      detail: `O Spock procurou o comando ${tool.binary} nas pastas de programas do sistema e não achou.`
    };
  return {
    state: 'Instalada',
    tone: 'success',
    detail: tool.version ? `Versão ${tool.version}.` : 'Instalada, mas não informou a versão.'
  };
}

// C3: no Cursor e no Codex o Harness grava a ligação com a IDE e a skill do contrato; prometer o Harness inteiro seria falso.
const PARTIAL = new Set(['cursor', 'codex']);

export function describeAiTool(tool: AiTool): AiToolDescription {
  return {
    ...stateOf(tool),
    harness: !tool.harnessSupport ? 'none' : PARTIAL.has(tool.id) ? 'partial' : 'full'
  };
}
