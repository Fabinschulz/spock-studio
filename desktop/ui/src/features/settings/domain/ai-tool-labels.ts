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

export function describeAiTool(tool: AiTool): AiToolDescription {
  return {
    ...stateOf(tool),
    harness: tool.harnessSupport
      ? 'O Spock configura o Harness para esta ferramenta.'
      : 'O Spock detecta esta ferramenta, mas ainda não configura o Harness dela.'
  };
}
