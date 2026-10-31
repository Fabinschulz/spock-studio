import type { Discovery, StackFinding } from '@/shared/schema';

import type { Tone } from '@/prototype/types/tag';

export const CONFIDENCE_TEXT: Record<StackFinding['confidence'], string> = {
  high: 'confiança alta',
  medium: 'confiança média',
  low: 'confiança baixa'
};

export const CONFIDENCE_TONE: Record<StackFinding['confidence'], Tone> = {
  high: 'success',
  medium: 'accent',
  low: 'warning'
};

export const INVENTORY_ROWS: readonly [string, keyof Discovery['inventory']][] = [
  ['Agentes', 'agents'],
  ['Skills', 'skills'],
  ['Regras', 'rules'],
  ['Hooks', 'hooks'],
  ['Servidores MCP', 'mcpServers'],
  ['Evals', 'evals']
];

export const TOOL_KIND_TEXT: Record<string, string> = { build: 'Build', test: 'Teste' };
