import type { RunResult } from '@/shared/schema';
import { HARNESS_ENGINEER_NOT_INVOKED } from './harness-engineer-prompt';

type Outcome = RunResult['outcome'];

export type OutcomeSeverity = 'success' | 'warning' | 'error' | 'info';

export const outcomeSeverity: Record<Outcome, OutcomeSeverity> = {
  completed: 'success',
  completed_with_limitations: 'warning',
  cancelled: 'info',
  failed: 'error',
  requires_intervention: 'error'
};

function explainFailure(result: RunResult): string {
  if (result.error) {
    return result.rolledBack
      ? 'A aplicação falhou e as alterações já feitas foram desfeitas.'
      : 'A aplicação falhou antes de alterar arquivos.';
  }
  return result.changesApplied
    ? 'As alterações foram aplicadas, mas uma verificação obrigatória falhou. Revise os Evals reprovados ou reverta as alterações.'
    : 'A execução falhou.';
}

/** Diz o que de fato aconteceu com os arquivos; nunca um sucesso genérico. */
export function explainOutcome(result: RunResult): string {
  switch (result.outcome) {
    case 'completed':
      return 'Todas as verificações obrigatórias foram aprovadas com evidência.';
    case 'completed_with_limitations':
      return 'A configuração foi concluída, mas há itens que não puderam ser verificados ou que dependem de você. Veja os detalhes abaixo.';
    case 'cancelled':
      return result.rolledBack
        ? 'A execução foi cancelada e as alterações já feitas foram desfeitas.'
        : 'A execução foi cancelada.';
    case 'requires_intervention':
      return 'A aplicação falhou e algumas alterações não puderam ser desfeitas sozinhas: os arquivos listados em pendências foram mantidos como estão.';
    case 'failed':
      return explainFailure(result);
  }
}

/** O agente só existe no projeto depois de aplicado: enquanto o plano usou as regras-base, falta pedir a proposta. */
export function awaitingHarnessProposal(apply: RunResult | null, revert: RunResult | null): boolean {
  return (
    apply !== null &&
    apply.changesApplied === true &&
    !apply.rolledBack &&
    revert?.outcome !== 'completed' &&
    (apply.limitations ?? []).some((limitation) => limitation.code === HARNESS_ENGINEER_NOT_INVOKED)
  );
}
