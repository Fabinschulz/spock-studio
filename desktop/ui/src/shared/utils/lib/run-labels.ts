const KIND_LABEL: Record<string, string> = {
  apply: 'Configuração aplicada',
  validate: 'Validação',
  revert: 'Reversão',
  workflow: 'Workflow',
  assistant: 'Pergunta ao assistente',
  investigation: 'Investigação de gestão'
};

/** Como a interface nomeia o tipo de uma execução. Um tipo novo do motor aparece como veio, nunca some. */
export function runKindLabel(kind: string): string {
  return KIND_LABEL[kind] ?? kind;
}

/** Frase curta do que a execução deixou no projeto; vazia quando não há o que dizer. */
export function runEffectLabel(run: { kind: string; changesApplied: boolean; reverted: boolean }): string {
  if (run.reverted) return 'revertida depois';
  if (run.kind === 'apply' && run.changesApplied) return 'alterações aplicadas';
  return '';
}
