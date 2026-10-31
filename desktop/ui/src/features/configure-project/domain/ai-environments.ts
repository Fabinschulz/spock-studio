export const AI_ENVIRONMENTS = ['claude-code', 'cursor', 'codex'] as const;

export const SUPPORTED_AI_ENVIRONMENT = 'claude-code';

export const aiEnvironmentNames: Record<string, string> = {
  'claude-code': 'Claude Code',
  cursor: 'Cursor',
  codex: 'OpenAI Codex'
};

export const isSupportedEnvironment = (id: string): boolean => id === SUPPORTED_AI_ENVIRONMENT;
