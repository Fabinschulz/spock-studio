import type { AssistantAnswer } from '@/shared/schema';

export const MIN_QUESTION = 3;
export const MAX_QUESTION = 500;

/** O que falta na pergunta para o botão habilitar; `null` quando ela já serve. */
export function questionHint(question: string): string | null {
  const length = question.trim().length;
  if (length < MIN_QUESTION) return `Escreva a pergunta, de ${MIN_QUESTION} a ${MAX_QUESTION} caracteres.`;
  if (length > MAX_QUESTION) return `A pergunta passou de ${MAX_QUESTION} caracteres.`;
  return null;
}

export const SUGGESTED_QUESTIONS = [
  'Quais entregas estão comprometidas?',
  'Quando provavelmente concluiremos este projeto?',
  'Quais são os principais riscos agora?',
  'O ritmo de entrega está caindo?',
  'Qual seria o impacto de adicionar mais capacidade?'
] as const;

export const ANSWER_SECTIONS: readonly {
  key: keyof Pick<AssistantAnswer, 'observed' | 'calculations' | 'inferences' | 'recommendations'>;
  label: string;
  hint: string;
}[] = [
  { key: 'observed', label: 'Dados observados', hint: 'O que os dados sincronizados mostram.' },
  { key: 'calculations', label: 'Cálculos', hint: 'Indicadores, previsão e riscos calculados pelo sistema.' },
  { key: 'inferences', label: 'Inferências', hint: 'Interpretações do modelo a partir dos fatos; não são fatos.' },
  { key: 'recommendations', label: 'Recomendações', hint: 'Ações possíveis, ligadas aos fatos citados.' }
];
