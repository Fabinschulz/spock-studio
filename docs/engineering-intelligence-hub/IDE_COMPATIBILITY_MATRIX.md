# Matriz de compatibilidade das IDEs e agentes

> Parte do estudo [Engineering Intelligence Hub](README.md). **Consulta feita em 2026-11-16, só em documentação oficial**
> (lista de fontes no §6). Nada foi instalado nem executado. "Não verificado" = sem confirmação oficial encontrada.
> Esta matriz envelhece rápido: cada adaptador deve ter um teste de conformidade com a versão mínima suportada, e a
> matriz deve ser revista a cada entrega do SPOCK Connect.

## 1. Versões consultadas

| Ferramenta | Versão | Data da versão |
| --- | --- | --- |
| Claude Code | 2.1.325 | 2026-11-13 |
| Cursor | Editor 3.27; CLI v2026.10.22 | data do editor não informada |
| OpenAI Codex CLI | 0.169.0 estável (0.171.0-alpha.7 em pré-lançamento) | 2026-11-13 |
| Gemini CLI | v0.68.0 estável | 2026-11-10 |
| GitHub Copilot (VS Code) | VS Code 1.146 | 2026-11-11 |
| Especificação MCP | 2026-07-28 (atual), conferida diretamente em `modelcontextprotocol.io/specification/versioning` | — |

## 2. Matriz

| Capacidade | Claude Code | Cursor | Codex | Gemini CLI | Copilot (VS Code) |
| --- | --- | --- | --- | --- | --- |
| Instrução persistente do projeto | **Sim**: `CLAUDE.md` ou `.claude/CLAUDE.md`, `@imports` até 4 níveis | **Sim**: `.cursor/rules/*.mdc`, `AGENTS.md`; o CLI lê também `CLAUDE.md` | **Sim**: `AGENTS.md` (e `AGENTS.override.md`), até 32 KiB | **Sim**: `GEMINI.md` hierárquico, `@file.md` | **Sim**: `.github/copilot-instructions.md`, `AGENTS.md` |
| `AGENTS.md` | **Sim** (≥ 2.1.277), por padrão só sem `CLAUDE.md` | **Sim**, inclusive aninhado | **Nativo** | **Parcial**: por `context.fileName` | **Sim**, o mais próximo vence |
| Regras condicionais por caminho | **Sim**: `.claude/rules/*.md` com `paths:` | **Sim**: `.mdc` com `globs` | **Parcial**: `AGENTS.md` por diretório | **Parcial**: `GEMINI.md` por diretório | **Sim**: `.github/instructions/*.instructions.md` com `applyTo` |
| MCP no projeto | **Sim**: `.mcp.json`, com aprovação interativa | **Sim**: `.cursor/mcp.json` | **Sim**: `.codex/config.toml`, só em projeto confiável | **Sim**: `.gemini/settings.json`, só em pasta confiável | **Sim**: `.mcp.json` na raiz (o harness padrão não lê `.vscode/mcp.json`) |
| Variável de pasta do projeto na config | `${CLAUDE_PROJECT_DIR}` e `${VAR:-padrão}` | `${workspaceFolder}`, `${env:X}` | Não verificado (args relativos) | `$VAR` em `env` | `${NAME:-padrão}` |
| MCP resources | **Sim** (`@servidor:uri`) | **Sim** | **Parcial** (indício em issues, sem doc oficial) | **Sim** (`@servidor://…`) | **Parcial** |
| MCP prompts | **Sim** (`/mcp__servidor__prompt`) | **Sim** | Não verificado | **Sim** (`/nome`) | **Não** |
| `structuredContent` / `outputSchema` | Não verificado | **Parcial**: valida `outputSchema`; indício de que resultado **só** estruturado é descartado | Não verificado | Não verificado | Não verificado |
| Roots | **Sim** | **Sim** | Não verificado | Não verificado | **Parcial**; e roots está **descontinuado** na spec 2026-07-28 |
| Hook de início de sessão com contexto | **Sim**: `SessionStart` (`additionalContext`, até 10.000 caracteres) | **Sim**: `sessionStart` (`additional_context`) | **Sim**: `SessionStart` (~2.500 tokens) | **Sim**: `SessionStart` | **Sim**: `sessionStart` (Preview) |
| Hook por prompt com injeção de contexto | **Sim**: `UserPromptSubmit` | **Não**: `beforeSubmitPrompt` só permite ou bloqueia | **Sim**: `UserPromptSubmit` | **Sim**: `BeforeAgent` | **Não**: saída de `userPromptSubmitted` é ignorada |
| Hook que chama ferramenta MCP | **Sim**: tipo `mcp_tool` (não no `SessionStart` de lançamento, quando o MCP ainda não subiu) | Não | Não verificado | Não verificado | Não |
| Skills | `.claude/skills/<n>/SKILL.md` | `.cursor/skills`, `.agents/skills` (lê também `.claude/skills`) | `.agents/skills` | `.gemini/skills`, `.agents/skills` | `.github/skills`, `.agents/skills` |
| Subagentes | `.claude/agents/*.md` | `.cursor/agents/*.md` (lê `.claude/agents`) | `.codex/agents/*.toml` | `.gemini/agents/*.md` | `.github/agents/*.agent.md` |
| Plugin / extensão | `.claude-plugin/plugin.json` + marketplace | `.cursor-plugin/plugin.json` | `plugin.json` / `.codex-plugin/` | `gemini-extension.json` | "agent plugins" (não verificado) |
| Confiança exigida | Aprovação do servidor de projeto | Aprovação por ferramenta | Projeto confiável; hooks por hash | Pasta confiável; hooks com impressão digital | Controles enterprise |

## 3. O que é garantido, e o que não é

**Nenhuma das cinco ferramentas garante chamar uma ferramenta MCP em todo prompt.** O que é determinístico:

1. **Arquivo de instrução carregado no início da sessão** (todas). Releitura depois da compactação documentada no Claude
   Code para o arquivo da raiz.
2. **Hook de início de sessão que injeta texto** (todas, Copilot em Preview).
3. **Hook por prompt que injeta texto**: Claude Code, Codex e Gemini CLI. **Não** no Cursor nem no Copilot.

Consequência para o desenho ([ADR-0003](../architecture/adr/0003-persistencia-de-contexto.md)):
- o bootstrap de sessão é **determinístico** nas cinco (hook) e **instruído** como reserva (arquivo);
- o contexto vivo por prompt é **determinístico** em três e **instruído** em duas;
- a ferramenta MCP é sempre o caminho completo; o hook entrega um resumo curto e diz o que buscar.

## 4. Pontos que mudaram o desenho

| Fato | Efeito no SPOCK |
| --- | --- |
| A spec MCP 2026-07-28 é stateless, sem `initialize`, com `server/discover` obrigatório; as revisões com handshake (≤ 2025-11-25) seguem interoperáveis | O servidor atual (2025-06-18, JSON-RPC próprio) continua funcionando, mas precisa de um plano de atualização. Decisão pendente: manter implementação própria ou adotar o SDK oficial C# (dependência nova, passa pela auditoria de licenças) |
| Roots descontinuado; a spec recomenda passar diretórios por parâmetro de ferramenta, URI ou configuração | Reforça vínculo por `--project` na configuração e por argumento, não por roots |
| `.mcp.json` na raiz é lido por Claude Code **e** pelo harness padrão do Copilot | Um arquivo atende dois clientes; formato comum `mcpServers` |
| Cursor, Copilot e Codex leem `.agents/skills` (Cursor e Copilot também leem pastas de outros clientes) | Skills canônicas em `.agents/skills` evitam três cópias; `.claude/skills` continua para o Claude Code |
| `AGENTS.md` é lido por quatro das cinco | `AGENTS.md` vira a projeção comum; `CLAUDE.md` importa ou referencia o bloco comum |
| Gemini CLI substituído pelo Antigravity CLI para o plano gratuito (aviso oficial de 18/06/2026); o repositório segue publicando versões | Adaptador Gemini fica em P2, depois de confirmar o produto que o cliente usa |
| Claude Code adia ferramentas (tool search ligado): as `instructions` do servidor e a descrição de cada ferramenta pesam mais | Descrições de ferramenta escritas como intenção ("use para…") e instruções do servidor curtas e acionáveis |
| Limite de saída MCP no Claude Code: 25k tokens por padrão | Respostas estruturadas e com orçamento próprio, bem abaixo disso |

## 5. Implicações por adaptador

| Adaptador | Instrução | MCP | Hooks | Skills/agentes | Prioridade |
| --- | --- | --- | --- | --- | --- |
| Claude Code | Bloco gerenciado no `CLAUDE.md` + `.claude/rules/spock-*.md` | `.mcp.json` | `SessionStart` (command) e `UserPromptSubmit` (command, leve) em `.claude/settings.json` | `.claude/agents`, `.claude/skills` (já gerados) | P1 (primeiro) |
| Cursor | `.cursor/rules/spock.mdc` (`alwaysApply`) + `AGENTS.md` | `.cursor/mcp.json` | `sessionStart` em `.cursor/hooks.json`; **sem** hook por prompt | `.agents/skills` | P1 |
| Codex | Bloco gerenciado no `AGENTS.md` | `.codex/config.toml` (exige projeto confiável) | `SessionStart`, `UserPromptSubmit` em `.codex/hooks.json` (confiança por hash) | `.agents/skills`, `.codex/agents/*.toml` | P1 |
| Copilot | `AGENTS.md` (+ `.github/copilot-instructions.md` se já existir) | `.mcp.json` | `sessionStart` (Preview) | `.agents/skills`, `.github/agents` | P2 |
| Gemini CLI | `GEMINI.md` com import do bloco comum | `.gemini/settings.json` | `SessionStart`, `BeforeAgent` | `.agents/skills` | P2 |

## 6. Fontes (consultadas em 2026-11-16)

| Ferramenta | Fontes |
| --- | --- |
| Claude Code | code.claude.com/docs/en/memory, /mcp, /hooks, /skills, /changelog |
| Cursor | cursor.com/docs/context/rules, /context/mcp, /agent/hooks, /cli/using, /cli/mcp, /skills, /subagents, /reference/plugins, /release-notes; forum.cursor.com (só indício, não oficial) |
| Codex | learn.chatgpt.com/docs/agent-configuration/agents-md, /extend/mcp, /hooks, /config-file/config-reference, /build-skills, /agent-configuration/subagents (redirecionados de developers.openai.com/codex); github.com/openai/codex/releases; issues #14242, #11264 e PR #50546 (indício) |
| Gemini CLI | google-gemini.github.io/gemini-cli/docs/cli/gemini-md.html; geminicli.com/docs/ (aviso do Antigravity), /tools/mcp-server, /hooks, /cli/trusted-folders, /cli/custom-commands, /cli/skills, /core/subagents, /extensions/reference; github.com/google-gemini/gemini-cli/releases; docs.flutter.dev/ai/antigravity-cli |
| Copilot | docs.github.com/en/copilot (custom instructions, MCP, hooks reference); code.visualstudio.com/docs/copilot/customization/custom-instructions, /hooks, /custom-agents, /reference/mcp-configuration; code.visualstudio.com/api/extension-guides/ai/mcp; code.visualstudio.com/updates |
| MCP | modelcontextprotocol.io/specification/versioning (conferida pelo autor do estudo), /latest/changelog, /2026-07-28/server/tools, /2026-07-28/client/roots |

Lacunas da pesquisa: páginas de resources, prompts e authorization da spec 2026-07-28 não foram lidas por inteiro; suporte
a `structuredContent` em Claude Code, Codex, Gemini e Copilot ficou sem confirmação oficial; algumas páginas do Copilot
e do Codex foram vistas só pelo resumo da busca.
