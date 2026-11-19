# Roadmap: compatibilidade multiversão e inteligência de versões

A sequência foi definida pelo que destrava as outras entregas. O MV-1 é a base: sem versão por pasta e sem catálogo, não há restrição para o agente, criação de projeto nem modernização. Uma entrega por PR (não empilhar).

## Entregas

| Id | Tipo | Problema | Entrega | Depende de | Esforço | Critério de aceite |
|---|---|---|---|---|---|---|
| **MV-1** | Funcionalidade | O agente não sabia a versão de cada pasta nem se ela tem suporte | Catálogo gerado de fontes oficiais, versão por pasta (incluindo `.nvmrc`, `global.json` e o alvo por projeto), classificação de suporte, restrição no contexto, `mariliz versions`, tela "Versões e suporte" | — | M | **Feito nesta branch.** Projeto legado e monorepo de referência passam; o contexto do legado não cita versão mais nova |
| MV-2 | Automação | O catálogo só se atualiza quando alguém roda o gerador | Workflow semanal (`schedule` + `workflow_dispatch`) que roda o gerador e abre PR com o relatório. No motor, aviso de catálogo velho na tela. Periodicidade proposta: semanal para runtimes, diária para segurança quando houver fonte de advisories | MV-1, **DD-MV-1** | B | O PR gerado tem o relatório; uma fonte fora do ar não quebra o job |
| MV-3 | Funcionalidade | Não há "incompatível" nem verificação depois da implementação | **Feito** (ver abaixo). Requisitos entre tecnologias no catálogo (Vite → Node mínimo, React → TypeScript, Tauri CLI ↔ crate). Situação `incompatible`. Checagem do diff (pacote novo exige runtime acima do alvo? TFM ou `engines` mudou sem pedido?) como validação do Harness | MV-1 | M | Um projeto com Vite 8 e `.nvmrc` 18 aparece como incompatível; o diff que sobe o `TargetFramework` numa tarefa comum é apontado |
| MV-4 | Funcionalidade | Ecossistemas fora da descoberta | PHP (`composer.json`/`composer.lock`, `platform.php`), Ruby (`Gemfile`, `.ruby-version`), C/C++ (CMake `cmake_minimum_required`, `CMAKE_CXX_STANDARD`), Kotlin/Android (`compileSdk`, AGP), Swift; `.tool-versions`, `mise.toml`, Volta, Gradle Wrapper, `toolchain` do `go.mod`; Microsoft Agent Framework e Semantic Kernel como frameworks; catálogo com PHP, Ruby e Kotlin | MV-1 | M | Um fixture por ecossistema com versão e evidência |
| MV-5 | Arquitetura | Sem política de seleção executável | `VersionPolicy`: dado o pedido (tecnologias e objetivo), devolve a combinação recomendada, as alternativas (LTS ou mais nova) e o motivo de cada escolha, só com dados do catálogo | MV-3 | M | Mesmo pedido e mesmo catálogo dão a mesma escolha; pré-lançamento nunca sai como padrão; linha com menos de 12 meses de suporte não é recomendada |
| MV-6 | Funcionalidade | O Spock não cria projeto | `project.scaffold` sobre a jornada "Da ideia ao projeto" (W4-3): versões do MV-5, comandos oficiais de criação (`dotnet new`, `npm create vite`) com versão fixada, registro `spock-versions.json` no projeto gerado, build e teste do projeto gerado | MV-5, W4-3 | A | O projeto .NET + React + Vite gerado compila e passa nos testes; o registro diz versão, motivo, fonte e data |
| MV-7 | Funcionalidade | Modernização misturada a tarefas | `modernization.plan` (W4-2) consumindo a compatibilidade: saltos por linha, breaking changes da fonte oficial, passos reversíveis, rollback, aprovação por passo | MV-1, W4-1 | A | Plano para .NET 6 → 10 em passos (6 → 8 → 10) com critério de aceite por passo; nenhuma mudança aplicada sem aprovação |
| MV-8 | Modernização do Spock | Runtime embarcado e CI desatualizados | (a) **feito** (MV-8a, ver abaixo): CPython embarcado 3.12.10 → 3.12.16; (b) `global.json` do motor (10.0.x, `latestFeature`); (c) GitHub Actions para os majors atuais, uma por vez; (d) Dependabot para NuGet, npm, Cargo, pip e Actions, agrupado por ecossistema, com a política de patch/minor/major e sem merge automático | **DD-MV-1** para (c) e (d) | M | Smoke do instalador verde; `generate_sbom.py --check` verde; Dependabot abre PR agrupado com o rótulo do tipo de atualização |
| MV-9 | Modernização do Spock | Majors pendentes na interface e nos testes | Uma entrega por item: TypeScript 7 + typescript-eslint; ESLint 10; Vitest 5.1; Tauri 2.12.x alinhado (crate, CLI, API, plugins); Microsoft.NET.Test.Sdk 18 + coverlet 10; xUnit v2 → v3; ProtectedData no trem 10.0.x **depois de resolver o aviso de licença** (o pacote 10.0.15 traz só a expressão MIT); pnpm 9 → 12; Python 3.12 → 3.13/3.14 (com `sqlite-vec` validado) | MV-8 | M cada | Por item: build, testes, lint, smoke e conformidade de licenças verdes, com versão anterior, nova, fonte e testes registrados no PR |
| MV-10 | Avaliação | .NET 11 | Ramo de avaliação: suíte, smoke do instalador, `argv[0]` do lançador, requisito de CPU, ganho do Runtime Async medido no laço do protocolo | **DD-MV-3** | M | Relatório com o ganho medido e os riscos; a decisão de migrar fica com o dono, com o .NET 12 LTS como alvo provável |

## Ordem

```
MV-1 ─► MV-3 ─► MV-5 ─► MV-6
  │       └──────────────► MV-7 (com W4-1/W4-2)
  ├─► MV-4
  ├─► MV-2 (DD-MV-1)
  └─► MV-8 ─► MV-9 ─► MV-10 (DD-MV-3)
```

Maior valor por esforço depois do MV-1 era o **MV-8 (a)**, feito, e depois o **MV-3**, também feito. A seguir vêm o MV-4 (ecossistemas) e o MV-9 (atualizações do Spock), e depois o MV-5.

## MV-8a: CPython embarcado 3.12.10 → 3.12.16 (2026-11-19)

| | |
|---|---|
| Versão anterior | Windows: CPython 3.12.10 embeddable do python.org. Linux e macOS: python-build-standalone 20250409 (CPython 3.12.10) |
| Versão nova | python-build-standalone 20261110 (CPython 3.12.16) nas três plataformas, com SHA-256 do `SHA256SUMS` da release fixado em `scripts/package-intelligence.py` |
| Motivo | Seis releases de segurança da 3.12 (3.12.11 a 3.12.16) faltavam. O python.org só publica binário, inclusive o embeddable do Windows, enquanto a linha recebe correções gerais. As releases 3.12.11+ saíram só em código-fonte, então o Windows não tinha como subir pelo embeddable. Decisão do dono: PBS nas três plataformas |
| Fontes | `python.org/ftp/python/3.12.16/` (só `.tgz`/`.tar.xz`; `python-3.12.11-embed-amd64.zip` e `python-3.12.16-embed-amd64.zip` respondem 404); `api.github.com/repos/astral-sh/python-build-standalone/releases/latest` (tag 20261110); `pythonbuild/downloads.json` e `cpython-unix/targets.yml` na tag |
| Impacto | O interpretador do Windows passa de 10,6 MB a 33,8 MB, com orçamento de 25 → 40 MB. O isolamento do embeddable é mantido por um `python312._pth` escrito pelo build (`Lib`, `DLLs`, `.`, `import site`; `sys.flags.isolated=1`). O PBS para Windows não traz no `LICENSE.txt` os textos de OpenSSL, SQLite, libffi, zlib, xz, expat e mpdecimal: eles passam a ir em `python/licenses/python-build-standalone`. As bibliotecas sobem (OpenSSL 3.0.16 → 3.5.9 LTS, SQLite 3.47.1 → 3.53.1, zlib 1.3.1 → 1.4), e o xz agora é 0BSD. `build-runtime.ps1 -FetchPython` (o nome antigo `-FetchPythonEmbed` continua aceito) |
| Testes | `package-intelligence.py bundle --python x86_64-pc-windows-msvc` local: interpretador 3.12.16, OpenSSL 3.5.9, SQLite 3.53.1, isolado. `Pacote_embarcado` com `MARILIZ_REQUIRE_PACKAGED=1` aprovado: o grafo LangGraph sobe do bytecode no interpretador novo. `build-runtime.ps1 -Rid win-x64 -FetchPython` completo. `generate_sbom.py --check` verde, com o manifesto de avisos completo. Linux e macOS não foram executados localmente (o CI está sem orçamento): o hash e a lista de bibliotecas vêm da fonte oficial, sem execução real |

## MV-3: combinação incompatível e conferência depois da tarefa (2026-11-19)

| | |
|---|---|
| Requisitos no catálogo | O gerador grava, por linha de React, Angular, Next.js, Vite e TypeScript, o `engines.node` da versão mais nova da linha, lido do registro do npm (exemplo: Vite 8 `^20.19.0 \|\| >=22.12.0`; Angular 22 `^22.22.3 \|\| ^24.15.0 \|\| >=26.0.0`). Requisito de TypeScript e entre CLI e crate do Tauri ficaram de fora: o npm não publica a faixa de TypeScript que o React aceita, e a regra do Tauri é interna do Spock (MV-9) |
| Situação `incompatible` | Calculada olhando o projeto inteiro. (1) O framework exige um Node que o Node da pasta (ou da pasta acima mais próxima; o fixado vence o mínimo do `engines`) não atende. (2) O SDK do `global.json` é mais antigo que algum `TargetFramework` abaixo dele, salvo com `rollForward` de versão maior. Node com só o número maior (`.nvmrc` com `20`) não vira incompatível: o motivo pede a versão exata. A avaliação das faixas do npm (`^`, `~`, `>=`, `<`, hífen, `\|\|`, versão parcial como intervalo) está em `NodeRange` |
| Orientação ao agente | Pasta incompatível: a restrição diz que a combinação não é suportada e que trocar versão para contornar não é com o agente |
| Conferência depois da tarefa | Ferramenta MCP `spock_version_check`, só leitura. Lê os manifestos (sem git, python ou node) e compara com o perfil que o Spock analisou, que é a restrição que o agente recebeu: versão trocada, dependência nova ou removida, e combinação incompatível no estado atual. O bloco do `CLAUDE.md` e a regra do Cursor pedem a chamada antes de concluir a tarefa. Schema em `contracts/schemas/mcp/spock_version_check.output.schema.json` |
| Defeito do MV-1 corrigido | O schema de saída do `spock_context_bootstrap` não aceitava a seção `versions`. Uma IDE que valide o `structuredContent` recusaria o pacote de um projeto analisado. O teste do contrato só cobria projeto sem perfil |
| Fora desta entrega | A conferência não consulta o registro de pacote para dependência arbitrária (rede no caminho da tarefa): só tecnologias do catálogo têm requisito. Comando de CLI para hook de fim de sessão fica para quando houver hook desse tipo |
| Testes | `IncompatibilityTests` (27 casos: faixas do npm, Vite e Next.js contra Node, Node da pasta acima, SDK contra alvo e `rollForward`, conferência com mudança, sem mudança e sem perfil, ferramenta MCP de ponta a ponta com contrato e trilha, bootstrap com `versions` contra o contrato). Suíte do motor 1.801 aprovados; interface 860; gerador do catálogo 13 |

## Decisões que dependem do dono

| Id | Decisão | Opções | Recomendação |
|---|---|---|---|
| DD-MV-1 | Automação em GitHub Actions (MV-2, MV-8 c/d) com o CI sem orçamento | (a) liberar orçamento; (b) Dependabot só com PR, validação local registrada; (c) adiar | (b) até o orçamento voltar. Atualização sem CI verde não tem merge |
| DD-MV-2 | Catálogo também atualizado pelo motor na máquina do usuário (cache local com TTL, fallback no embutido)? | (a) só por release do Spock; (b) motor consulta as fontes oficiais com o consentimento do usuário | (a) por enquanto: sem rede no caminho de desenvolvimento e com revisão de cada mudança. (b) quando o MV-6 precisar de versão do dia para criar projeto |
| DD-MV-3 | .NET 11 (STS) no motor | (a) ficar no 10 LTS até o 12 LTS; (b) migrar depois do MV-10 | (a), salvo ganho medido relevante no MV-10 |
| DD-MV-4 | Cobrar `confirmed=false` (faixa declarada) como aviso na descoberta? | Sim ou não | Não: já vai como instrução ao agente; aviso duplicaria |
