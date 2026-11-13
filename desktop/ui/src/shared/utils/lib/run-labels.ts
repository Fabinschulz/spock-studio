const KIND_LABEL: Record<string, string> = {
  apply: 'Configuração aplicada',
  validate: 'Validação',
  revert: 'Reversão',
  workflow: 'Workflow',
  assistant: 'Pergunta ao assistente',
  investigation: 'Investigação de gestão'
};

export function runKindLabel(kind: string): string {
  return KIND_LABEL[kind] ?? kind;
}

export function runEffectLabel(run: { kind: string; changesApplied: boolean; reverted: boolean }): string {
  if (run.reverted) return 'revertida depois';
  if (run.kind === 'apply' && run.changesApplied) return 'alterações aplicadas';
  return '';
}
