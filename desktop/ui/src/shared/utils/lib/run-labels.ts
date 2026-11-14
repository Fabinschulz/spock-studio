const KIND_LABEL: Record<string, string> = {
  apply: 'Configuração aplicada',
  validate: 'Validação',
  revert: 'Reversão',
  workflow: 'Pedido de engenharia',
  assistant: 'Pergunta ao assistente',
  investigation: 'Investigação de gestão'
};

export function runKindLabel(kind: string): string {
  return KIND_LABEL[kind] ?? kind;
}

const ACTION_STATUS_LABEL: Record<string, string> = {
  executed: 'executada',
  skipped: 'não aprovada, nada foi gravado',
  conflict: 'não gravada: já existe um arquivo diferente neste caminho',
  failed: 'não foi possível executar',
  approved: 'aprovada, aguardando a execução',
  proposed: 'proposta'
};

export function actionStatusLabel(status: string): string {
  return ACTION_STATUS_LABEL[status] ?? status;
}

// O motor grava o resultado da verificação com o nome do enum (Passed, NotRun...): a comparação ignora maiúsculas.
const EVAL_STATUS_LABEL: Record<string, string> = {
  passed: 'aprovada',
  failed: 'reprovada',
  blocked: 'bloqueada',
  notrun: 'não executada'
};

export function evalStatusLabel(status: string): string {
  return EVAL_STATUS_LABEL[status.toLowerCase()] ?? status;
}

export function runEffectLabel(run: { kind: string; changesApplied: boolean; reverted: boolean }): string {
  if (run.reverted) return 'revertida depois';
  if (run.kind === 'apply' && run.changesApplied) return 'alterações aplicadas';
  return '';
}
