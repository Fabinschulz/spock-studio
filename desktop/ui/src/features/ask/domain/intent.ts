import { paths } from '@/shared/routing';

/** Os caminhos que tratam um pedido escrito. A entrada só encaminha: quem executa é a tela de destino. */
export type AskKind = 'request' | 'assistant' | 'idea';

export interface AskOption {
  kind: AskKind;
  label: string;
  /** O que acontece ao continuar, em uma frase. */
  description: string;
  to: string;
}

export const ASK_OPTIONS: Readonly<Record<AskKind, AskOption>> = {
  request: {
    kind: 'request',
    label: 'Pedido de engenharia',
    description: 'Analisa o pedido no projeto e mostra o impacto antes de mexer em qualquer arquivo.',
    to: paths.engineeringRequest
  },
  assistant: {
    kind: 'assistant',
    label: 'Pergunta sobre o projeto',
    description: 'Responde sobre andamento, riscos e previsão, separando fato de interpretação.',
    to: paths.assistant
  },
  idea: {
    kind: 'idea',
    label: 'Nova ideia',
    description: 'Transforma a ideia em especificação, arquitetura e backlog, uma etapa por vez.',
    to: paths.idea
  }
};

export const ASK_KINDS: readonly AskKind[] = ['request', 'assistant', 'idea'];

const normalize = (text: string) => text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();

// Sinais simples e explicáveis: a pessoa vê a sugestão e pode trocá-la; nenhum modelo decide por ela.
const ASSISTANT_SIGNALS = [
  'risco',
  'prazo',
  'andamento',
  'previs',
  'entrega',
  'atras',
  'ritmo',
  'marco',
  'status',
  'capacidade',
  'quando ',
  'quanto ',
  'quais ',
  'qual '
];
const IDEA_SIGNALS = ['ideia', 'novo projeto', 'novo sistema', 'novo produto', 'do zero', 'criar um app', 'mvp'];

/** O caminho mais provável para o texto. Sem sinal nenhum, é um pedido de engenharia. */
export function suggestAskKind(text: string): AskKind {
  const normalized = normalize(text);
  if (IDEA_SIGNALS.some((signal) => normalized.includes(signal))) return 'idea';
  if (normalized.trim().endsWith('?') || ASSISTANT_SIGNALS.some((signal) => normalized.includes(signal)))
    return 'assistant';
  return 'request';
}

/** Curto demais para ser um pedido: uma ou duas palavras são busca, não uma frase. */
export function looksLikeRequest(text: string): boolean {
  const words = text.trim().split(/\s+/).filter(Boolean);
  return words.length >= 3 && text.trim().length >= 12;
}

/** Os três caminhos, com o mais provável primeiro. */
export function rankAskOptions(text: string): AskOption[] {
  const first = suggestAskKind(text);
  return [ASK_OPTIONS[first], ...ASK_KINDS.filter((kind) => kind !== first).map((kind) => ASK_OPTIONS[kind])];
}
