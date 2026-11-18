export const AI_ENVIRONMENTS = ['claude-code', 'cursor', 'codex'] as const;

export const SUPPORTED_AI_ENVIRONMENTS: readonly string[] = ['claude-code', 'cursor', 'codex'];

export const aiEnvironmentNames: Record<string, string> = {
  'claude-code': 'Claude Code',
  cursor: 'Cursor',
  codex: 'OpenAI Codex'
};

export const isSupportedEnvironment = (id: string): boolean => SUPPORTED_AI_ENVIRONMENTS.includes(id);

/** C3: no Cursor e no Codex o Spock grava só a ligação com a IDE; agentes, regras e skills ainda são do Claude Code. */
export const partialEnvironmentNote: Record<string, string> = {
  cursor: 'só a ligação com a IDE',
  codex: 'só a ligação com a IDE'
};
