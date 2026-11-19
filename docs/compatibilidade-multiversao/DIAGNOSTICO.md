# Diagnóstico tecnológico do Spock

Consulta feita em **2026-11-18**. A versão usada vem dos manifestos e lockfiles do repositório, não da documentação. A versão disponível vem de fontes oficiais consultadas nesse dia: índices de release, registries e páginas de suporte, listados ao fim. Quando algo não foi confirmado, está escrito "não verificado".

## 1. Inventário de versões

### Motor (.NET)

| Componente | Usado | Estável mais recente | Situação | Observação |
|---|---|---|---|---|
| .NET (TargetFramework) | `net10.0` em todos os projetos | 11.0.0 (STS, 2026-11-10); 10.0.15 (LTS) | 10.0 LTS ativo até 2028-11-14 | Sem `global.json`: o SDK que compila depende da máquina (CI fixa `10.0.x`) |
| SDK local | 10.0.402 | 10.0.404 / 11.0.100 | Suportado | |
| ModelContextProtocol.Core | 2.4.0 | 2.4.0 | Atual | |
| YamlDotNet | 18.1.0 | 18.1.0 | Atual | |
| Microsoft.ML.Tokenizers (+O200kBase) | 3.0.0 | 3.0.0 | Atual | |
| System.Security.Cryptography.ProtectedData | 9.0.11 | 10.0.15 (trem do .NET 10); 11.0.0 | Desalinhado do TFM | Tentamos atualizar para 10.0.15 e revertemos: o pacote novo só declara "MIT" e não traz o texto da licença, e a conferência de avisos do instalador falha. Ver MV-9 |
| AWSSDK.* | 4.1.0–4.1.2 | Core 4.1.1.1 | Atual na linha 4.1 | |
| Azure.Identity / Azure.ResourceManager | 1.21.0 / 1.14.0 | 1.21.0 / não verificado | | |
| Google.Apis.* | 1.78.0 | não verificado | | |
| Microsoft.NET.Test.Sdk | 17.14.1 | 18.12.0 | Major atrás | Exige rodar a suíte inteira e o smoke do pacote |
| xunit / xunit.runner.visualstudio | 2.9.3 / 3.1.4 | xunit.v3 4.0.3 / 4.0.1 | xUnit v2 sem novas versões | Migrar para v3 é major e muda pacote e API (MV-9) |
| coverlet.collector | 6.0.4 | 10.1.1 | Major atrás | |
| JsonSchema.Net | 9.5.0 | 9.7.0 | Minor atrás | Licença com taxa já sinalizada na auditoria de licenças |

### Interface (React)

| Componente | Declarado / instalado | Estável mais recente | Situação |
|---|---|---|---|
| React / React DOM | ^19.3.0 / 19.3.1 | 19.3.1 | Atual |
| Vite | ^8.3.2 / 8.3.5 | 8.3.8 (beta 8.4.0) | Patch atrás |
| TypeScript | ~6.0.3 / 6.0.3 | **7.0.2** | Major atrás. O TypeScript 7 é o compilador nativo, e precisa de avaliação própria com `typescript-eslint` (MV-9) |
| Vitest | ^5.0.3 / 5.0.5 | 5.1.1 | Minor atrás |
| ESLint | ^9.39.5 | **10.15.0** | Major atrás |
| typescript-eslint | ^8.71.0 | 8.73.0 | Minor atrás |
| @mui/material | ^9.4.0 / 9.5.0 | 9.5.0 | Atual |
| react-router | ^8.4.0 / 8.5.0 | 8.5.2 | Patch atrás |
| zod / react-hook-form | 4.6.7 / ^7.89.0 | 4.6.7 / 7.92.0 | Atual / minor atrás |
| pnpm (`packageManager`) | 9.1.2 | 12.21.0 | Três majors atrás. A situação de suporte do pnpm 9 não foi verificada |
| Node.js (CI e `engines`) | 22 / `>=22` | 26.13.1 (LTS ativa) | 22 em manutenção até **2027-04-30** |
| Node.js na máquina de desenvolvimento | v25.2.1 | | **Sem suporte desde 2026-06-01** (linha ímpar) |

### Desktop (Tauri e Rust)

| Componente | Lock | Estável mais recente | Situação |
|---|---|---|---|
| tauri | 2.12.5 | 2.12.7 (3.0.0-alpha.6 em pré-lançamento) | Patch atrás |
| tauri-build | 2.7.4 | 2.7.6 | Patch atrás |
| @tauri-apps/cli / @tauri-apps/api | 2.12.4 / 2.12.4 | 2.12.6 / 2.12.5 | Patch atrás. CLI, API, crate e plugins precisam subir juntos |
| tauri-plugin-dialog / shell / single-instance | 2.8.3 / 2.5.0 / 2.5.4 | 2.8.4 / 2.6.0 / 2.5.5 | Patch / minor atrás |
| tokio / serde / serde_json | 1.54.1 / 1.0.229 / 1.0.154 | iguais | Atual |
| portable-pty | 0.9.0 | 0.9.0 (crate sem publicação desde 2025-02-11) | Manutenção a observar |
| Rust (`rust-version`, mínimo) | 1.90 | 1.100.0 | Mínimo declarado, não a versão em uso (local 1.99.1) |

### Inteligência (Python)

| Componente | Usado | Estável mais recente | Situação |
|---|---|---|---|
| Python (CI, ruff `py312`, lock) | 3.12 | 3.15.2 | 3.12 só recebe correções de segurança, até 2028-10 |
| **CPython embarcado no instalador** | **3.12.10** (python-build-standalone 20250409) | 3.12.16 | **Seis correções de segurança atrás.** Achado mais urgente do diagnóstico (MV-8) |
| langgraph | 1.2.19 | 1.2.22 | Patch atrás |
| langgraph-checkpoint-sqlite | 3.1.3 | 3.1.3 | Atual |
| langchain-core | 1.7.4 (transitiva) | 1.8.3, exige Python >=3.11 | Minor atrás |
| pydantic | 2.14.2 | 2.14.3 | Patch atrás |
| sqlite-vec | 0.1.9 | 0.1.9 | Atual. Pacote em 0.x, sem `requires_python` |
| ruff (CI) | 0.17.4 | 0.17.5 | Patch atrás |

### Automação (GitHub Actions)

| Ação | Usada | Mais recente | Situação |
|---|---|---|---|
| actions/checkout | v4 | v7.0.1 | Três majors atrás |
| actions/setup-dotnet | v4 | v6.1.0 | |
| actions/setup-python | v5 | v7.1.0 | |
| actions/setup-node | v4 | v7.1.0 | |
| pnpm/action-setup | v4 | v6.1.1 | |
| actions/upload-artifact | v4 | v7.0.2 | |

O repositório não tem Dependabot nem Renovate, nem imagem de container. As ações em majors antigos podem depender de um runtime Node que o GitHub já desligou ou vai desligar. Isso não foi verificado e é motivo para tratar as ações como uma entrega à parte (MV-8). O CI do repositório está sem orçamento de minutos, então qualquer automação que dependa de Actions fica bloqueada até o dono decidir.

## 2. Motor .NET: .NET 10 para .NET 11

Pelo índice oficial, o **.NET 11 é STS** e tem fim de suporte em **2028-11-14**, a mesma data do .NET 10 LTS. Migrar agora **não ganha tempo de suporte**. O .NET 8 e o .NET 9 saíram de suporte em 2026-11-10.

O que o .NET 11 traz de relevante para um motor console, stdio, publicado self-contained e single-file por RID (fonte: "What's new in .NET 11" e "Breaking changes in .NET 11", atualizados em novembro de 2026):

| Item | Ganho ou risco para o Spock |
|---|---|
| Runtime Async ligado por padrão | Ganho: menos custo e pilhas de chamada mais limpas no laço de requisições |
| API de processo ampliada (`StartSuspended`, `TryGetProcessById`, `Signal(PosixSignal)`, `ProcessExitStatus`) | Ganho real: o motor controla Python, `tofu` e o terminal. Pode simplificar o encerramento de processo filho no Linux, que hoje usa `kill -TERM` |
| `Environment.GetCommandLineArgs()[0]` passa a manter o nome do apphost | Risco: o lançador `spock` repassa a linha de comando. Precisa de teste |
| Requisito mínimo de CPU maior (x86/x64 e Arm64) | Risco para usuário de desktop com máquina antiga: o instalador deixaria de abrir |
| `.runtimeconfig.dev.json` sobrepõe o `.runtimeconfig.json` | Baixo risco: só afeta desenvolvimento |
| ZIP e TAR validam CRC e checksum | Ganho: o motor baixa pacotes |
| C# 15 (uniões, hierarquias fechadas, argumentos em expressão de coleção) | Uniões e hierarquias fechadas cabem nos resultados do protocolo, mas não justificam a migração sozinhas |

**Recomendação:** manter `net10.0`, que é LTS. Fixar o SDK com `global.json` (`10.0.x` com `rollForward: latestFeature`) para o build não depender da máquina. Abrir um ramo de avaliação do .NET 11 (MV-10) com quatro critérios: a suíte inteira, o smoke do instalador nas três plataformas, o lançador `spock` com o `argv[0]` novo e a conferência de requisito de CPU. O alvo de produção é o **.NET 12 LTS**, que pelo ciclo anual sai em novembro de 2027. A data ainda não está publicada (não verificado).

## 3. Microsoft Agent Framework

- **Maturidade:** GA. `Microsoft.Agents.AI` 1.0.0 saiu em 2026-04-02, e a versão atual é 1.28.0 (2026-11-06). Em Python, `agent-framework` 1.26.0 está marcado como Production/Stable e exige Python >=3.11. A documentação oficial o descreve como sucessor direto do Semantic Kernel e do AutoGen.
- **Compatibilidade com o Spock:** o pacote declara `net8.0`, `net9.0`, `net10.0`, `netstandard2.0` e `net472`, e entra no motor sem mudar o TFM. O provedor Foundry ainda é instalado como pré-lançamento no guia rápido oficial. O repositório no GitHub respondeu 404 no dia da consulta (não verificado).
- **Problema que ele resolveria:** nenhum que o Spock tenha hoje. A orquestração agentic do Spock está no LangGraph (Python), com checkpoint em SQLite, e o motor .NET já fala MCP pelo SDK oficial. Usar o Agent Framework no motor criaria um segundo orquestrador de agentes, o oposto de um único lugar para essa responsabilidade. Do lado Python, trocar o LangGraph seria uma reescrita sem defeito que a justifique.
- **Recomendação:** não adotar agora no motor. Reconhecer o pacote na descoberta de frameworks .NET e Python (MV-4), para que um projeto do usuário que use `Microsoft.Agents.AI` tenha a versão respeitada. Reavaliar só se o Spock precisar de orquestração de agentes dentro do motor .NET, por exemplo um agente que rode sem Python.

## 4. Demais componentes

- **React, Vite e MUI:** atuais na linha major. O TypeScript 7 e o ESLint 10 são as duas mudanças grandes pendentes e precisam ser avaliadas juntas com `typescript-eslint`, que hoje está na linha 8. A dúvida é se ele já declara suporte ao TypeScript 7 (não verificado).
- **Tauri:** só patches. CLI (npm), API (npm), crate e plugins precisam subir na mesma entrega. A linha 3.0 está em alpha e fica fora.
- **Python:** a linha 3.12 é suportada só em segurança. A subida para 3.13 ou 3.14 depende de `sqlite-vec`, que não declara versão de Python, e do CPython embarcado. Precisa de entrega própria com o smoke do instalador.
- **Ferramentas de qualidade:** ruff fixo no CI (bom). Não há Playwright, ferramenta de vulnerabilidade de dependência nem container.

## 5. Riscos de modernização

| Risco | Impacto | Mitigação |
|---|---|---|
| CPython embarcado sem patches de segurança | Alto: vai para a máquina de todo usuário | MV-8 com o smoke do instalador nas três plataformas |
| Pacote que perde o texto de licença numa versão nova | Bloqueia a conformidade | A conferência de licenças já pegou o caso do ProtectedData 10.0.15. Toda atualização roda `generate_sbom.py --check` |
| TypeScript 7 e ESLint 10 juntos | Médio: a ferramenta inteira da interface | Uma entrega por major, com typecheck, lint e testes |
| .NET 11 com requisito de CPU maior | Usuário com máquina antiga sem o app | Não migrar sem a decisão do dono (DD-MV-3) |
| CI sem orçamento | Nenhuma atualização é validada no servidor | Validação local obrigatória e registrada no PR até o orçamento voltar |

## 6. Lacunas de compatibilidade multiversão (antes do MV-1)

| Lacuna | Situação depois do MV-1 |
|---|---|
| O perfil juntava as versões de uma tecnologia sem dizer a pasta (`versions` e `roots` separados) | Resolvida: `modules` por pasta |
| Numa solução .NET, só o alvo mais novo era lido (`net10.0` escondia um projeto `net6.0`) | Resolvida: um alvo por projeto quando a solução mistura alvos, e o mais antigo de um multi-target |
| O Node de um projeto React não era lido (`.nvmrc`, `.node-version` e `engines` ignorados fora do Node puro) | Resolvida |
| `global.json` ignorado | Resolvida |
| Nenhuma noção de suporte ou fim de vida | Resolvida: catálogo e classificação |
| O agente não recebia restrição de versão | Resolvida no contexto (`bootstrap` e `architecture`) |
| Gradle Wrapper (`gradle-wrapper.properties`), `toolchain` do `go.mod`, `.tool-versions` (asdf/mise), `mise.toml`, Volta, `.sdkmanrc` | Aberta: MV-4 |
| PHP (`composer.json`), Ruby (`Gemfile`, `.ruby-version`), C/C++ (CMake), Kotlin/Android, Swift | Aberta: MV-4 |
| Compatibilidade entre tecnologias (Vite 8 exige Node >= 20.19, React 19 com TypeScript) | Aberta: MV-3 |
| Verificação depois da implementação (o diff usa API mais nova que o alvo?) | Aberta: MV-3 |

## 7. Lacunas nos templates e na criação de projetos

O Spock **não gera projetos hoje**. A jornada "Da ideia ao projeto" (`Ideas/`) produz documentos de arquitetura, não código. Os únicos templates de geração são os de IaC (`Iac/IacBootstrapTemplates.cs`), e o OpenTofu já é resolvido pela API oficial de releases. Não há versão de framework fixada em template para ficar velha. Também não há o caminho do pedido "crie um projeto .NET + React + Vite" até as versões escolhidas. Esse caminho é o MV-6, que depende da política de seleção (MV-5) e da compatibilidade entre tecnologias (MV-3).

## Fontes consultadas em 2026-11-18

- .NET: `https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json`; `https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/overview`; `https://learn.microsoft.com/dotnet/core/compatibility/11`
- Node.js: `https://raw.githubusercontent.com/nodejs/Release/main/schedule.json`; `https://nodejs.org/dist/index.json`
- Python: `https://peps.python.org/api/release-cycle.json`; `https://www.python.org/api/v2/downloads/release/`
- Go: `https://go.dev/dl/?mode=json`. Rust: `https://static.rust-lang.org/dist/channel-rust-stable.toml`. Java: `https://api.adoptium.net/v3/info/available_releases`
- npm: `https://registry.npmjs.org/<pacote>`. crates.io: `https://crates.io/api/v1/crates/<crate>`. NuGet: `https://api.nuget.org/v3-flatcontainer/<id>/index.json`. PyPI: `https://pypi.org/pypi/<pacote>/json`
- GitHub Actions: `https://api.github.com/repos/<ação>/releases/latest`
- Agent Framework: `https://learn.microsoft.com/agent-framework/overview/`
