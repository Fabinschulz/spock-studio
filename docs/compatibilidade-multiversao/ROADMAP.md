# Roadmap: compatibilidade multiversão e inteligência de versões

A sequência foi definida pelo que destrava as outras entregas. O MV-1 é a base: sem versão por pasta e sem catálogo, não há restrição para o agente, criação de projeto nem modernização. Uma entrega por PR (não empilhar).

## Entregas

| Id | Tipo | Problema | Entrega | Depende de | Esforço | Critério de aceite |
|---|---|---|---|---|---|---|
| **MV-1** | Funcionalidade | O agente não sabia a versão de cada pasta nem se ela tem suporte | Catálogo gerado de fontes oficiais, versão por pasta (incluindo `.nvmrc`, `global.json` e o alvo por projeto), classificação de suporte, restrição no contexto, `mariliz versions`, tela "Versões e suporte" | — | M | **Feito nesta branch.** Projeto legado e monorepo de referência passam; o contexto do legado não cita versão mais nova |
| MV-2 | Automação | O catálogo só se atualiza quando alguém roda o gerador | Workflow semanal (`schedule` + `workflow_dispatch`) que roda o gerador e abre PR com o relatório. No motor, aviso de catálogo velho na tela. Periodicidade proposta: semanal para runtimes, diária para segurança quando houver fonte de advisories | MV-1, **DD-MV-1** | B | O PR gerado tem o relatório; uma fonte fora do ar não quebra o job |
| MV-3 | Funcionalidade | Não há "incompatível" nem verificação depois da implementação | **Feito** (ver abaixo). Requisitos entre tecnologias no catálogo (Vite → Node mínimo, React → TypeScript, Tauri CLI ↔ crate). Situação `incompatible`. Checagem do diff (pacote novo exige runtime acima do alvo? TFM ou `engines` mudou sem pedido?) como validação do Harness | MV-1 | M | Um projeto com Vite 8 e `.nvmrc` 18 aparece como incompatível; o diff que sobe o `TargetFramework` numa tarefa comum é apontado |
| MV-4 | Funcionalidade | Ecossistemas fora da descoberta | **Feito** (ver abaixo). PHP (`composer.json`/`composer.lock`, `platform.php`), Ruby (`Gemfile`, `.ruby-version`), C/C++ (CMake `cmake_minimum_required`, `CMAKE_CXX_STANDARD`), Kotlin/Android (`compileSdk`, AGP), Swift; `.tool-versions`, `mise.toml`, Volta, Gradle Wrapper, `toolchain` do `go.mod`; Microsoft Agent Framework e Semantic Kernel como frameworks; catálogo com PHP, Ruby e Kotlin | MV-1 | M | Um fixture por ecossistema com versão e evidência |
| MV-5 | Arquitetura | Sem política de seleção executável | **Feito** (ver abaixo). `VersionPolicy`: dado o pedido (tecnologias e objetivo), devolve a combinação recomendada, as alternativas (LTS ou mais nova) e o motivo de cada escolha, só com dados do catálogo | MV-3 | M | Mesmo pedido e mesmo catálogo dão a mesma escolha; pré-lançamento nunca sai como padrão; linha com menos de 12 meses de suporte não é recomendada |
| MV-6 | Funcionalidade | O Spock não cria projeto | `project.scaffold` sobre a jornada "Da ideia ao projeto" (W4-3): versões do MV-5, comandos oficiais de criação (`dotnet new`, `npm create vite`) com versão fixada, registro `spock-versions.json` no projeto gerado, build e teste do projeto gerado | MV-5, W4-3 | A | O projeto .NET + React + Vite gerado compila e passa nos testes; o registro diz versão, motivo, fonte e data |
| MV-7 | Funcionalidade | Modernização misturada a tarefas | `modernization.plan` (W4-2) consumindo a compatibilidade: saltos por linha, breaking changes da fonte oficial, passos reversíveis, rollback, aprovação por passo | MV-1, W4-1 | A | Plano para .NET 6 → 10 em passos (6 → 8 → 10) com critério de aceite por passo; nenhuma mudança aplicada sem aprovação |
| MV-8 | Modernização do Spock | Runtime embarcado e CI desatualizados | (a) **feito** (MV-8a, ver abaixo): CPython embarcado 3.12.10 → 3.12.16; (b) **feito** (MV-8b, ver abaixo): `global.json` com SDK mínimo 10.0.404 e `latestFeature`; (c) GitHub Actions para os majors atuais, uma por vez; (d) Dependabot para NuGet, npm, Cargo, pip e Actions, agrupado por ecossistema, com a política de patch/minor/major e sem merge automático | **DD-MV-1** para (c) e (d) | M | Smoke do instalador verde; `generate_sbom.py --check` verde; Dependabot abre PR agrupado com o rótulo do tipo de atualização |
| MV-9 | Modernização do Spock | Majors pendentes na interface e nos testes | Uma entrega por item: TypeScript 7 + typescript-eslint (**bloqueado**, ver abaixo); ESLint 10 (**bloqueado**, ver abaixo); Vitest 5.1 (**feito**, com Vite e plugin-react); Tauri 2.12.x alinhado (crate, CLI, API, plugins; **feito**, ver abaixo); Microsoft.NET.Test.Sdk 18 + coverlet 10 (**feito**, ver abaixo); xUnit v2 → v3 (**feito**, ver abaixo); ProtectedData no trem 10.0.x (**feito**, ver abaixo); pnpm 9 → 12 (**feito**, ver abaixo); Python 3.12 → 3.14 (**feito**, com `sqlite-vec` validado; ver abaixo) | MV-8 | M cada | Por item: build, testes, lint, smoke e conformidade de licenças verdes, com versão anterior, nova, fonte e testes registrados no PR |
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

## MV-8b: SDK mínimo no `global.json` (2026-11-19)

| | |
|---|---|
| Antes | Sem `global.json`: o SDK que compilava era o da máquina. Nesta máquina era o 10.0.402, que embute no motor *self-contained* o runtime 10.0.13 |
| Depois | `global.json` na raiz com `sdk.version` 10.0.404, `rollForward: latestFeature` e `allowPrerelease: false`. Aceita SDK 10.0.404 ou mais novo dentro do 10.0, nunca o 11 nem pré-lançamento, e recusa SDK sem as correções |
| Motivo | O .NET 10.0.15 (SDK 10.0.404, 2026-11-10) corrige 16 CVEs, entre elas a CVE-2026-105544 no VSTest que vem dentro do SDK, e o 10.0.14 corrige outras quatro (`builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json`) |
| Máquina de desenvolvimento | SDK 10.0.404 instalado em 2026-11-19 pelo pacote oficial `Microsoft.DotNet.SDK.10` do winget (instalador de `download.microsoft.com`, SHA-256 conferido pelo winget). O 10.0.402 segue instalado ao lado |
| Inventário de licenças | `generate_sbom.py` troca o 10.0.13 fixo pela constante `DOTNET_RUNTIME = "10.0.15"`, amarrada ao `global.json`. Os textos `LICENSE.TXT` e `THIRD-PARTY-NOTICES.TXT` do runtime são iguais aos da 10.0.13, e os do pacote linux-x64 são iguais aos do win-x64 a menos da quebra de linha (conferido) |
| CI | `setup-dotnet` com `10.0.x` instala o SDK mais novo da linha, que satisfaz o `global.json` |
| Testes | `dotnet --version` no repositório responde 10.0.404; publicação *self-contained* do motor com `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/10.0.15` no `deps.json`; suíte do motor; `generate_sbom.py --check` e os testes do gerador |

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

## MV-4: ecossistemas na descoberta (2026-11-19)

| | |
|---|---|
| Stacks novas no registro | PHP, Ruby, C/C++ e Swift, no fim da ordem do registro (não mudam quem vence na leitura da arquitetura; "PHP com Laravel" passa a ser `php`). Sem regra do Harness: o plano registra a limitação, como já fazia com Python, Java, Go e Rust |
| Versão de cada uma | PHP: `config.platform.php`, depois `.php-version`, depois o mínimo do `require.php`. Ruby: `.ruby-version`, depois o `ruby` do Gemfile, depois o `RUBY VERSION` do lock. C/C++: padrão da linguagem do CMake (C++20), com o CMake mínimo como nota. Swift: `swift-tools-version` |
| Frameworks | Laravel, Symfony, Slim, Doctrine, PHPUnit, Pest; Rails, Sinatra, Sidekiq, RSpec, Minitest (versão do `Gemfile.lock`); Kotlin e Android Gradle Plugin pelo bloco `plugins` do Gradle; Microsoft Agent Framework em .NET (`Microsoft.Agents.AI*`) e Python (`agent-framework`, `agent-framework-core`); Semantic Kernel também em Python |
| Runtime fixado | `.tool-versions` (asdf), `mise.toml`, `volta.node`, `.sdkmanrc`, `toolchain` do `go.mod` e o Gradle do wrapper, para a pasta e as de baixo. No Node, a ordem é `.nvmrc`/`.node-version`, Volta, gerenciador e, por fim, o mínimo do `engines`. Valor sem número (`lts`, `system`) não vira versão |
| Catálogo | PHP (`php.net/releases/states.php`: suporte ativo, só segurança, fim) e Ruby (`branches.yml` do site oficial). Hoje: PHP 8.2 só segurança até 2026-12-31, 8.1 sem suporte; Ruby 3.3 em manutenção legada até 2027-03-31, 3.2 sem suporte desde 2026-04-01 |
| Fora desta entrega | `compileSdk`/`targetSdk` do Android, catálogo de Kotlin, Swift e C++ (sem fonte oficial de ciclo de vida legível), projetos só Xcode, Makefile e Bazel |
| Testes | `EcosystemDiscoveryTests` (12 casos: PHP com plataforma, `.php-version` e mínimo; Ruby com `.ruby-version` e lock; CMake com subpasta; SwiftPM; Android com Kotlin e wrapper; asdf com Volta vencendo; mise, toolchain do Go e SDKMAN; Agent Framework e Semantic Kernel; situação de suporte de PHP e Ruby pelo catálogo embutido). Registro: "PHP com Laravel", "Ruby on Rails" e "C++ com CMake". Gerador do catálogo: 15 testes |

## MV-9, item 1: Tauri alinhado (2026-11-19)

| Componente | Antes | Depois | Fonte |
|---|---|---|---|
| `tauri` (crate) | 2.12.5 | 2.12.7 | crates.io; notas `tauri-v2.12.6` e `tauri-v2.12.7` (sem mudança incompatível; só a auditoria do cargo) |
| `tauri-build` | 2.7.4 | 2.7.6 | crates.io |
| `tauri-plugin-dialog` | 2.8.3 | 2.8.4 | `dialog-v2.8.4`: `rfd` 0.16 → 0.17 |
| `tauri-plugin-shell` | 2.5.0 | 2.6.0 | `shell-v2.6.0`: só acrescenta `Command::kill_on_drop` |
| `tauri-plugin-single-instance` | 2.5.4 | 2.5.5 | `single-instance-v2.5.5`: corrige corrida no Windows e solta a trava em `cleanup_before_exit` |
| `@tauri-apps/cli` | 2.12.4 | 2.12.6 | npm; `cli-v2.12.5` corrige `signCommand` no Windows, `cli-v2.12.6`, o servidor de desenvolvimento |
| `@tauri-apps/api` | 2.12.4 | 2.12.5 | npm; `core.js` idêntico; muda `mocks` e `window` |

- **Alinhamento verificado na suíte.** `TauriAlignmentTests` confere que o crate `tauri`, a CLI e a API do npm estão na mesma versão maior e menor, que estão fixados sem faixa, e que `tauri-build` e os plugins seguem a versão maior do crate. Conferido que o teste falha com a API em 2.11.0.
- **Escopo do `cargo update`.** Restrito aos pacotes do Tauri (`-p`): mudaram só a árvore do Tauri, o `rfd` e as crates de hash que o `tauri-codegen` 2.7.6 traz. O `pnpm-lock.yaml` mudou só nos pacotes `@tauri-apps`.
- **Achado.** Depois da troca do `@tauri-apps/api`, 22 testes da interface falharam localmente sem defeito: o cache de módulos do Vitest (`node_modules/.vitest-cache`, na raiz) guardava o caminho resolvido da versão antiga, e o `vi.mock` deixou de interceptar. Registrado em [POLITICA.md](POLITICA.md).
- **Testes.** Motor 1.817 e 12 de integração; host em Rust com `cargo fmt --check`, `cargo clippy -D warnings` e 50 testes; interface com typecheck, lint, formatação e 860 testes; `build-runtime.ps1 -FetchPython`; instalador NSIS; smoke do instalador e motor instalado de ponta a ponta (ver o PR); `generate_sbom.py --check` com o manifesto de avisos completo. Linux e macOS não foram executados.

## MV-9, item 2: Vitest, Vite e plugin-react (2026-11-19)

| Componente | Antes | Depois | Fonte |
|---|---|---|---|
| `vitest` | 5.0.5 | 5.1.1 | Notas `v5.1.0` e `v5.1.1` (recursos e correções, sem mudança incompatível) |
| `vite` | 8.3.5 | 8.3.8 | `CHANGELOG.md` do Vite (só patches) |
| `@vitejs/plugin-react` | 6.1.2 | 6.3.0 | `CHANGELOG.md` do plugin (opções novas de fast refresh e correções) |
| `rolldown` (transitivo do Vite) | 1.2.15 | 1.2.19 | Puxado pelo Vite 8.3.8 |

- Sobem juntas porque o Vitest roda sobre o Vite e o plugin-react exige o Vite 8.
- **Pré-lançamento transitivo.** O lock passou a listar `@emnapi/core` e `@emnapi/runtime` 2.0.0-beta.2. O próprio rolldown fixa essas versões, e elas servem só à variante `@rolldown/binding-wasm32-wasi`, que é opcional e marcada `cpu: wasm32`. Não são instaladas em Windows, Linux nem macOS (conferido em `node_modules/.pnpm`), e nada disso vai no instalador.
- **Aviso de tamanho do chunk.** O aviso do build (`index` com 1,56 MB) já existia no master, com o mesmo tamanho (1.562,76 kB antes, 1.562,84 kB depois).
- **Testes.** Typecheck, lint, formatação e 860 testes da interface; `pnpm ui:build`; `generate_sbom.py --check` com o manifesto de avisos completo.

## MV-9, item 3: Microsoft.NET.Test.Sdk e coverlet (2026-11-19)

| Componente | Antes | Depois | Fonte |
|---|---|---|---|
| `Microsoft.NET.Test.Sdk` (e `TestHost`, `ObjectModel`, `CodeCoverage`) | 17.14.1 | 18.12.0 | Notas do `microsoft/vstest` de v18.0.0 a v18.12.0: nenhuma seção de mudança incompatível |
| `coverlet.collector` | 6.0.4 | 10.1.1 | `Documentation/Changelog.md` do coverlet. A única mudança incompatível no caminho é exigir .NET 8 ou mais novo (8.0.0); a 10.0.0 traz o suporte ao .NET 10 |

- Só pacotes de teste, nos dois projetos de teste. Nada vai no instalador.
- **Testes.** Motor 1.817 e 12 de integração. Coleta real de cobertura (`--collect:"XPlat Code Coverage"`), com 1.483 classes no Cobertura. Smoke do pacote com logger TRX, conferido pelo `scripts/assert-test-passed.py` como no CI. `generate_sbom.py --check`.

### Achado: SDK do .NET desta máquina sem dois releases de segurança

A nota do VSTest 18.12.0 corrige a CVE-2026-105544 (elevação de privilégio no pós-processador de artefatos do `Microsoft.TestPlatform.CLI`), que vem dentro do .NET SDK. A correção é o SDK 10.0.404, do .NET 10.0.15 (2026-11-10), que corrige 16 CVEs. O 10.0.14 (SDK 10.0.403) corrige outras quatro (`builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json`).

A máquina de desenvolvimento tem o **SDK 10.0.402** (runtime 10.0.13). O motor é publicado *self-contained*, e o inventário de licenças registra o runtime `Microsoft.NETCore.App.Runtime.win-x64@10.0.13`. Um instalador gerado nesta máquina leva um runtime sem esses dois releases de segurança. O CI usa `dotnet-version: 10.0.x` e pegaria o mais novo, mas está sem orçamento. Isso sobe a prioridade do **MV-8 (b)**: o `global.json` com SDK mínimo 10.0.404 faz o build recusar um SDK sem as correções. Depende de atualizar o SDK desta máquina.

## MV-9, item 4: xUnit v2 → v3 (2026-11-20)

| Componente | Antes | Depois | Fonte |
|---|---|---|---|
| `xunit` | 2.9.3 | `xunit.v3.mtp-off` 4.0.3 | Guia `xunit.net/docs/getting-started/v3/migration`; notas `xunit.net/releases/v3/4.0.0` a `4.0.3` |
| `xunit.runner.visualstudio` | 3.1.4 | 4.0.1 | `xunit.net/releases/visualstudio/4.0.0` (suporta projetos v3 4.0) |

- **Por que `mtp-off`.** Com o SDK .NET 10, o v3 pode usar o Microsoft.Testing.Platform, e aí o `dotnet test` via VSTest deixa de funcionar. O CI e o `scripts/assert-test-passed.py` dependem do VSTest (`--filter`, logger TRX). A variante `mtp-off` é o mesmo v3 4.0.3 com o MTP desligado. O smoke do pacote, com filtro e TRX, passa no script do CI.
- **Mudanças na suíte.** Projetos de teste com `OutputType` `Exe`. O `using Xunit.Abstractions` sai dos testes ao vivo. Os atributos `Fact` próprios, que decidem `Skip` no construtor, ganham o construtor com arquivo e linha (`xUnit3003`); o `LiveFact` usa `params` e fica com o aviso suprimido, com o motivo escrito. O `xUnit1051` (analisador novo) fica desligado nos projetos de teste com o motivo no csproj. Os testes passam `default` de propósito (mapeamento de erro sem cancelamento, esperas curtas), e adotar o `TestContext.Current.CancellationToken` em 59 chamadas é uma mudança à parte. Novos transitivos só de teste: `Microsoft.Win32.Registry` 5.0.0 e `System.Security.AccessControl` 6.0.1, vindos do `xunit.v3.runner.common` (MIT, não distribuídos).
- **Desempenho na máquina de desenvolvimento.** Medição limpa: três pares alternados, uma rodada por vez, sem processos órfãos.

  | Par | v2 | v3 |
  |---|---|---|
  | 1 | 99 s | 131 s |
  | 2 | 149 s | 331 s |
  | 3 | 114 s | 157 s |
  | Mediana | 114 s | 157 s (+38%) |

  Os testes rápidos não mudam. Ficam mais lentos só os que gravam muitos arquivos e abrem processos, e só com vários ao mesmo tempo; uma classe sozinha leva o mesmo tempo. GC, prioridade, CPUs, pool de threads e variáveis de ambiente são iguais. A diferença é a imagem do processo: no v2 os testes rodam no `testhost` da Microsoft, assinado; no v3, no executável do projeto, sem assinatura. O antivírus ativo na máquina é o Kaspersky (o Defender está desligado), que vigia mais esse tipo de executável. Essa é a causa provável, inferida pelos sintomas e não comprovada desligando o antivírus. O CI de testes roda em Linux e não é afetado. Na máquina de desenvolvimento, uma exclusão do antivírus para as pastas `bin` resolveria; é decisão do dono.
- **Cuidado ao medir.** Rodadas interrompidas (fim de sessão ou parada do processo em segundo plano) deixaram `dotnet test`, `vstest.console` e `testhost` vivos. Eles seguiram rodando a suíte em paralelo com as rodadas seguintes e contaminaram as primeiras medições (de 3 min 50 s a 4 min 52 s). Antes de medir, confira que não há `testhost` nem `Mariliz.Application.Tests` rodando.
- **Falhas intermitentes vistas na medição.**
  - `WorkflowRobustnessTests.Aprovacao_mais_lenta_que_o_prazo_do_runtime_ainda_conclui`: o prazo de 150 ms cobria a retomada inteira (estado em disco e eventos). Corrigido, com prazo de 1 s e espera de 2 s.
  - `SecurityValidationTests.Um_projeto_nao_enxerga_a_execucao_nem_o_conhecimento_do_outro`: a aprovação da IA em Python não abre em 20 s com a máquina cheia. **Acontece também no v2**; já existia.
  - `SafetyTests.Plano_com_caminho_fora_do_escopo_falha_antes_de_gravar_e_desfaz_o_que_ja_foi_gravado`: `FileNotFoundException` no `CLAUDE.MD.tmp` da gravação atômica, uma vez no v3. É compatível com o antivírus segurando o arquivo temporário. Não investigado a fundo.
- **Testes.** Motor 1.817 aprovados e 28 ignorados, os mesmos do v2. O `Skip` decidido no construtor continua valendo. Integração com 12 testes. Smoke do pacote com TRX e `assert-test-passed.py`. `generate_sbom.py --check`.

## MV-5: política de versões para projeto novo (2026-11-20)

| | |
|---|---|
| O que faz | `VersionPolicy.Recommend(tecnologias, objetivo, catálogo, data)` devolve, por tecnologia, a linha escolhida com versão, canal, fim de suporte, versão da linguagem (C# por .NET) e motivo; as alternativas aceitas pela política, cada uma com o motivo; e notas (fim de suporte não publicado, linha recém-lançada, fonte que falhou, requisito entre tecnologias). Função pura: só o catálogo embutido e a data |
| Regras (POLITICA.md, seção 2) | Só linha `active` e fora de `preview`. Linha com fim de suporte a menos de 12 meses fica de fora, com nota (hoje: .NET 9, até 2027-05-12). Objetivo `production` (padrão): LTS ativa mais nova; no empate de fim de suporte com uma STS mais nova (.NET 10 e 11, ambas até 2028-11-14), ganha a LTS, e a STS vira alternativa. Objetivo `latest`: a estável mais nova, com a LTS como alternativa para produção. Tecnologia sem LTS: a linha mais nova, com o motivo pela política da fonte (`latest-major`, `rolling`, `supported-window`) |
| Combinação | O Node é escolhido primeiro. Cada pacote do npm fica com a linha preferida que aceita o Node escolhido (`requires.node` do catálogo, avaliado pelo `NodeRange` do MV-3); uma linha mais antiga só entra quando a preferida não aceita, com nota. Sem Node no pedido, o requisito vira nota |
| Limite conhecido | Do requisito entre tecnologias, o catálogo só tem o Node exigido pelos pacotes do npm. Faixas como React → TypeScript ou typescript-eslint → TypeScript não estão nele: hoje a política recomenda TypeScript 7, que o typescript-eslint ainda não aceita (MV-9). O MV-6 confere as faixas reais na instalação do projeto gerado; levar esses requisitos ao catálogo é uma extensão do gerador |
| Onde | `runtime/Mariliz.Application/Compatibility/VersionPolicy.cs`; CLI `mariliz recommend <tecnologias> [--goal production\|latest] [--json]`, sem projeto e sem rede. A ferramenta MCP e a criação do projeto ficam com o MV-6, que consome a política |
| Testes | `VersionPolicyTests` (13): LTS em produção com o empate explicado, STS no objetivo `latest`, pré-lançamento nunca escolhido nem como alternativa, folga de 12 meses, mesma resposta para o mesmo pedido (também com a ordem invertida), queda para a linha que aceita o Node, aviso de requisito sem Node, linha recém-lançada, fornecedor sem data, fonte que falhou, tecnologia fora do catálogo, pedido inválido e o catálogo embutido inteiro. `CliTests` (3): saída em texto e JSON, sem processo externo, e erros de uso |

## MV-9, item 5: pnpm 9.1.2 → 12.21.0 (2026-11-20)

| | |
|---|---|
| Antes → depois | `packageManager` `pnpm@9.1.2` → `pnpm@12.21.0` (`registry.npmjs.org/pnpm/latest`) |
| Fontes | Notas `github.com/pnpm/pnpm/releases/tag/v10.0.0`, `v11.0.0` e `v12.0.0`; guia `pnpm.io/11.x/migration` |
| Scripts de instalação | Desde o 11, script de dependência fora do `allowBuilds` (em `pnpm-workspace.yaml`) é erro. Só o `@parcel/watcher` (via `sass-embedded` e `vite`) tem script, e fica recusado (`false`): o script só compila do código-fonte quando falta o binário pronto, que já vem pelas dependências opcionais de cada plataforma |
| Lockfile | O formato 9.0 continua. O pnpm 12 grava antes do documento do projeto um documento YAML com `packageManagerDependencies`, que fixa o próprio pnpm com o hash de cada binário por plataforma. `--frozen-lockfile` passa |
| Node | O pnpm 11+ exige Node 22 ou mais novo: o `engines` (`>=22`) e o CI (Node 22) já atendem |
| CI | `pnpm/action-setup` sem `version`: a ação lê o `packageManager`, a fonte única |
| Inventário de licenças | Três ajustes no `generate_sbom.py`. (1) `pnpm licenses list` com `-r`, porque num workspace o pnpm 10+ sem `-r` lista só o pacote da raiz e devolvia `{}`. (2) As duas leituras do lockfile usam só o último documento, o do projeto: o documento do pnpm tem `snapshots:` próprio, e a leitura dos binários por plataforma parava nele. (3) Com isso o `@types/react-dom` deixa de ser contado como produção e sai dos avisos do instalador; está certo, porque é só de tipos e não vai no bundle |
| Máquina de desenvolvimento | O `pnpm` global antigo (9.1.2) quebra neste repositório: ao ver o `packageManager` em 12.21.0, tenta delegar e monta um caminho de atalho inválido no Windows. Quem trabalha no repositório precisa do pnpm 12 global (`npm install -g pnpm@12.21.0`) |
| Testes | Instalação limpa com `--frozen-lockfile`; interface com typecheck, lint, formatação e 860 testes; `pnpm ui:build`; `pnpm tauri --version` no host (`tauri-cli 2.12.6`); `pnpm audit --prod` sem vulnerabilidades conhecidas; `generate_sbom.py --check` e testes do gerador |

## MV-9, item 6: Python 3.12.16 → 3.14.10 (2026-11-20)

| | |
|---|---|
| Antes → depois | CPython embarcado 3.12.16 → **3.14.10**, do mesmo python-build-standalone 20261110, com o SHA-256 de cada plataforma do `SHA256SUMS` da release. CI (`setup-python`), ruff (`py314`) e lock (`--python-version 3.14`) acompanham |
| Por que 3.14 | Pelo catálogo oficial (`peps.python.org/api/release-cycle.json`), a 3.14 recebe correções gerais até 2027-10 e segurança até 2030-10. A 3.13 passou a só segurança em 2026-10. A 3.15 saiu em 2026-10-09 e tem um mês; fica para quando as dependências tiverem histórico nela |
| Dependências | Nenhuma versão mudou. Todas as nativas (`orjson`, `ormsgpack`, `pydantic-core`, `pyyaml`, `uuid-utils`, `xxhash`, `zstandard`, `sqlite-vec`) já publicam wheel `cp314` (ou `py3-none`) para Windows x64, manylinux x86_64 e macOS arm64, conferido no PyPI. O lock recompilado só ganhou o cabeçalho do `uv` |
| `sqlite-vec` | 0.1.9, dependência do `langgraph-checkpoint-sqlite`. Validado no interpretador embarcado 3.14.10 com os pacotes em bytecode: a extensão carrega (`vec_version()` = `v0.1.9`, SQLite 3.53.1) e uma tabela `vec0` responde à busca por vizinhos |
| Mudanças do PBS na 3.14 | No Linux e no macOS, o executável traz o interpretador inteiro (libpython estático) e não carrega o `libpython3.14.so`/`.dylib`. `_dbm` e `_tkinter` viraram módulos separados em `lib-dynload`. No Windows, entram `_zstd.pyd`, o zlib-ng 2.2.4 no `zlib1.dll` (API zlib 1.3.1) e o libmpdec 4.0.0; o Tcl 9 traz `libtommath.dll` |
| Poda | Unix: fica um único executável, com o nome `bin/python3` que o host e o motor procuram (antes, a cópia do `python3.12` duplicaria 32 MB). Saem o `libpython`, o `_dbm`, o `_tkinter` e o `libtcl*`. Windows: sai também o `libtommath.dll`. Tamanhos: Windows 34,4 MB medidos (orçamento 40); Linux ~40,6 MB e macOS ~27,9 MB estimados pelo arquivo oficial (orçamentos 45 e 30; o do Linux baixou de 55) |
| Licenças | **O Berkeley DB (Sleepycat) sai do pacote Linux.** Na 3.14 ele fica só no `_dbm`, que a poda tira: era a remoção pedida na D-05, impossível na 3.12. `scripts/notices/berkeley-db/` sai, o R-02 fica encerrado e a D-05 superada, **com a remoção confirmada pelo dono em 2026-11-20**. Saem também Tcl/Tk e X11 do Linux. No Windows, os avisos ganham zstd e zlib-ng no lugar do zlib. Inventário: 962 componentes (−1), sem Sleepycat; política com zero bloqueantes |
| Python 3.14 no código | Intelligence: ruff limpo com `py314`; 228 testes verdes com `-X dev`, sem aviso vindo do código do Spock (só uma depreciação dentro do `langsmith`, prevista para a 3.16). Scripts e ferramentas de conformidade verdes na 3.14 |
| Testes | `package-intelligence.py bundle --python x86_64-pc-windows-msvc`: interpretador 3.14.10 isolado (`ignore_environment`, `sys.path` só do pacote), dependências 28,4 MB. `Pacote_embarcado` com `MARILIZ_REQUIRE_PACKAGED=1` verde: o grafo do LangGraph sobe do bytecode. Suíte do motor com `MARILIZ_PYTHON` na 3.14: 1.817 e 12 de integração. Dois testes ajustados: a descoberta do próprio Spock lê `py314`, e o ack real do runtime passou a respeitar o `MARILIZ_PYTHON` (pegava o primeiro Python do PATH). Instalador NSIS real (47,39 MiB) aberto pelo `smoke-installer.py`: `Pacote_embarcado` e os dois `PackagedEngineE2ETests` verdes com o Python de dentro dele; desinstalado depois. `generate_sbom.py --check` em dia |
| Não executado | Linux e macOS: o pacote e o smoke só rodam no CI, que segue sem orçamento de Actions. O smoke do Linux confere que o `_dbm` está fora e que os avisos do zstd e do ncurses vão junto |
| Máquina de desenvolvimento | `package-intelligence.py` exige Python 3.14 (o bytecode só carrega na mesma versão menor). Aqui foi usado o 3.14.7 do `uv` (`uv python install 3.14 --no-bin`, sem mexer no PATH); o `uv` 0.12.15 ainda não conhecia a release 20261110 |

## MV-9, item 7: System.Security.Cryptography.ProtectedData 9.0.11 → 10.0.15 (2026-11-20)

| | |
|---|---|
| Antes → depois | `9.0.11` → `10.0.15` (`api.nuget.org`, `system.security.cryptography.protecteddata/index.json`), a mesma linha do runtime que o motor embute (`DOTNET_RUNTIME` 10.0.15). A 11.0.0 estável acompanha o .NET 11 e fica com o MV-10 (DD-MV-3) |
| Bloqueio resolvido | O pacote 10.0.15 só declara a expressão `MIT` no nuspec, sem o arquivo de licença, e aponta para `github.com/dotnet/dotnet` (commit `8e0f8ed`). A entrada `dotnet` do `fetch_upstream_licenses.py`, que já cobre `Microsoft.Extensions.*`, `System.Memory.Data` e outros do mesmo repositório, passa a cobrir também o ProtectedData. O texto é o `LICENSE.TXT` já fixado no commit `c2435c3` (MIT), sem download novo. Na conferência, o pacote passa de "CONFERE" para "SÓ EXPRESSÃO", como os demais pacotes do `dotnet/dotnet` |
| Impacto | Só o `DpapiSecretVault` usa o pacote, e ele chama o DPAPI do Windows (`CryptProtectData`). O formato do segredo é do sistema operacional, então os segredos gravados com a 9.0.11 continuam legíveis |
| Testes | Build da solução sem erro (os três avisos de teste já existiam). 109 testes do cofre e das credenciais, incluindo gravação e releitura pelo DPAPI real. Suíte do motor verde: 1.817 e 12 de integração. `generate_sbom.py --check` em dia, manifesto dos avisos completo com o texto do ProtectedData 10.0.15 |

## MV-9: itens bloqueados por compatibilidade (consulta de 2026-11-19)

| Item | Bloqueio | Fonte | Quando reavaliar |
|---|---|---|---|
| TypeScript 6 → 7 | O `typescript-eslint` 8.73.0 (o mais novo) declara `typescript` `>=4.8.4 <6.1.0`. Com o TypeScript 7, o lint da interface fica fora do suportado | `registry.npmjs.org/typescript-eslint/latest` (`peerDependencies`) | Quando o `typescript-eslint` aceitar o TypeScript 7. O MV-2 (catálogo periódico) pode avisar |
| ESLint 9 → 10 | O `eslint-plugin-jsx-a11y` 6.10.2 (o mais novo) declara `eslint` só até `^9`. O `typescript-eslint` e o `eslint-plugin-react-hooks` já aceitam o 10 | `registry.npmjs.org/eslint-plugin-jsx-a11y/latest` | Quando o `jsx-a11y` aceitar o ESLint 10, ou se a decisão for trocar de plugin de acessibilidade |

## Decisões que dependem do dono

| Id | Decisão | Opções | Recomendação |
|---|---|---|---|
| DD-MV-1 | Automação em GitHub Actions (MV-2, MV-8 c/d) com o CI sem orçamento | (a) liberar orçamento; (b) Dependabot só com PR, validação local registrada; (c) adiar | (b) até o orçamento voltar. Atualização sem CI verde não tem merge |
| DD-MV-2 | Catálogo também atualizado pelo motor na máquina do usuário (cache local com TTL, fallback no embutido)? | (a) só por release do Spock; (b) motor consulta as fontes oficiais com o consentimento do usuário | (a) por enquanto: sem rede no caminho de desenvolvimento e com revisão de cada mudança. (b) quando o MV-6 precisar de versão do dia para criar projeto |
| DD-MV-3 | .NET 11 (STS) no motor | (a) ficar no 10 LTS até o 12 LTS; (b) migrar depois do MV-10 | (a), salvo ganho medido relevante no MV-10 |
| DD-MV-4 | Cobrar `confirmed=false` (faixa declarada) como aviso na descoberta? | Sim ou não | Não: já vai como instrução ao agente; aviso duplicaria |
