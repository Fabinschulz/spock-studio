import type { Discovery } from '@/shared/schema';

export const AI_ENVIRONMENTS = ['claude-code', 'cursor', 'codex'] as const;

export const SUPPORTED_AI_ENVIRONMENTS: readonly string[] = ['claude-code', 'cursor', 'codex'];

export const aiEnvironmentNames: Record<string, string> = {
  'claude-code': 'Claude Code',
  cursor: 'Cursor',
  codex: 'OpenAI Codex'
};

export const isSupportedEnvironment = (id: string): boolean => SUPPORTED_AI_ENVIRONMENTS.includes(id);

/** C3: no Cursor e no Codex o Spock grava a ligação com a IDE e a skill do contrato; agentes e regras ainda são do Claude Code. */
export const partialEnvironmentNote: Record<string, string> = {
  cursor: 'ligação com a IDE e skill',
  codex: 'ligação com a IDE e skill'
};

const order = (id: string) => {
  const index = (AI_ENVIRONMENTS as readonly string[]).indexOf(id);
  return index < 0 ? AI_ENVIRONMENTS.length : index;
};

/** Na ordem da lista da tela: com o Claude Code marcado, ele é o principal de um projeto novo. */
export const inEnvironmentOrder = (ids: readonly string[]): string[] =>
  [...new Set(ids)].sort((a, b) => order(a) - order(b));

export const toggleEnvironment = (selected: readonly string[], id: string): string[] =>
  inEnvironmentOrder(selected.includes(id) ? selected.filter((item) => item !== id) : [...selected, id]);

/**
 * O ponto de partida da escolha. A declaração manda; sem ela, as IDEs detectadas com evidência forte. Um AGENTS.md
 * sozinho (confiança baixa) não marca o Codex: o arquivo é lido por várias ferramentas.
 */
export function initialEnvironments(discovery: Discovery): string[] {
  if (discovery.declaredAiEnvironments.length > 0) return inEnvironmentOrder(discovery.declaredAiEnvironments);
  return inEnvironmentOrder(
    discovery.aiEnvironments.filter((e) => e.supported && e.confidence !== 'low').map((e) => e.id)
  );
}

/**
 * Tirar o Claude Code de um projeto já configurado ainda não é feito pelo plano: levaria agentes, regras e a estrutura
 * de especificação. As outras IDEs saem, e o plano tira o que o Spock gravou para elas.
 */
export const isLockedEnvironment = (discovery: Discovery, id: string): boolean =>
  id === 'claude-code' && discovery.declaredAiEnvironments.includes(id);
