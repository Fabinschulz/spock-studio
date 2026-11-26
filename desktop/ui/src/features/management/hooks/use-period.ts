import { readPreference, writePreference } from '@/shared/utils';
import { useCallback, useState } from 'react';

export const PERIOD_WEEKS = [2, 4, 8, 12] as const;
/** O mesmo padrão do motor: a previsão usa até 12 semanas de histórico, e 4 deixa a média semanal instável. */
export const DEFAULT_PERIOD_WEEKS = 8;

const KEY = 'spock.management.periodWeeks';

function stored(): number {
  const value = Number(readPreference(KEY));
  return (PERIOD_WEEKS as readonly number[]).includes(value) ? value : DEFAULT_PERIOD_WEEKS;
}

/** Um período só para o Acompanhamento e o Relatório: o que se escolhe numa tela vale na outra. */
export function usePeriod(): [number, (weeks: number) => void] {
  const [weeks, setWeeks] = useState(stored);
  const choose = useCallback((value: number) => {
    setWeeks(value);
    writePreference(KEY, String(value));
  }, []);
  return [weeks, choose];
}
