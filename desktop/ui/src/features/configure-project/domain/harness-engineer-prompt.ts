import type { Discovery, HarnessNames } from '@/shared/schema';

import type { Loadable } from '../types/journey';

/** Código da limitação que o motor devolve enquanto o plano usa só as regras-base (ainda não há proposta). */
export const HARNESS_ENGINEER_NOT_INVOKED = 'HARNESS_ENGINEER_NOT_INVOKED';

// Uma jornada retomada sem a descoberta: vale o nome atual do Spock até a próxima análise, que traz o do projeto.
const CURRENT_NAMES: HarnessNames = {
  declaration: 'spock.yaml',
  lock: 'spock.lock',
  stateDirectory: '.spock',
  proposal: '.spock/proposal.json',
  proposalSchema: '.spock/schemas/harness-proposal.v1.schema.json',
  harnessEngineer: 'spock-harness-engineer',
  harnessEngineerAgent: '.claude/agents/spock-harness-engineer.md'
};

export const namingOf = (discovery: Loadable<Discovery>): HarnessNames =>
  discovery.status === 'ready' ? discovery.data.naming : CURRENT_NAMES;

/**
 * Pedido pronto para colar no ambiente de IA do projeto, com os nomes que este projeto usa (a descoberta os informa). O
 * motor não chama modelos: é a pessoa quem pede ao agente que escreva a proposta, e o motor a valida antes de planejar.
 * O texto espelha as regras do próprio agente: escrever só o arquivo da proposta, nunca permissões, hooks, comandos ou
 * MCPs, e nunca segredos.
 */
export function harnessEngineerPrompt(naming: HarnessNames): string {
  return [
    `Use o agente ${naming.harnessEngineer} para analisar este projeto e propor a composição do Harness.`,
    '',
    `1. Leia ${naming.declaration}, ${naming.lock}, ${naming.proposalSchema}, AGENTS.md, CLAUDE.md, .claude/, .agents/, .mcp.json e .codex/config.toml (os que existirem).`,
    '2. Para cada componente do catálogo do schema, decida enable, keep ou skip, com justificativa e ao menos uma evidência (arquivo).',
    '3. Registre sobreposições e conflitos como observações e classifique cada afirmação como fact, inference, hypothesis ou unverified.',
    `4. Escreva SOMENTE o arquivo da proposta (${naming.proposal}), válido contra o schema. Não edite nenhum outro arquivo e não proponha permissões, hooks, comandos nem servidores MCP.`,
    '5. Nunca inclua segredos, tokens ou strings de conexão.'
  ].join('\n');
}
