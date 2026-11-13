import type { AssistantAnswer } from '@/shared/schema';

export const MIN_QUESTION = 3;
export const MAX_QUESTION = 500;

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
