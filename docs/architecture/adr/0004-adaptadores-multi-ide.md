# ADR-0004 — Harness canônico com adaptadores por IDE

**Status:** proposta em 2026-11-16, aguarda o dono.
**Contexto completo:** [../../engineering-intelligence-hub/CANONICAL_HARNESS_MODEL.md](../../engineering-intelligence-hub/CANONICAL_HARNESS_MODEL.md).

## Contexto

O Harness só aplica para o Claude Code (`AiEnvironments.Supported = [ClaudeCode]`); Cursor e Codex bloqueiam o plano. O
mecanismo de plano, lock, aplicação e reversão já é independente de IDE; o que está preso são os caminhos de destino do
catálogo e o escopo gravável.

## Decisão proposta

1. Cada componente do catálogo tem conteúdo canônico (vocabulário neutro de ferramentas) e projeções por IDE.
2. Adaptadores são tabelas de projeção sem regra de negócio; adicionar IDE não muda o núcleo.
3. `mariliz.yaml` aceita `aiEnvironments` (lista); `aiEnvironment` continua aceito.
4. A matriz de capacidades vira dado versionado (`environments.json`); recurso sem suporte vira limitação no plano, nunca
   arquivo inventado.
5. `AGENTS.md` carrega o bloco comum quando há mais de uma IDE; `.agents/skills` é o destino comum de skills.
6. Ordem: Claude Code, depois Cursor e Codex (P1); Copilot e Gemini (P2).
7. Cada adaptador tem teste de conformidade com a versão mínima suportada.

## Consequências

- Escopo gravável cresce (`CLAUDE.md`, `AGENTS.md`, `.cursor/`, `.codex/`, `.mcp.json`), em modo bloco ou chave própria.
- Evals do Harness passam a ser por projeção.
- A matriz precisa de revisão a cada entrega; formatos das IDEs mudam rápido.

## Alternativas rejeitadas

- Um gerador por IDE com conteúdo próprio: textos divergem e o dono perde a fonte única.
- Gerar todos os formatos para toda IDE: arquivos inúteis e incompatíveis.
