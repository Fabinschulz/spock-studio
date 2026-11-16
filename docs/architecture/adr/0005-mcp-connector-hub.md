# ADR-0005 — SPOCK MCP como gateway de capacidades, não proxy

**Status:** proposta em 2026-11-16, aguarda o dono.
**Contexto completo:** [../../engineering-intelligence-hub/CONNECTOR_HUB.md](../../engineering-intelligence-hub/CONNECTOR_HUB.md),
[../../engineering-intelligence-hub/MCP_CAPABILITY_CATALOG.md](../../engineering-intelligence-hub/MCP_CAPABILITY_CATALOG.md).

## Contexto

O servidor MCP existe (stdio, só leitura, 13+ ferramentas, só texto). As conexões ficam num catálogo central com conexão,
vínculo, recorte e política separados. O pedido quer que a pessoa configure uma vez no SPOCK e que as IDEs recebam só o
autorizado.

## Decisão proposta

1. As IDEs se ligam a um único servidor: o do SPOCK. Credenciais nunca saem do motor.
2. O SPOCK expõe **capacidades** agrupadas por domínio e intenção, com schema de entrada e saída, impacto, erros, limites
   e auditoria. Nada de ferramenta genérica "consultar" nem de micro-ferramentas por endpoint.
3. Nada de proxy indiscriminado: uma ferramenta de MCP externo só aparece na IDE se a pessoa a permitiu, por projeto,
   como ferramenta do SPOCK, com resultado redigido e rotulado como externo (cliente MCP, P2).
4. Ligação direta de um MCP de terceiro na IDE é exceção explícita, exibida como fora da governança.
5. Saída com `structuredContent` e texto sempre junto (há cliente que descarta resultado só estruturado).
6. Respostas de gestão vêm de dados sincronizados com data; chamada ao vivo só pela sincronização controlada.
7. Escrita fica fora do MCP de leitura; quando existir, cria pedido de aprovação na tela do SPOCK.

## Consequências

- Uma composição única do motor para o sidecar e para o MCP (correção H0-2).
- Plano para a versão do protocolo (atual 2026-07-28) e decisão sobre SDK oficial (DD-5).

## Alternativas rejeitadas

- IDE conectada direto a Jira, GitHub e nuvem por MCPs de terceiros: perde auditoria, política e recorte.
- Repassar `tools/list` de servidores externos: expõe operações não classificadas, inclusive destrutivas.
