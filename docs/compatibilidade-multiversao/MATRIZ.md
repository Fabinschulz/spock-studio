# Matriz de compatibilidade

A matriz diz o que o Spock lê de cada ecossistema, como classifica a versão e o que está comprovado por teste. Ela não declara suporte a "todas as versões". A classificação orienta o agente e nunca impede o trabalho num projeto legado.

## Classificação

| Situação (`status`) | Rótulo | Quando |
|---|---|---|
| `current` | Moderno | Linha estável mais nova, ou a LTS mais nova ativa |
| `supported` | Suportado | Linha ativa que não é a mais nova (ex.: .NET 9 antes do fim, Python 3.14, Go 1.26). Rust anterior à estável também cai aqui, com o motivo dito: a versão segue válida, mas sem correção |
| `maintenance` | Legado suportado | A fonte oficial diz manutenção ou só segurança (Node 22 e 24, Python 3.11 a 3.13) |
| `end-of-life` | Legado sem suporte | A fonte diz fim de vida, ou a data de fim já passou. Para o Go, linha mais antiga que as duas suportadas |
| `preview` | Pré-lançamento | Linha que ainda não é estável (Node 27, Python 3.16, Java 28) |
| `partial` | Parcialmente validado | A linha existe, mas o fim de suporte não é publicado em formato legível (React, Angular, Next.js, Vite e TypeScript fora da maior mais nova) ou depende do fornecedor (JDK LTS antigas) |
| `unknown` | Desconhecido | Sem versão nos arquivos, tecnologia fora do catálogo, versão que não vira linha (`net48`, `netstandard2.0`) ou mais nova que o catálogo |
| `incompatible` | Incompatível | Framework do catálogo que exige um Node que o Node fixado na pasta (ou acima) não atende, ou SDK do `global.json` mais antigo que um `TargetFramework` abaixo dele (MV-3). Vence a situação de suporte |

Toda classificação leva `reason` (o porquê, em uma frase), `confirmed` (a versão veio de arquivo que fixa ou de faixa declarada) e `evidence` (arquivo e valor lido).

## O que a descoberta lê

| Ecossistema | Arquivos lidos | Versão de onde | Por pasta | Teste |
|---|---|---|---|---|
| .NET | `.sln`/`.slnx`, `*.csproj` (`TargetFramework[s]`, SDK, `PackageReference`), `Directory.Build.props`, `Directory.Packages.props`, **`global.json`** | Alvo mais novo da solução (stack). **Alvo mais antigo de cada projeto** quando a solução mistura alvos (runtime). SDK do `global.json` | Raiz da solução, e cada projeto quando há mistura | `CompatibilityTests` (legado, monorepo, um alvo só), `DotnetFrameworkDiscoveryTests` |
| Node.js / React / Next.js / Angular | `package.json`, `node_modules/<pacote>/package.json`, lockfiles npm/pnpm/yarn/bun, **`.nvmrc`, `.node-version`**, `engines.node` | Versão instalada; sem ela, a faixa declarada (`confirmed=false`). Node pelo arquivo de versão mais próximo, ou o mínimo do `engines` | Cada `package.json` até profundidade 3 | `CompatibilityTests`, testes de descoberta existentes |
| Vite, TypeScript e demais frameworks JS | Dependências do `package.json` | Faixa declarada | Raiz do pacote | Classificados quando estão no catálogo (Vite, TypeScript) |
| Python | `pyproject.toml` (`requires-python`, `target-version` do ruff), `.python-version`, `setup.py`, `Pipfile` (`python_version`), `requirements*.txt/.in/.lock` | `.python-version`; sem ele, o que o manifesto declara (mínimo de `requires-python`) | Cada manifesto até profundidade 3 | `CompatibilityTests`, `LanguageDiscoveryTests` |
| Java | `pom.xml` (`maven.compiler.release`/`source`, `java.version`, `release`), `build.gradle[.kts]` (toolchain `JavaLanguageVersion.of`, `JavaVersion.VERSION_*`) | Propriedade declarada | Cada manifesto | `LanguageDiscoveryTests`, `EcosystemDiscoveryTests`. Kotlin e Android Gradle Plugin pelo bloco `plugins`; Gradle do wrapper (`gradle-wrapper.properties`) e Java do `.sdkmanrc` como runtime |
| Go | `go.mod` (diretiva `go` e `toolchain`) | Diretiva `go` (stack); `toolchain` como runtime | Cada `go.mod` | `LanguageDiscoveryTests`, `EcosystemDiscoveryTests` |
| Rust | `Cargo.toml` (`rust-version`), `rust-toolchain[.toml]` | Canal fixado; sem ele, o mínimo declarado | Cada `Cargo.toml` | `LanguageDiscoveryTests` |
| Flutter / Dart | `pubspec.yaml` | Restrição do SDK | Cada `pubspec.yaml` | Testes de descoberta existentes. Fora do catálogo: `unknown` |
| PHP | `composer.json` (`config.platform.php`, `require.php`, `require`/`require-dev`), `.php-version`, `composer.lock` | Plataforma do Composer; depois `.php-version`; depois o mínimo do `require.php` | Cada `composer.json` | `EcosystemDiscoveryTests`. Laravel, Symfony, Slim, Doctrine, PHPUnit, Pest |
| Ruby | `Gemfile` (`ruby`, `gem`), `Gemfile.lock` (gems e `RUBY VERSION`), `.ruby-version` | `.ruby-version`; depois o `ruby` do Gemfile; depois o lock | Cada `Gemfile` | `EcosystemDiscoveryTests`. Rails, Sinatra, Sidekiq, RSpec, Minitest com a versão do lock |
| C/C++ | `CMakeLists.txt` (`CMAKE_CXX_STANDARD`, `cxx_std_N`, `CMAKE_C_STANDARD`, `cmake_minimum_required`) | Padrão da linguagem (C++20, C11); CMake mínimo como nota | Projeto CMake de cima (subpastas são parte dele) | `EcosystemDiscoveryTests`. Makefile e Bazel não são lidos |
| Swift | `Package.swift` (`swift-tools-version`) | Versão das ferramentas do SwiftPM | Cada `Package.swift` | `EcosystemDiscoveryTests`. Projeto só Xcode não é lido |
| Gerenciadores de versão | `.tool-versions` (asdf), `mise.toml`/`.mise.toml`, `volta.node` do `package.json`, `.sdkmanrc` | Runtime fixado para a pasta e as de baixo; o mais próximo vence. Node: `.nvmrc`/`.node-version`, depois Volta, depois o gerenciador, por fim o mínimo do `engines` | Por stack | `EcosystemDiscoveryTests` |

## Catálogo de versões (consulta de 2026-11-18)

| Tecnologia | Fonte oficial | Política | Linhas que o motor reconhece |
|---|---|---|---|
| .NET | builds.dotnet.microsoft.com (`releases-index.json`) | Datas da fonte (LTS/STS, fase, fim) | 1.0 a 11.0. C# padrão por linha |
| Node.js | nodejs/Release `schedule.json` + `nodejs.org/dist/index.json` | Datas da fonte | 4 a 27 (27 em pré-lançamento) |
| Python | `peps.python.org/api/release-cycle.json` | Datas da fonte (fim por mês) | 3.x |
| Go | `go.dev/dl/?mode=json` | Janela: só as duas mais novas | As listadas pela fonte |
| Rust | `static.rust-lang.org/dist/channel-rust-stable.toml` | Contínua: só a estável recebe correção | A estável atual |
| Java | Eclipse Adoptium (`api.adoptium.net`) | Fornecedor | As disponíveis. LTS antigas como `partial` |
| PHP | `www.php.net/releases/states.php` | Janela: suporte ativo, depois só segurança, depois fim | As listadas pela fonte |
| Ruby | `branches.yml` do repositório do site oficial (`ruby/www.ruby-lang.org`) | Datas da fonte (manutenção normal, legada, fim) | As listadas; branches antigas de três números viram `major.minor` |
| React, Angular, Next.js, Vite, TypeScript | `registry.npmjs.org` | Maior mais nova | Uma linha por versão maior |

O gerador marca a entrada como `stale` quando a fonte falha, devolve dado inválido ou faz a linha ativa mais nova regredir. Nesse caso mantém a consulta anterior. O motor marca o catálogo como desatualizado quando alguma fonte está `stale` ou quando ele tem mais de 90 dias, e diz isso ao agente.

## Projetos de referência (testes)

| Cenário | O que prova |
|---|---|
| Legado: .NET 6 + SDK 6.0.428 no `global.json` + React 17 + Node 18 no `.nvmrc` + TypeScript 4.9 + Python 3.10 | Cada versão sai do próprio arquivo, com evidência. Todas as de runtime viram "Legado sem suporte" |
| Monorepo .NET: `net6.0`, `net10.0`, `netstandard2.0;net8.0` + React com `engines.node >=26` | Uma restrição por projeto. O multi-target é limitado pelo alvo mais antigo. O mínimo do `engines` é dito como mínimo |
| Solução com um alvo só | Nenhuma linha repetida por projeto |
| `.nvmrc` com `lts/*` | Nenhuma versão inventada |
| Pacote de contexto de um projeto legado | O agente recebe a versão do projeto e nunca a versão mais nova (nem `11.0`, nem `26.13`) |
| Catálogo com fonte fora do ar ou com regressão | A entrada anterior é mantida e marcada |

Para projetos modernos, o teste usa `net10.0`/`net11.0`, React 19 e Node 26 (`Cada_versao_ganha_a_situacao_de_suporte`). Projetos novos gerados pelo Spock não existem ainda (MV-6), então não há matriz de geração.
