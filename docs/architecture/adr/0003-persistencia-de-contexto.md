# ADR-0003 — Contexto persistente por arquivo e hook, contexto vivo por MCP

**Status:** proposta em 2026-11-16, aguarda o dono.
**Contexto completo:** [../../engineering-intelligence-hub/CONTEXT_PERSISTENCE_AND_ADAPTERS.md](../../engineering-intelligence-hub/CONTEXT_PERSISTENCE_AND_ADAPTERS.md),
[../../engineering-intelligence-hub/IDE_COMPATIBILITY_MATRIX.md](../../engineering-intelligence-hub/IDE_COMPATIBILITY_MATRIX.md).

## Contexto

Hoje a IDE só conhece o SPOCK se a pessoa copiar um comando de escopo de usuário; nenhum arquivo do projeto orienta o
agente a consultá-lo; nenhum hook traz o estado atual. A pesquisa de 2026-11-16 mostrou que nenhuma das cinco ferramentas
garante chamar uma ferramenta MCP em todo prompt; todas carregam arquivo de instrução no início da sessão; todas têm hook
de início de sessão; só Claude Code, Codex e Gemini CLI têm hook por prompt que injeta contexto.

## Decisão proposta

1. **Duas camadas.** Arquivo de instrução com um bloco gerenciado de bootstrap (≤ 1.200 tokens): identidade, regras,
   como consultar o SPOCK, regra de não inventar. Nenhum dado dinâmico e nenhum segredo em arquivo.
2. **Bootstrap determinístico** por hook de início de sessão onde existir (comando `spock context session`, sem modelo),
   e ferramenta `spock_context_bootstrap` como caminho instruído.
3. **Contexto vivo** sempre pela ferramenta MCP. Hook por prompt só onde existir, leve (≤ 300 tokens, ≤ 300 ms, sem RAG,
   sem modelo), e só para avisar que algo mudou.
4. Limitações por IDE documentadas e exibidas; nenhuma promessa de chamada automática em todo prompt.
5. Arquivos projetados são componentes do Harness: plano com diff, aprovação, lock por bloco, reversão.

## Consequências

- Modo `block` novo no plano; conteúdo da pessoa fora do bloco nunca é lido para decisão nem reescrito.
- Comando `spock context` e um lançador `spock` estável (DD-6).
- Cursor e Copilot ficam com contexto vivo dependente da instrução (limitação do cliente).

## Alternativas rejeitadas

- `CLAUDE.md` como fonte da verdade: prende o produto a uma IDE e congela dado dinâmico.
- Hook por prompt que faz busca no índice: custo e latência em todo prompt, inclusive nos que não têm relação com o projeto.
- Hook `mcp_tool` no `SessionStart` do Claude Code: roda antes de o MCP estar disponível no lançamento.
