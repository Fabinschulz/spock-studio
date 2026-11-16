# ADR-0007 — Vínculo obrigatório de projeto e política por destino

**Status:** proposta em 2026-11-16, aguarda o dono.
**Contexto completo:** [../../engineering-intelligence-hub/ACCESS_POLICIES_AND_SECURITY.md](../../engineering-intelligence-hub/ACCESS_POLICIES_AND_SECURITY.md).

## Contexto

Sem `--project`, o MCP expõe todos os projetos autorizados (`McpProjectResolver.cs:9`). O id do projeto é o hash do
caminho, então clone e worktree viram outro projeto. A política de contextualização é um booleano por vínculo, que vale
para índice, workflow, assistente e, por extensão, IDE. Não há identidade de pessoa.

## Decisão proposta

1. **Vínculo obrigatório.** O MCP recusa iniciar sem `--project` ou `--project-dir`. Sem resolução única: erro tipado
   (`PROJECT_NOT_BOUND`, `PROJECT_AMBIGUOUS`). Nunca uma lista de todos como reserva.
2. **Id estável** `project.id` no `mariliz.yaml`; registro local liga o id às pastas autorizadas na máquina; a resolução
   exige id conhecido **e** pasta autorizada. Remoto Git diferente do registrado gera aviso.
3. **Política por destino** em cada vínculo: `management`, `knowledge`, `workflow`, `assistant`, `ide` (`none`,
   `summary`, `items`, `full`), padrão `ide: summary`. `FeedsKnowledge` migra para os destinos de conhecimento.
4. Camadas verificadas no motor em ordem (pasta, vínculo, ferramenta, fonte, política, recorte); qualquer "não" encerra.
5. Conteúdo externo vai em campo `untrusted`, nunca concatenado à instrução. Nenhuma ferramenta muda política.
6. Filtro de ambiente nos processos filhos antes de hooks que chamam o motor e antes do cliente MCP.

## Consequências

- Configurações "Todos os projetos" já copiadas param de funcionar, com mensagem do que fazer.
- Papéis de gestor e desenvolvedor dentro da mesma credencial continuam impossíveis sem identidade (ADR-0008).

## Alternativas rejeitadas

- Roots do MCP para escolher o projeto: descontinuado na spec 2026-07-28.
- Manter a reserva "todos": é exatamente o vazamento que o pedido proíbe.
