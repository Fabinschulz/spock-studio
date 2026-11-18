/** Código da limitação que o motor devolve enquanto o plano usa só as regras-base (não há proposta em `.spock/proposal.json`). */
export const HARNESS_ENGINEER_NOT_INVOKED = 'HARNESS_ENGINEER_NOT_INVOKED';

/**
 * Pedido pronto para colar no ambiente de IA do projeto. O motor não chama modelos: é a pessoa quem pede ao agente
 * `spock-harness-engineer` (`harness-engineer` num projeto configurado antes da troca de nome) que escreva a proposta, e o motor a valida antes de planejar.
 * O texto espelha as regras do próprio agente: escrever só o arquivo da proposta, nunca permissões, hooks,
 * comandos ou MCPs, e nunca segredos.
 */
export const harnessEngineerPrompt = [
  'Use o agente spock-harness-engineer (harness-engineer num projeto configurado antes da troca de nome) para analisar este projeto e propor a composição do Harness.',
  '',
  '1. Leia spock.yaml, spock.lock, .spock/schemas/harness-proposal.v1.schema.json (num projeto configurado antes da troca de nome: mariliz.yaml, mariliz.lock e .mariliz/schemas/), AGENTS.md, CLAUDE.md, .claude/, .agents/, .mcp.json e .codex/config.toml (os que existirem).',
  '2. Para cada componente do catálogo do schema, decida enable, keep ou skip, com justificativa e ao menos uma evidência (arquivo).',
  '3. Registre sobreposições e conflitos como observações e classifique cada afirmação como fact, inference, hypothesis ou unverified.',
  '4. Escreva SOMENTE o arquivo da proposta que a definição do agente em .claude/agents/ indica (.spock/proposal.json, ou .mariliz/proposal.json num projeto configurado antes da troca de nome), válido contra o schema. Não edite nenhum outro arquivo e não proponha permissões, hooks, comandos nem servidores MCP.',
  '5. Nunca inclua segredos, tokens ou strings de conexão.'
].join('\n');
