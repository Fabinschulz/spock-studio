import { paths } from '@/shared/routing';

/** Os caminhos que tratam um pedido escrito. A entrada só encaminha: quem executa é a tela de destino. */
export type AskKind = 'request' | 'agent' | 'assistant' | 'idea';

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
  agent: {
    kind: 'agent',
    label: 'Pergunta sobre o código',
    description: 'Um agente de IA instalado neste computador lê o projeto e responde, sem mudar arquivo nenhum.',
    to: paths.agents
  },
  assistant: {
    kind: 'assistant',
    label: 'Pergunta sobre o andamento',
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

export const ASK_KINDS: readonly AskKind[] = ['request', 'agent', 'assistant', 'idea'];

const normalize = (text: string) => text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();

// Sinais simples e explicáveis: a pessoa vê a sugestão e pode trocá-la; nenhum modelo decide por ela.
const IDEA_SIGNALS = ['ideia', 'novo projeto', 'novo sistema', 'novo produto', 'do zero', 'criar um app', 'mvp'];
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
  'conclui'
];
const AGENT_SIGNALS = ['explique', 'explica', 'como funciona', 'onde fica', 'onde esta', 'o que faz', 'resuma'];

const hasAny = (text: string, signals: readonly string[]) => signals.some((signal) => text.includes(signal));

/**
 * O caminho mais provável para o texto. Gestão tem vocabulário próprio (prazo, risco, entrega); uma pergunta sem ele é
 * sobre o código. Sem sinal nenhum, é um pedido de engenharia.
 */
export function suggestAskKind(text: string): AskKind {
  const normalized = normalize(text).trim();
  if (hasAny(normalized, IDEA_SIGNALS)) return 'idea';
  if (hasAny(normalized, ASSISTANT_SIGNALS)) return 'assistant';
  if (normalized.endsWith('?') || hasAny(normalized, AGENT_SIGNALS)) return 'agent';
  return 'request';
}

/** Curto demais para ser um pedido: uma ou duas palavras são busca, não uma frase. */
export function looksLikeRequest(text: string): boolean {
  const words = text.trim().split(/\s+/).filter(Boolean);
  return words.length >= 3 && text.trim().length >= 12;
}

/** Todos os caminhos, com o mais provável primeiro. */
export function rankAskOptions(text: string): AskOption[] {
  const first = suggestAskKind(text);
  return [ASK_OPTIONS[first], ...ASK_KINDS.filter((kind) => kind !== first).map((kind) => ASK_OPTIONS[kind])];
}

/** O que cada caminho precisa ter pronto. `null` enquanto o motor não respondeu: sem resposta, não há aviso. */
export interface AskReadiness {
  modelConfigured: boolean | null;
  agentInstalled: boolean | null;
}

export interface AskNotice {
  text: string;
  /** Onde resolver, quando há uma tela para isso. */
  action?: { label: string; to: string };
}

/**
 * O aviso aparece só para o caminho escolhido e só quando falta algo que ele usa: quem pede uma análise local não
 * precisa ouvir sobre modelo de IA.
 */
export function askNotice(kind: AskKind, readiness: AskReadiness): AskNotice | null {
  if (kind === 'assistant' && readiness.modelConfigured === false)
    return {
      text: 'Este caminho usa um modelo de IA para escrever a explicação, e nenhum está conectado. Sem ele, só a investigação em etapas funciona.',
      action: { label: 'Conectar um modelo de IA', to: paths.connectionsModels }
    };
  if (kind === 'agent' && readiness.agentInstalled === false)
    return {
      text: 'Este caminho usa uma ferramenta de IA instalada neste computador, e nenhuma foi encontrada. Ao continuar, você vê quais são aceitas.'
    };
  return null;
}
