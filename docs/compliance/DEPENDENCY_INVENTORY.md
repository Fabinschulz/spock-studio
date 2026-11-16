# Inventário de dependências e componentes de terceiros

Data: 2026-11-15 · commit `2bdba24`. Critérios: [LICENSE_AUDIT.md](LICENSE_AUDIT.md) §2.

O inventário completo tem **955 componentes**, cada um com os 14 campos pedidos: nome, versão, tipo, ecossistema, origem, uso,
dependência, ambiente, licença declarada, licença confirmada, distribuição, obrigações, risco, evidências e hashes. Ele é
gerado, não editado à mão:

| Arquivo | Conteúdo |
| --- | --- |
| [data/inventory.csv](data/inventory.csv) / [data/inventory.json](data/inventory.json) | Todos os componentes, todos os campos |
| [data/distributed-components.md](data/distributed-components.md) | Os 483 componentes que chegam ao usuário, por instalador |
| [data/summary.json](data/summary.json) | Totais por ecossistema, licença, risco e conferência |
| [SBOM/](SBOM/) | Os mesmos dados em SPDX 2.3 e CycloneDX 1.6, com hashes e grafo de dependências |

Para regenerar: `python docs/compliance/tools/generate_sbom.py`. Os pré-requisitos estão no cabeçalho do script.

**Licença confirmada.** Para componentes de gerenciador, é a licença reconhecida no texto do arquivo de licença que vem dentro
do pacote. Sem arquivo, o campo traz **DESCONHECIDA / PENDENTE DE VERIFICAÇÃO**. Para os componentes fora de gerenciador, a
conferência foi manual nas fontes oficiais (LICENSE_AUDIT.md §10).

## 1. Totais

| Ecossistema (como registrado no SBOM) | Componentes | Diretos | Distribuídos | Origem dos dados |
| --- | ---: | ---: | ---: | --- |
| npm (pnpm 9, lockfile v9) | 445 (inclui `@tech-leads-club/agent-skills`, sob demanda) | 37 | 106 | `pnpm-lock.yaml`, `pnpm licenses list`, `pnpm list` |
| Cargo (Rust 1.90+) | 377 | 9 | 276 (200 no Windows, 261 no Linux) | `Cargo.lock`, `cargo metadata --filter-platform` |
| NuGet (.NET 10) | 66 (inclui os 2 runtimes .NET) | 29 | 49 | `obj/project.assets.json`, nuspec no cache |
| PyPI (Python 3.12) | 43 (inclui o Ruff do CI) | 3 | 41 | `intelligence/requirements.lock` (com hash), `dist-info` |
| Binários | 9 (7 embarcados, mais OpenTofu e Terraform sob demanda) | 9 | 7 | `resources/python`, scripts de build, cache do tauri-bundler, `Iac/` |
| Sistema operacional | 2 (WebView2, WebKitGTK) | 2 | 1 (WebKitGTK no AppImage) | `tauri.conf.json`, `ci.yml` |
| Registro Terraform/OpenTofu | 1 (provider `hashicorp/aws`) | 1 | 0 | `TERRAFORM_PROJECT_LAYOUT.md` |
| Externos (CLIs) | 6 | 6 | 0 | `AiToolCatalog.cs`, `SystemProbe.cs`, `SystemProcessRunner.cs` |
| Serviços | 2 | 2 | 0 | `ModelProviders.cs`, conectores |
| Ativos | 3 | 3 | 3 | `icons/`, `Assets/`, `Iac/*.asc` |
| GitHub Actions | 1 | 1 | 0 | `.github/workflows/ci.yml` |
| **Total** | **955** | **102** | **483** | |

## 2. Dependências diretas de gerenciador

| Componente | Versão | Ecossistema | Ambiente | Uso no Spock | Licença declarada | Conferência | Risco |
| --- | --- | --- | --- | --- | --- | --- | --- |
| tauri | 2.12.5 | cargo | execução | Framework do host desktop (janela, IPC, WebView) | Apache-2.0 OR MIT | CONFERE | BAIXO |
| tauri-plugin-dialog | 2.8.3 | cargo | execução | Diálogos nativos | Apache-2.0 OR MIT | CONFERE | BAIXO |
| tauri-plugin-shell | 2.5.0 | cargo | execução | Processo do sidecar (motor .NET) | Apache-2.0 OR MIT | CONFERE | BAIXO |
| tauri-plugin-single-instance | 2.5.4 | cargo | execução | Instância única do app | Apache-2.0 OR MIT | CONFERE | BAIXO |
| portable-pty | 0.9.0 | cargo | execução | Pseudo-terminal do terminal embutido | MIT | CONFERE | BAIXO |
| serde | 1.0.229 | cargo | execução | Serialização | MIT OR Apache-2.0 | CONFERE | BAIXO |
| serde_json | 1.0.154 | cargo | execução | JSON | MIT OR Apache-2.0 | CONFERE | BAIXO |
| tokio | 1.54.1 | cargo | execução | Runtime assíncrono | MIT | CONFERE | BAIXO |
| tauri-build | 2.7.4 | cargo | build | Script de build do Tauri | Apache-2.0 OR MIT | CONFERE | BAIXO |
| react / react-dom | 19.3.1 | npm | execução | Interface | MIT | CONFERE | BAIXO |
| react-router | 8.5.0 | npm | execução | Rotas da interface | MIT | CONFERE | BAIXO |
| @mui/material | 9.5.0 | npm | execução | Componentes visuais (base do ui-kit) | MIT | CONFERE | BAIXO |
| @emotion/react / @emotion/styled | 11.14.0 / 11.14.1 | npm | execução | CSS-in-JS do MUI | MIT | CONFERE | BAIXO |
| @mariliz/ui-kit | 0.6.0 | npm | execução | Biblioteca de componentes própria (publicada no npm) | MIT | SEM ARQUIVO | BAIXO |
| @tauri-apps/api | 2.12.4 | npm | execução | Ponte da interface com o host | Apache-2.0 OR MIT | CONFERE | BAIXO |
| @xterm/xterm / @xterm/addon-fit | 6.0.0 / 0.11.0 | npm | execução | Terminal embutido | MIT | CONFERE | BAIXO |
| react-hook-form | 7.91.0 | npm | execução | Formulários | MIT | CONFERE | BAIXO |
| @hookform/resolvers | 7.0.0 | npm | execução | Validação de formulários com zod | MIT | CONFERE | BAIXO |
| zod | 4.6.7 | npm | execução | Validação de esquemas e contratos | MIT | CONFERE | BAIXO |
| clsx | 2.1.1 | npm | execução | Classes CSS condicionais | MIT | CONFERE | BAIXO |
| @tauri-apps/cli | 2.12.4 | npm | desenvolvimento | Build e empacotamento do instalador | Apache-2.0 OR MIT | CONFERE | BAIXO |
| vite / vitest | 8.3.5 / 5.0.5 | npm | desenvolvimento | Build e testes da interface | MIT | DIVERGE (o LICENSE.md lista licenças de dependências embutidas) | BAIXO |
| typescript | 6.0.3 | npm | desenvolvimento | Compilador | Apache-2.0 | CONFERE | BAIXO |
| eslint, @eslint/js, typescript-eslint, eslint-config-prettier, eslint-plugin-jsx-a11y, eslint-plugin-react-hooks, globals, prettier | várias | npm | desenvolvimento | Lint e formatação | MIT | CONFERE | BAIXO |
| @testing-library/*, jsdom, @vitejs/plugin-react, @types/* | várias | npm | desenvolvimento | Testes e tipos | MIT | CONFERE | BAIXO |
| axe-core | 4.14.0 | npm | desenvolvimento | Testes de acessibilidade | MPL-2.0 | DIVERGE (texto MPL com notas de terceiros) | BAIXO |
| sass-embedded | 1.106.1 | npm | desenvolvimento | SCSS (inclui binário Dart Sass) | MIT | CONFERE | BAIXO |
| YamlDotNet | 18.1.0 | nuget | execução | Leitura de mariliz.yaml e frontmatter | MIT | SÓ EXPRESSÃO | BAIXO |
| Microsoft.ML.Tokenizers (+ Data.O200kBase) | 3.0.0 | nuget | execução | Contagem de tokens; vocabulário o200k_base | MIT | CONFERE | BAIXO |
| System.Security.Cryptography.ProtectedData | 9.0.11 | nuget | execução | DPAPI: cofre de credenciais no Windows | MIT | CONFERE | BAIXO |
| AWSSDK.SecurityToken, SSO, SSOOIDC, EC2, S3, ECS, ECR, RDS, Lambda, CloudFormation, CloudWatch | 4.1.0–4.1.2 | nuget | execução | Conector AWS (identidade e leitura de recursos) | Apache-2.0 | SÓ EXPRESSÃO | BAIXO |
| Azure.Identity / Azure.ResourceManager | 1.21.0 / 1.14.0 | nuget | execução | Conector Azure | MIT | SÓ EXPRESSÃO | BAIXO |
| Google.Apis.Auth, CloudResourceManager.v3, Compute.v1, Storage.v1, ServiceUsage.v1 | 1.78.0.x | nuget | execução | Conector GCP | Apache-2.0 | CONFERE | BAIXO |
| xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, coverlet.collector | várias | nuget | desenvolvimento | Testes | Apache-2.0 / MIT | SÓ EXPRESSÃO | BAIXO |
| **JsonSchema.Net** | **9.5.0** | nuget | desenvolvimento | Validação de contratos nos testes | MIT (fonte) AND EULA Open Source Maintenance Fee (binário) | CONFERIDO no nuspec | **ALTO** (R-17) |
| langgraph | 1.2.19 | pypi | execução | Orquestração dos agentes e workflows | MIT | CONFERE | BAIXO |
| langgraph-checkpoint-sqlite | 3.1.3 | pypi | execução | Checkpoints dos grafos em SQLite | MIT | CONFERE | BAIXO |

Transitivas que merecem atenção, todas no inventário completo:

- **MPL-2.0 distribuídas:** `cssparser` 0.37.0, `selectors` 0.38.0 e `dtoa-short` 0.3.5 (via `tauri` → `tauri-utils` → `dom_query`),
  `option-ext` 0.2.0 (via `dirs`), `certifi` 2026.10.23, `orjson` 3.13.0.
- **Dependências do JsonSchema.Net** sob o mesmo EULA: `Json.More.Net` 3.0.1 e `JsonPointer.Net` 7.0.3.
- **Telemetria declarada:** `@mui/core-downloads-tracker` (MIT), um pacote vazio que o MUI usa para contar downloads no npm.
  Não roda nada no app.

## 3. Componentes fora de gerenciador de pacotes

| Componente | Versão | Tipo | Uso no Spock | Ambiente | Distribuição | Licença confirmada | Risco | Evidência |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CPython (Windows embeddable) | 3.12.10 | runtime | Interpretador da inteligência no Windows | execução | Redistribuído em `resources/python` | Python-2.0 (PSF), com as condições do binário Windows | MÉDIO | `scripts/build-runtime.ps1:66`, `resources/python/LICENSE.txt` |
| OpenSSL (no CPython) | 3.0.16 | biblioteca | TLS do Python | execução | Redistribuído | Apache-2.0 | BAIXO | `resources/python/libcrypto-3.dll` |
| Visual C++ Runtime | 14.x | runtime | Runtime C do CPython | execução | Redistribuído | Distributable Code da Microsoft | MÉDIO | `resources/python/vcruntime140*.dll` |
| python-build-standalone | 3.12.10+20250409 | runtime | Interpretador no Linux | execução | Redistribuído em `resources/python` | Python-2.0 + bibliotecas nativas | MÉDIO | `scripts/package-intelligence.py:31` |
| Berkeley DB (no PBS, `_dbm`) | 6.0.19 | biblioteca | Backend do `dbm`, não usado | execução | Redistribuído (Linux) | Sleepycat | ALTO | `downloads.py` da tag 20250409 |
| .NET Runtime win-x64 | 10.0.x (10.0.13 no cache) | runtime | Runtime do sidecar | execução | Incorporado (single-file) | MIT AND .NET Library License | MÉDIO | `scripts/build-runtime.ps1:31` |
| .NET Runtime linux-x64 | 10.0.x | runtime | Runtime do sidecar | execução | Incorporado (single-file) | MIT | MÉDIO | `ci.yml:117` |
| NSIS (stub e plugins) | 3.x | instalador | Instalador Windows | build | Incorporado no setup.exe | Zlib AND bzip2-1.0.6 AND CPL-1.0 (exceção do LZMA) | BAIXO | `%LOCALAPPDATA%/tauri/NSIS/COPYING` |
| nsis_tauri_utils | do tauri-bundler | plugin | Plugin do instalador | build | Incorporado no setup.exe | MIT OR Apache-2.0 | BAIXO | cache do tauri-bundler |
| WebView2 Runtime | do sistema | runtime | Motor web no Windows | sob demanda | Bootstrapper baixado da Microsoft se faltar | Termos Microsoft WebView2 | BAIXO | `tauri.conf.json` (modo padrão) |
| WebKitGTK 4.1 + GTK 3 | do sistema | runtime | Motor web no Linux | execução | Embutido no AppImage; dependência no `.deb` | LGPL-2.1-or-later (e outras) | ALTO | `ci.yml:79,107` |
| OpenTofu (`tofu`) | dinâmica (testes: 1.13.9) | CLI | Motor padrão de IaC | sob demanda | Baixado da release oficial, com consentimento | MPL-2.0 | BAIXO | `OpenTofuToolchain.cs` |
| Terraform | dinâmica ≥ 1.6 (testes: 1.17.2) | CLI | Motor alternativo de IaC | sob demanda | Baixado de releases.hashicorp.com, com consentimento | BUSL-1.1 | ALTO | `HashiCorpTerraformToolchain.cs` |
| Provider `hashicorp/aws` | 6.66.0 (template) | provider | IaC AWS no projeto do usuário | sob demanda | Baixado pelo `init` | MPL-2.0 | BAIXO | `TERRAFORM_PROJECT_LAYOUT.md:111` |
| `@tech-leads-club/agent-skills` | dinâmica (validadas 3.3.0 e 4.0.0) | CLI + conteúdo | Estrutura de especificação | sob demanda | `npx` no projeto do usuário | MIT (CLI) AND CC-BY-4.0 (skill) | MÉDIO | `SpecificationManifest.cs:13` |
| Git | do usuário | CLI | Status do repositório | externo | Externo | GPL-2.0 | BAIXO | `SystemProbe.cs:21` |
| Node.js / npx | do usuário | CLI | Executa o agent-skills | externo | Externo | MIT | BAIXO | `SystemProcessRunner.cs:84` |
| Claude Code CLI | do usuário | CLI | Agente em modo somente leitura | externo | Externo | Termos comerciais da Anthropic | MÉDIO | `AiToolCatalog.cs:16` |
| Codex CLI | do usuário | CLI | Agente em sandbox somente leitura | externo | Externo | Apache-2.0 | BAIXO | `AiToolCatalog.cs:22` |
| Cursor Agent CLI | do usuário | CLI | Agente em sandbox | externo | Externo | Termos da Cursor | PENDENTE | `AiToolCatalog.cs:19` |
| Gemini CLI | do usuário | CLI | Agente | externo | Externo | Apache-2.0 | BAIXO | `AiToolCatalog.cs:25` |
| APIs de modelo | — | serviço | OpenAI, Anthropic, Gemini com a chave do usuário | externo | Externo | Termos de cada provedor | PENDENTE | `ModelProviders.cs:12` |
| APIs de nuvem e integrações | — | serviço | AWS, Azure, GCP, GitHub, Jira | externo | Externo | Termos de cada serviço | BAIXO | `Mariliz.Infrastructure.Cloud`, `Integrations/Connectors` |
| Ícones e logotipo | 0.1.0 | ativo | Ícone do app e do instalador | execução | Incorporado | Próprio | PENDENTE (marca) | `icons/source/spock-studio.svg` |
| Chaves OpenPGP de releases | — | dado | Verificar assinaturas | execução | Incorporado | Dado factual | BAIXO | `Iac/*.asc` |
| Conteúdo adaptado de harness-engineering | — | dado | Agentes, skill e regras gerados | execução | Incorporado e copiado para projetos | MIT (mesmo titular) | BAIXO | `Assets/agents/code-reviewer.md:10` |
| GitHub Actions | v2–v5 | CI | Build e testes | build | Não distribuído | MIT / Apache-2.0; `swatinem/rust-cache` LGPL-3.0 | BAIXO | `ci.yml` |
| Ruff | 0.17.4 | CLI | Lint do Python no CI | desenvolvimento | Não distribuído | MIT | BAIXO | `ci.yml:36` |

## 4. Componentes sem licença confirmada

Todos os componentes distribuídos têm licença declarada. Os itens abaixo não trazem o texto no pacote, ou trazem um texto
que a conferência não reconheceu. A licença declarada é aceita, mas o texto a reproduzir é **PENDENTE**:

- `alloc-stdlib` 0.3.0 (BSD-3-Clause)
- `dlopen2` 0.8.2 (MIT)
- `libappindicator-sys` 0.9.0 (Apache-2.0 OR MIT)
- `selectors` 0.38.0 (MPL-2.0)
- `webview2-com` / `webview2-com-sys` 0.39.1 (MIT)
- `@mariliz/ui-kit` 0.6.0 (MIT, próprio)
- `sqlite-vec` 0.1.9 (MIT OR Apache-2.0, conforme o repositório; o metadado PyPI é texto livre)
- `hoist-non-react-statics` 3.3.2 (BSD-3-Clause; texto em formato não reconhecido)
- 34 pacotes NuGet de execução que só declaram a expressão SPDX (AWS SDK Apache-2.0; Azure e Microsoft MIT)

Em desenvolvimento: `xunit.abstractions` 2.0.3 (só `licenseUrl`) e `httpx2-jsfetch` 1.0 (não instalado, marcador emscripten).

Conflitos entre o declarado e o arquivo foram registrados, sem escolha silenciosa:

- `crossbeam-channel` 0.5.17: o arquivo traz também BSD-3-Clause.
- `distro` 1.9.0: metadado "Apache License, Version 2.0", arquivo Apache-2.0.
- `jsonpointer` 3.8: metadado "Modified BSD License", arquivo BSD-3-Clause.
- `sqlite-vec`: metadado "MIT License, Apache License, Version 2.0", repositório MIT e Apache-2.0.

Nos três últimos, a normalização para SPDX está registrada no gerador.
