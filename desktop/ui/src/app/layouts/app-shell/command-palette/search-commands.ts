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

// Palavras curtas, as de ligação e as que valem para qualquer tela ("projeto") não dizem para onde a pessoa quer ir.
const MIN_WORD = 4;
// Na descrição a palavra precisa ser mais específica: frases longas têm palavras comuns demais.
const MIN_DESCRIPTION_WORD = 5;
const STEM = 5;
const STOPWORDS = [
  'para',
  'como',
  'quero',
  'preciso',
  'gostaria',
  'meus',
  'minha',
  'minhas',
  'esse',
  'essa',
  'este',
  'esta',
  'isso',
  'sobre',
  'todos',
  'todas',
  'mostre',
  'mostrar',
  'veja',
  'fazer',
  'favor',
  'voce',
  'projeto',
  'spock',
  'studio'
];

const stem = (word: string) => word.slice(0, STEM);
const STOP_STEMS = new Set(STOPWORDS.map(stem));

const stemsOf = (text: string, minLength = MIN_WORD) =>
  new Set(
    normalize(text)
      .split(/[^a-z0-9]+/)
      .filter((word) => word.length >= minLength)
      .map(stem)
  );

const WEIGHT = { label: 3, keywords: 2, description: 1 };

/**
 * Para uma frase, não para um nome: ignora as palavras de ligação e compara pelo começo da palavra, de modo que
 * "configure as integrações" ache a tela de integrações. Devolve só o que tem relação com o texto.
 */
export function suggestCommands(commands: readonly Command[], sentence: string, limit = 3): Command[] {
  const wanted = [...stemsOf(sentence)].filter((word) => !STOP_STEMS.has(word));
  if (wanted.length === 0) return [];

  return commands
    .map((command, index) => {
      const label = stemsOf(command.label);
      const keywords = stemsOf(command.keywords);
      const description = stemsOf(command.description ?? '', MIN_DESCRIPTION_WORD);
      const score = wanted.reduce((total, word) => {
        if (label.has(word)) return total + WEIGHT.label;
        if (keywords.has(word)) return total + WEIGHT.keywords;
        return description.has(word) ? total + WEIGHT.description : total;
      }, 0);
      return { command, index, score };
    })
    .filter((entry) => entry.score > 0)
    .sort((a, b) => b.score - a.score || a.index - b.index)
    .slice(0, limit)
    .map((entry) => entry.command);
}
