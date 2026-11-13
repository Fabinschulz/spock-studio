import type { RunDetail } from '@/shared/schema';

export interface DeliverySummary {
  request: string;
  /** Nunca vazio. */
  done: string[];
  validated: string;
  leftOut: string[];
}

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

const DEFAULT_REQUEST: Record<string, string> = {
  apply: 'Aplicar a configuração do Harness no projeto.',
  validate: 'Validar o projeto contra as verificações do Harness.',
  revert: 'Reverter uma aplicação anterior do Harness.',
  workflow: 'Pedido de engenharia.',
  assistant: 'Pergunta ao assistente gerencial sobre o projeto.',
  investigation: 'Investigação de gestão em etapas sobre o projeto.'
};

/**
 * O texto que a pessoa escreveu não aparece de propósito: o diário nunca o guarda (ele fica só no checkpoint do
 * runtime), e a tela não deve prometer o que o motor não registra.
 */
function requestOf(detail: RunDetail): string {
  return DEFAULT_REQUEST[detail.kind] ?? 'Execução do Spock Studio.';
}

function doneOf({ done }: RunDetail['summary']): string[] {
  const files = [
    done.created > 0 ? `criou ${plural(done.created, 'arquivo', 'arquivos')}` : null,
    done.updated > 0 ? `atualizou ${plural(done.updated, 'arquivo', 'arquivos')}` : null,
    done.removed > 0 ? `removeu ${plural(done.removed, 'arquivo', 'arquivos')}` : null,
    done.reverted > 0 ? `reverteu ${plural(done.reverted, 'arquivo', 'arquivos')}` : null
  ].filter((part): part is string => part !== null);

  const sentences: string[] = [];
  if (files.length > 0) sentences.push(`${files.join(', ').replace(/^./, (letter) => letter.toUpperCase())}.`);
  if (done.stepsTotal > 0)
    sentences.push(`${plural(done.stepsCompleted, 'passo concluído', 'passos concluídos')} de ${done.stepsTotal}.`);
  if (done.actionsTotal > 0)
    sentences.push(
      `${plural(done.actionsExecuted, 'ação proposta por agentes executada', 'ações propostas por agentes executadas')} de ${done.actionsTotal}.`
    );
  return sentences.length > 0 ? sentences : ['Nenhum arquivo foi alterado.'];
}

function validatedOf({ validated }: RunDetail['summary']): string {
  if (validated === null) return 'Nenhuma verificação foi registrada nesta execução.';

  const parts = [
    validated.failed > 0 ? plural(validated.failed, 'falhou', 'falharam') : null,
    validated.blocked > 0 ? plural(validated.blocked, 'ficou bloqueada', 'ficaram bloqueadas') : null,
    validated.notRun > 0 ? plural(validated.notRun, 'não rodou', 'não rodaram') : null
  ].filter((part): part is string => part !== null);

  const head = `${validated.passed} de ${plural(validated.total, 'verificação', 'verificações')} ${validated.passed === 1 ? 'passou' : 'passaram'}`;
  const required =
    validated.requiredNotPassed > 0
      ? ` ${plural(validated.requiredNotPassed, 'obrigatória não passou', 'obrigatórias não passaram')}.`
      : '';
  return `${head}${parts.length > 0 ? `; ${parts.join(', ')}` : ''}.${required}`;
}

function leftOutOf({ leftOut }: RunDetail['summary']): string[] {
  return [
    leftOut.notApplied > 0
      ? `${plural(leftOut.notApplied, 'alteração não foi aplicada', 'alterações não foram aplicadas')}.`
      : null,
    leftOut.conflicts > 0 ? `${plural(leftOut.conflicts, 'arquivo em conflito', 'arquivos em conflito')}.` : null,
    leftOut.actionsNotExecuted > 0
      ? `${plural(leftOut.actionsNotExecuted, 'ação proposta não foi executada', 'ações propostas não foram executadas')}.`
      : null,
    leftOut.pending > 0 ? `${plural(leftOut.pending, 'item pendente', 'itens pendentes')}.` : null
  ].filter((part): part is string => part !== null);
}

/** Vem só das contagens do diário; nada aqui interpreta o resultado nem promete mais do que o motor registrou. */
export function deliverySummary(detail: RunDetail): DeliverySummary {
  return {
    request: requestOf(detail),
    done: doneOf(detail.summary),
    validated: validatedOf(detail.summary),
    leftOut: leftOutOf(detail.summary)
  };
}
