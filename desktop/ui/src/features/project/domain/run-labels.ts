import type { RunSummary } from '@/shared/schema';
import { runStateLabel } from '@/shared/utils';

const KIND_LABEL: Record<string, string> = {
  apply: 'Configuração aplicada',
  validate: 'Validação',
  revert: 'Reversão'
};

/** Um tipo novo do motor aparece como veio, nunca some. */
export function runKindLabel(kind: string): string {
  return KIND_LABEL[kind] ?? kind;
}

/** Frase curta do que a execução deixou no projeto; vazia quando não há o que dizer. */
export function runEffectLabel(run: Pick<RunSummary, 'kind' | 'changesApplied' | 'reverted'>): string {
  if (run.reverted) return 'revertida depois';
  if (run.kind === 'apply' && run.changesApplied) return 'alterações aplicadas';
  return '';
}

export function runRowText(run: RunSummary): string {
  const effect = runEffectLabel(run);
  return [runStateLabel(run.state), effect].filter(Boolean).join(' · ');
}
