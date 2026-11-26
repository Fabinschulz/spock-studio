import type { ProjectReport } from '@/shared/schema';
import { plural, runKindLabel, runStateLabel } from '@/shared/utils';

/** Os mesmos títulos e frases no Acompanhamento, no Relatório e no texto copiado: a ordem e os nomes não divergem. */
export const flowTitle = (weeks: number) => `Fluxo nas últimas ${plural(weeks, 'semana', 'semanas')}`;

export const FORECAST_TITLE = 'Previsão de entrega';
export const RISKS_TITLE = 'Riscos';

export const NO_DATA = {
  forecast: 'Sem dados para calcular a previsão ainda.',
  flow: 'Sem dados para calcular o fluxo ainda.',
  risks: 'Sem dados para detectar riscos ainda.'
} as const;

/** Tipo e estado são rótulos, e não uma frase: o estado no masculino não concorda com "investigação" ou "configuração". */
export function describeRunCount(run: ProjectReport['runs'][number]): string {
  return `${runKindLabel(run.kind)} · ${runStateLabel(run.state)} · ${plural(run.count, 'execução', 'execuções')}`;
}
