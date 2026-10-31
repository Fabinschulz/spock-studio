export const AI_ENVIRONMENT_OPTIONS = [
  { id: 'claude-code', name: 'Claude Code', supported: true },
  { id: 'cursor', name: 'Cursor', supported: false },
  { id: 'codex', name: 'OpenAI Codex', supported: false }
] as const;

export const AI_ENVIRONMENT_NAMES: Record<string, string> = Object.fromEntries(
  AI_ENVIRONMENT_OPTIONS.map((option) => [option.id, option.name])
);
