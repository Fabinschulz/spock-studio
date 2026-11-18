# Arquitetura do runtime (.NET)

O runtime vive em `runtime/`. A experiência desktop está em `desktop/` e a intelligence em `intelligence/`; este documento cobre só o .NET.
As camadas seguem o backend do EmpregaNet, sem camadas "para o futuro": o domínio no centro, casos de uso na
Application, e tudo que toca o sistema operacional na Infrastructure. A pasta `intelligence/` fica fora dessas camadas:
o runtime sobe o processo e fala com ele por HTTP.

## Camadas

| Projeto | Papel | Depende de |
| ------- | ----- | ---------- |
| `Mariliz.Core` | Modelos por contexto (`Discovery/`, `Plan/`, `Evals/`), políticas puras (`PathGuard`, `ManagedScope`, `SecretScanner`), hash, diff e caminhos | nada |
| `Mariliz.Application` | Casos de uso e protocolo. Define as portas (`IProjectFiles`, `IProcessRunner`, `IRunStore`, `IClock`) | Core |
| `Mariliz.Infrastructure` | Implementa as portas: disco, processos, diário, cofre do sistema, tokenizador, processo Python | Application |
| `Mariliz.Infrastructure.Cloud` | Provedores de nuvem (`Aws/`, `Azure/`, `Gcp/`) atrás de `LocalCloudHub`, com os SDKs de cada um | Application, Infrastructure |
| `Mariliz.Runtime` / `Mariliz.Cli` | Pontos de entrada (protocolo NDJSON, `mcp`, `context`, `version` e linha de comando) | Application, Infrastructure (o Runtime também .Cloud) |

Os testes ficam em `runtime/tests/`. A solução é `runtime/Mariliz.sln`.

A Application nunca acessa disco nem processos diretamente: usa as portas.

## Dentro de `Mariliz.Application`

| Pasta | Responsabilidade |
| ----- | ---------------- |
| `Harness/` | Compor e planejar: `HarnessComposition`, `ComponentSelector`, `PlanBuilder`, `LockBuilder` |
| `Specification/` | O único ponto que conhece o mecanismo da estrutura de especificação: `SpecificationManifest` (instalador, pacote, versões), `SpecificationInspector`, `SpecificationPlanner` |
| `Discovery/` | Descobrir o projeto: um detector por stack (`DotnetDetector`, `NodeDetector`...), `SystemProbe`, `HarnessInventoryReader` |
| `Execution/` | Aplicar e reverter: `ApplyService` (orquestra e desfaz), `ApplyExecution` (uma fase por método), `WritePlanner`, `RevertService`, `RunJournalRecorder` |
| `Validation/` | Evals: `EvalRunner` roda o catálogo; cada verificação é um `IEvalCheck` em `Checks/` |
| `Protocol/` | `EngineSession` (laço e envelope), `ConfigureRequestHandler` (casos de uso), `RequestParams`, `ProtocolMethods` |
| `Workspace/` | Autorização de pastas |
| `Integrations/` | Central de integrações: `ConnectionService` (credencial e ciclo de vida), `ProjectIntegrationService` (vínculo e recorte por projeto), `SyncService` (sincronização incremental), e um `IIntegrationConnector` por provedor em `Connectors/`. Portas: `ISecretVault`, `IIntegrationStore`, `ISyncedDataStore`, `IConnectorTransport`, `IAuditLog`. Ver [evolucao-engineering-management.md](../plataforma/evolucao-engineering-management.md) |
| `Cloud/` | Conexões de nuvem e leitura por projeto: `CloudConnectionService`, `CloudProjectService`, `CloudHub`; os provedores ficam em `Mariliz.Infrastructure.Cloud` |
| `Iac/` | OpenTofu/Terraform: geração por provedor, validação, plano, aplicação com aprovação, drift, state e runtimes |
| `Workflows/` | Fluxos com o Python: `WorkflowService`, `ApprovalGate`, `ActionGovernance`, `KnowledgeSources` (o que vai ao índice) |
| `Context/` | Context Engine e prévia do contexto: `ContextEngine`, `ContextPackage`, `ContextReviewService` |
| `Mcp/` | Servidor MCP para a IDE: `McpServer` sobre o SDK oficial e as ferramentas por domínio |
| `Models/` | Provedores, finalidades e tetos de modelo: `ModelProviders`, `ModelPurposes`, `ModelBudgets`, `ModelBroker` |
| `Management/` | Gestão: decisões (`DecisionMemory`), fluxo, previsão, riscos e investigação |
| `Engineering/` | Catálogo do Harness, revisões, política dos agentes, `TechnologyCatalog`, inventário de MCP |
| `AiTools/` | CLIs de IA externas em modo leitura: `AiToolCatalog`, `AgentRunService` |
| `Ideas/` | Jornada "Da ideia ao projeto": `IdeaArchitectService`, `IdeaChecks` |
| `Security/` | Varredura e correções: `SecurityScanner`, `SecurityFixPlanner` |
| `Audit/`, `Timeline/` | Trilha de auditoria e linha do tempo do projeto |
| `Assets/` | Textos embutidos do Harness: agentes, regras, hooks, skill e catálogo de componentes |
| `Ports/` | Tipos das portas (`ProcessSpec`, `ProcessResult`, `WorkflowAck`) |

Desvios conhecidos da regra "a Application nunca acessa disco": `InvestigationMemory`, `WorkflowContextBuilder` e
`WorkspaceAuthorizer` ainda criam ou sobem pastas diretamente; o SDK do MCP e o stdio moram em `Mcp/`. Ver
[SPOCK_CURRENT_STATE.md](../ai-engineering-platform/SPOCK_CURRENT_STATE.md#2-estrutura).

## Regras

- Um tipo por arquivo; o nome do arquivo é o nome do tipo. A pasta é o contexto (`Plan/`, `Ports/`...); dentro dela, enums ficam em
  `Enums/` e interfaces em `Interfaces/`. O namespace é o do contexto e não inclui `Enums` nem `Interfaces`, para que mover
  um tipo entre subpastas não mude nenhuma referência. Seções aninhadas de um formato de arquivo (`DeclaredConfig`, `ManagedLockFile`) ficam
  no tipo que as contém.
- **Produto x mecanismo.** O produto fala em "modelo de implementação" (`ImplementationModels.Mariliz`) e em "estrutura de
  especificação" (`SpecificationStatus`, `ComponentKind.Specification`, `EvalIds.Specification`, `PlanOptions`). Só
  `Specification/` sabe que hoje a estrutura é uma skill instalada por um instalador de terceiros; trocar o mecanismo muda
  essa pasta, não os contratos. A exceção é a política de escrita (`ManagedScope.SpecificationDirectory`), que fica no Core
  porque a decisão de "quem é dono deste caminho" é de segurança.
- Os identificadores que viajam no protocolo ou ficam em disco (`implementationModel` em `mariliz.yaml`, seção
  `specification` em `mariliz.lock`, `mariliz.specification.structure`) são neutros. O que é fato do instalador (comando
  `npx`, pasta `.claude/skills/tlc-spec-driven`) continua visível no cartão de aprovação do comando, porque é o que a
  pessoa aprova.
- Um método faz uma coisa. Uma sequência de fases vira uma classe com um método por fase (`ApplyExecution`).
- Adicionar uma verificação de validação é criar um `IEvalCheck` e uma entrada em `EvalCatalog`, sem mexer em `EvalRunner`.
- Adicionar um provedor de integração é criar um `IIntegrationConnector` e registrá-lo em `LocalIntegrationHub`, sem mexer nos
  serviços nem na interface (ela desenha os campos a partir do `ConnectorDescriptor`). A credencial só passa por `ISecretVault`.
- Valores textuais repetidos viram constante (`OpKinds`, `OpSources`, `StepStatus`, `ComponentDecisions`, `ProtocolMethods`).
- Interface só quando há mais de uma implementação (`IEvalCheck`) ou uma fronteira com o sistema (portas).
- Comentário explica um **porquê** que o código não mostra (segurança, contrato com o instalador, protocolo). Não repete o nome
  do tipo nem descreve o que o código já diz.

## Validação

```bash
dotnet build runtime/Mariliz.sln
dotnet test runtime/Mariliz.sln
```
