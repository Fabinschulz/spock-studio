# Arquitetura do backend (.NET)

Segue as camadas do backend do EmpregaNet, sem camadas "para o futuro": o domínio no centro, casos de uso na
Application, e tudo que toca o sistema operacional na Infrastructure.

## Camadas

| Projeto | Papel | Depende de |
| ------- | ----- | ---------- |
| `Mariliz.Core` | Modelos, políticas puras (`PathGuard`, `SecretScanner`), hash, diff e caminhos | nada |
| `Mariliz.Application` | Casos de uso e protocolo. Define as portas (`IProjectFiles`, `IProcessRunner`, `IRunStore`, `IClock`) | Core |
| `Mariliz.Infrastructure` | Implementa as portas: disco, processos, diário | Application |
| `Mariliz.Engine` / `Mariliz.Cli` | Pontos de entrada (protocolo NDJSON e linha de comando) | Application, Infrastructure |

A Application nunca acessa disco nem processos diretamente: usa as portas.

## Dentro de `Mariliz.Application`

| Pasta | Responsabilidade |
| ----- | ---------------- |
| `Harness/` | Compor e planejar: `HarnessComposer`, `ComponentSelector`, `TlcPlanner`, `PlanBuilder`, `LockBuilder` |
| `Discovery/` | Descobrir o projeto: um detector por stack (`DotnetDetector`, `NodeDetector`...), `SystemProbe`, `HarnessInventoryReader` |
| `Execution/` | Aplicar e reverter: `ApplyService` (orquestra e desfaz), `ApplyExecution` (uma fase por método), `WritePlanner`, `RevertService`, `RunJournalRecorder` |
| `Validation/` | Evals: `EvalRunner` roda o catálogo; cada verificação é um `IEvalCheck` em `Checks/` |
| `Protocol/` | `EngineSession` (laço e envelope), `ConfigureRequestHandler` (casos de uso), `RequestParams`, `ProtocolMethods` |
| `Workspace/` | Autorização de pastas |

## Regras

- Um tipo por arquivo; o nome do arquivo é o nome do tipo.
- Um método faz uma coisa. Uma sequência de fases vira uma classe com um método por fase (`ApplyExecution`).
- Adicionar uma verificação de validação é criar um `IEvalCheck` e uma entrada em `EvalCatalog`, sem mexer em `EvalRunner`.
- Valores textuais repetidos viram constante (`OpKinds`, `OpSources`, `StepStatus`, `ComponentDecisions`, `ProtocolMethods`).
- Interface só quando há mais de uma implementação (`IEvalCheck`) ou uma fronteira com o sistema (portas).
- Comentário explica um **porquê** que o código não mostra (segurança, contrato com o TLC, protocolo). Não repete o nome
  do tipo nem descreve o que o código já diz.

## Validação

```bash
dotnet build Mariliz.sln
dotnet test Mariliz.sln
```
