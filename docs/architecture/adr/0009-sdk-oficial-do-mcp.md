# ADR-0009 — Protocolo MCP pelo SDK oficial em C#; o servidor próprio fica registrado para restauração

**Status:** aceita em 2026-11-17 pelo dono (DD-5 do estudo Engineering Intelligence Hub).
**Contexto:** [../../engineering-intelligence-hub/ROADMAP.md](../../engineering-intelligence-hub/ROADMAP.md) (F3).

## Contexto

Desde a fase P (`1aed78e`, 2026-11-14), o modo `mcp` do motor usava um servidor JSON-RPC próprio
(`runtime/Mariliz.Application/Mcp/McpServer.cs`, cerca de 140 linhas, sem dependências). Ele parou na revisão
2025-06-18 do protocolo, duas revisões atrás da atual (2026-07-28), e só devolvia texto. O F3 pede resposta estruturada,
erros tipados, aviso de mudança da lista de ferramentas e um plano de versão do protocolo.

O SDK oficial (`ModelContextProtocol`, organização modelcontextprotocol, mantido com a Microsoft, Apache-2.0) está na
2.4.0 estável, implementa a 2026-07-28 com fallback para clientes antigos e traz saída estruturada. O pacote
`ModelContextProtocol.Core` tem o transporte stdio e os handlers de baixo nível, sem hosting nem DI.

## Decisão

1. A camada de protocolo do modo `mcp` passa a ser o `ModelContextProtocol.Core`, em versão fixada. Atualização só de
   propósito, com os testes do MCP verdes.
2. O que é do Spock não muda e não depende do SDK: escopo obrigatório e resolução de projeto (`McpOptions`,
   `McpProjectResolver`), as ferramentas (`McpTool`, `ProjectContextTools`, `CloudMcpTools`, `IacMcpTools`), a trilha de
   auditoria por chamada, a redação de linhas com cara de segredo (`SecretScanner.RedactLines`) e o texto das instruções.
3. O servidor próprio fica registrado aqui para ser restaurado se o SDK deixar de servir (licença, quebra de
   compatibilidade, abandono, cliente que só funcione com o comportamento antigo).

## O servidor próprio, para restaurar

**Última versão:** commit `87ba3b6097959d54a3b990af584ba4c415636209` (master de 2026-11-16, merge do #124). Arquivos:

- `runtime/Mariliz.Application/Mcp/McpServer.cs`: o servidor;
- `runtime/tests/Mariliz.Application.Tests/McpServerTests.cs`: os testes de protocolo e das ferramentas;
- `runtime/Mariliz.Runtime/McpEntry.cs`: a chamada `new McpServer(...).RunAsync(Console.In, Console.Out, ct)`.

Para restaurar:

```bash
git show 87ba3b6:runtime/Mariliz.Application/Mcp/McpServer.cs > runtime/Mariliz.Application/Mcp/McpServer.cs
git show 87ba3b6:runtime/tests/Mariliz.Application.Tests/McpServerTests.cs > runtime/tests/Mariliz.Application.Tests/McpServerTests.cs
```

Depois: remover o `PackageReference` de `ModelContextProtocol.Core` do `Mariliz.Application.csproj`, voltar a
`McpEntry` para a chamada acima, ajustar os testes que passaram a usar o cliente do SDK e tirar o pacote do inventário de
licenças.

### Comportamento que ele tinha (o que um substituto precisa manter)

| Ponto | Comportamento |
| --- | --- |
| Transporte | JSON-RPC 2.0, uma mensagem por linha no stdio; linha vazia ignorada |
| BOM | Ignora o BOM do UTF-8 no começo da linha, e uma linha só com o BOM (o PowerShell abre a entrada assim; revalidação de 2026-11-15) |
| Versões | Responde na versão que o cliente pede, entre 2025-06-18, 2025-03-26 e 2024-11-05; versão desconhecida cai na 2025-06-18 |
| `initialize` | `serverInfo` `spock-studio` / "Spock Studio" com a versão do motor; `capabilities.tools.listChanged = false`; `instructions` em português |
| Métodos | `initialize`, `ping`, `tools/list`, `tools/call`; notificações (sem `id`) não têm resposta |
| Erros de protocolo | -32700 JSON inválido; -32600 sem objeto ou sem `method`; -32601 método desconhecido; -32602 ferramenta desconhecida |
| Erros de ferramenta | `McpToolException` e `EngineException` voltam como resultado com `isError: true` e a mensagem, para o modelo corrigir o pedido; exceção inesperada vira "Falha inesperada ao ler o contexto (Tipo)" sem derrubar o servidor |
| Descrição das ferramentas | `name`, `title`, `description`, `inputSchema` e `annotations` com `readOnlyHint: true` e `openWorldHint` (só a cloud) |
| Saída | Só `content` de texto, sempre passado por `SecretScanner.RedactLines` |
| Cliente na auditoria | `clientInfo.name` do `initialize`, só se parecer nome de programa (letras, números, espaço, `._-`, até 60 caracteres) |

## Consequências

- Entra o `ModelContextProtocol.Core` 2.4.0 (mais as abstrações da Microsoft, MIT). O pacote declara Apache-2.0 sem
  trazer o texto; o `LICENSE` do repositório na tag `v2.4.0` (commit `4cbf42ef`) explica que o projeto está em transição
  de MIT para Apache-2.0 e traz os dois textos: contribuições sem consentimento de relicenciamento continuam MIT. O
  arquivo inteiro vai nos avisos dos instaladores (`docs/compliance/notices/upstream/mcp-csharp-sdk-LICENSE`), o que
  cumpre as duas licenças. Não há NOTICE no repositório.
- Mudanças de comportamento em relação ao servidor próprio: uma linha de JSON inválido não tem mais resposta -32700 (o SDK
  descarta e a sessão segue); o `initialize` anuncia também a capacidade `logging` (padrão do SDK); a revisão 2026-07-28
  não passa pelo `initialize` (o cliente dessa revisão usa a descoberta). O BOM do UTF-8 no começo da entrada continua
  aceito, por um stream do Spock (`LeadingBomSkipStream`), porque o SDK não reconhece a primeira mensagem com ele grudado.
- O SDK muda rápido (1.x em fevereiro, 2.0 em julho de 2026, com quebras). A versão fica fixada e cada atualização é um
  PR com os testes do MCP.
- A compatibilidade com Claude Code e Cursor reais precisa ser conferida em cada atualização relevante do SDK.

## Alternativas rejeitadas

- Manter o servidor próprio e implementar a 2026-07-28 nele: o Spock passaria a acompanhar sozinho cada revisão do
  protocolo (fim do `initialize`, descoberta, obsolescência de logging, sampling e roots).
- Usar o pacote `ModelContextProtocol` completo: traz hosting e DI que o modo `mcp` não usa; o `Core` basta.
