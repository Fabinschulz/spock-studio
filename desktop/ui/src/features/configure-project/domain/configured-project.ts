import type { HarnessStatus, RunSummary } from '@/shared/schema';
import { plural } from '@/shared/utils';

export interface ConfiguredProject {
  status: HarnessStatus;
  /** A aplicação que o motor aceitaria reverter agora; `null` quando reverter daqui não daria certo. */
  revertible: RunSummary | null;
}

// Espelha RunKinds.IsRevertible do motor: só essas execuções gravam no projeto pelo diário.
const REVERTIBLE_KINDS = new Set(['apply', 'security', 'idea', 'workflow', 'infrastructure']);

/**
 * O motor só reverte a execução mais recente que ainda tem alterações no projeto. Se ela não for uma aplicação do
 * Configurar (uma correção de segurança, por exemplo), o atalho não aparece: o motor recusaria.
 */
export function revertibleApply(runs: readonly RunSummary[]): RunSummary | null {
  const latest = [...runs]
    .sort((a, b) => Date.parse(b.startedAt) - Date.parse(a.startedAt))
    .find((run) => REVERTIBLE_KINDS.has(run.kind) && run.changesApplied && !run.reverted);
  return latest?.kind === 'apply' ? latest : null;
}

/** "17 componentes: 15 íntegros, 2 modificados à mão". Contagens zeradas não entram. */
export function componentsSummary(status: HarnessStatus): string {
  const parts = [
    [status.intact, 'íntegro', 'íntegros'],
    [status.kept, 'mantido na sua versão', 'mantidos na sua versão'],
    [status.modified, 'modificado à mão', 'modificados à mão'],
    [status.missing, 'ausente', 'ausentes']
  ] as const;
  const counts = parts.filter(([count]) => count > 0).map(([count, one, many]) => plural(count, one, many));
  const total = plural(status.components.length, 'componente', 'componentes');
  return counts.length > 0 ? `${total}: ${counts.join(', ')}` : total;
}
