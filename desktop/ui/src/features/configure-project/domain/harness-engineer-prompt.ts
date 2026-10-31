/** Código da limitação que o motor devolve enquanto o plano usa só as regras-base (não há `.mariliz/proposal.json`). */
export const HARNESS_ENGINEER_NOT_INVOKED = 'HARNESS_ENGINEER_NOT_INVOKED';

/**
 * Pedido pronto para colar no ambiente de IA do projeto. O motor não chama modelos: é a pessoa quem pede ao agente
 * `harness-engineer` (instalado na aplicação) que escreva a proposta, e o motor a valida antes de planejar.
 * O texto espelha as regras do próprio agente: escrever só o arquivo da proposta, nunca permissões, hooks,
 * comandos ou MCPs, e nunca segredos.
 */
export const harnessEngineerPrompt = [
  'Use o agente harness-engineer para analisar este projeto e propor a composição do Harness.',
  '',
  '1. Leia mariliz.yaml, mariliz.lock, .mariliz/schemas/harness-proposal.v1.schema.json, CLAUDE.md, .claude/ e .mcp.json (os que existirem).',
  '2. Para cada componente do catálogo do schema, decida enable, keep ou skip, com justificativa e ao menos uma evidência (arquivo).',
  '3. Registre sobreposições e conflitos como observações e classifique cada afirmação como fact, inference, hypothesis ou unverified.',
  '4. Escreva SOMENTE .mariliz/proposal.json, válido contra o schema. Não edite nenhum outro arquivo e não proponha permissões, hooks, comandos nem servidores MCP.',
  '5. Nunca inclua segredos, tokens ou strings de conexão.'
].join('\n');
