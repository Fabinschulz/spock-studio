import type { Command } from './commands';

const normalize = (text: string) => text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();

function tokenScore(label: string, token: string): number {
  if (label.startsWith(token)) return 3;
  if (label.includes(` ${token}`)) return 2;
  return label.includes(token) ? 1 : 0;
}

function scoreOf(command: Command, tokens: readonly string[]): number | null {
  const label = normalize(command.label);
  const searchable = `${label} ${normalize(command.hint)} ${normalize(command.keywords)}`;
  if (!tokens.every((token) => searchable.includes(token))) return null;
  return tokens.reduce((total, token) => total + tokenScore(label, token), 0);
}

/** Sem texto, tudo na ordem de origem; com texto, só o que contém todas as palavras, o título primeiro. */
export function searchCommands(commands: readonly Command[], query: string): Command[] {
  const tokens = normalize(query).split(/\s+/).filter(Boolean);
  if (tokens.length === 0) return [...commands];

  return commands
    .map((command, index) => ({ command, index, score: scoreOf(command, tokens) }))
    .filter((entry): entry is typeof entry & { score: number } => entry.score !== null)
    .sort((a, b) => b.score - a.score || a.index - b.index)
    .map((entry) => entry.command);
}
