import type { PortfolioProject } from '@/shared/schema';

/** O motor marca o projeto sem ferramenta com este sinal; é falta de dado, não um ponto de atenção sobre o projeto. */
export const NO_SOURCES = 'no-sources';

export function attentionSignals(signals: PortfolioProject['signals']): PortfolioProject['signals'] {
  return signals.filter((signal) => signal.id !== NO_SOURCES);
}
