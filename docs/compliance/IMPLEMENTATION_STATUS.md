# Implementação das decisões de licenciamento

Data: 2026-11-16. Branch `feat/conformidade-licencas`, a partir do master `3a4bb78`. As fontes de verdade são
[LICENSE_DECISIONS.md](LICENSE_DECISIONS.md) e os demais relatórios desta pasta. Este documento mostra o que o código faz
hoje, com a evidência de cada ponto. Decisões que aguardam aprovação continuam pendentes: nada aqui as aprova.

SPOCK significa **System of Programming, Organization, Code and Knowledge**.

## 1. Matriz de decisões

| Decisão | Estado documentado | Estado no código (verificado) | Ação feita nesta etapa |
| --- | --- | --- | --- |
| D-01 OpenTofu padrão | Adotada | Já implementada: `IacRuntimeService.PreferredEngine = OpenTofu`, ranking OpenTofu antes de Terraform e proposta sem `engine` = OpenTofu. A execução usa a abstração `IIacToolchain`/`IIacRuntime` (`OfficialReleaseToolchain`, `CliIacRuntime`), sem acoplamento ao Terraform | Nenhuma alteração |
| D-02 Terraform gerenciado | **Aguarda parecer** | Antes: liberado em qualquer build | Preparada a trava por edição de build (seção 3). O padrão continua liberado, como está decidido; a trava vale só no build `commercial` |
| D-03 Download oficial sob demanda | Adotada | Já implementada (seção 2.2) | Nenhuma alteração |
| D-04 CPython embarcado | Adotada | Windows: embed oficial com `LICENSE.txt`. Linux: o PBS não trazia as licenças das bibliotecas nativas | O pacote Linux passa a levar as licenças das 16 bibliotecas ligadas ao `libpython`, e o teste do pacote confere a presença |
| D-05 Berkeley DB | Adotada em 2026-11-15: cumprir a Sleepycat | O pedido desta etapa fala em remover o `_dbm`, mas isso não era possível pela poda na 3.12 (seção 4) | **Superada (2026-11-20, MV-9)**: na 3.14 o `_dbm` é módulo separado e sai na poda; o teste do pacote falha se ele voltar |
| D-06 Formato Linux | **Adotada em 2026-11-16: só `.deb`** | Antes: AppImage e `.deb` no CI | CI com `bundles: deb` (seção 9); relatório em [LINUX_DISTRIBUTION_REPORT.md](LINUX_DISTRIBUTION_REPORT.md) |
| D-07 Avisos no produto | **Adotada em 2026-11-16** | Antes: os avisos não iam ao instalador | Avisos em `resources/notices/` no instalador, comando do host, tela Sobre e conferência no smoke e no `--check` (seção 9) |
| D-08 CLIs de IA | Adotada; confirmação pendente | Conferido (seção 2.4) | Nenhuma alteração de código; as pendências da Anthropic e da Cursor estão registradas |
| D-09 EULA | **Aguarda redação e aprovação** | O instalador não exibe EULA | Nenhuma |
| D-10 Política de licenças | **Aguarda aprovação** | Não havia verificação automática | Preparada: `license-policy.json` (proposta), `--policy` em modo relatório e job `compliance` no CI |
| D-11 Validador JSON Schema | **Aguarda aprovação** | `JsonSchema.Net` 9.5.0 só nos testes | Só o relatório [JSON_SCHEMA_VALIDATOR_REPORT.md](JSON_SCHEMA_VALIDATOR_REPORT.md); a dependência não mudou |
| D-12 Marca SPOCK | **Aguarda parecer** | — | Só o escopo da avaliação em [TRADEMARK_ASSESSMENT.md](TRADEMARK_ASSESSMENT.md); nome e identidade visual não mudaram |

## 2. Decisões adotadas: verificação no código

### 2.1 D-01: OpenTofu padrão

- **Padrão:** `PreferredEngine = IacEngine.OpenTofu` e `Rank` ordena OpenTofu antes de Terraform
  (`runtime/Mariliz.Application/Iac/IacRuntimeService.cs`). `OfferAsync(engine: null)` propõe o OpenTofu.
- **Abstração:** a aplicação só conhece `IIacToolchain` (distribuição) e `IIacRuntime`/`IIacRuntimeFactory` (execução).
  `IacEngine` é um enum: um terceiro mecanismo exige um novo `IIacToolchain`, sem mexer nas operações.
- **Evidência:** `IacRuntimeTests`, `OpenTofuRuntimeTests` e `IacOperationTests` passaram (seção 6).

### 2.2 D-03: download oficial sob demanda

| Requisito | Onde | Situação |
| --- | --- | --- |
| Origem oficial | `HashiCorpTerraformToolchain` (só `releases.hashicorp.com`, sem redirecionamento) e `OpenTofuToolchain` (`get.opentofu.org` + releases do GitHub; redirecionamento só para a CDN do GitHub) | OK |
| Integridade | `OfficialReleaseToolchain.InstallAsync`: assinatura OpenPGP da lista de checksums com chave embutida, SHA-256 do pacote e extração só do binário; hash do binário conferido antes de cada uso (`Intact`, `ResolveForUseAsync`) | OK |
| Versão e plataforma | `MinimumFor`, `required_version` do projeto (`IacConfigReader`), `IacPlatform.Detect` | OK |
| Autorização | `InstallAsync` só aceita o `offerId` da proposta mostrada (hash de versão, plataforma, endereços e chave) | OK |
| Diretório gerenciado | `<dados>/tools/<ferramenta>/<versão>/`, fora do PATH | OK |
| Atualização e remoção | Versão nova só por proposta; projetos só trocam ao fixar; `RemoveAsync` exige confirmação e recusa versão em uso | OK |
| Sem gerenciador externo | Não usa winget, apt, brew nem tfenv | OK |
| Sem binário de IaC no instalador | `tauri.conf.json` embarca só `binaries/mariliz-runtime`, `resources/intelligence/` e `resources/python/`; os scripts de build não baixam `tofu` nem `terraform` | OK |

- **Teste real:** `IacRuntimeLiveTests` baixa a release oficial e confere a assinatura (seção 6).
- **Teste de ponta a ponta:** o executável do motor, chamado pelo protocolo, devolveu uma proposta real de
  `releases.hashicorp.com` no build padrão.

### 2.3 D-04: CPython embarcado

- **Atualização de 2026-11-19 (MV-8a, `docs/compatibilidade-multiversao`):** as três plataformas passam para o
  python-build-standalone 20261110 (CPython 3.12.16), com SHA-256 fixado. O python.org não publica o embeddable do Windows
  para releases só de segurança. No Windows, o `LICENSE.txt` do PBS cobre o CPython e o bzip2. Os textos de OpenSSL, SQLite,
  libffi, zlib, xz, expat e mpdecimal ficam em `scripts/notices/python-build-standalone-windows/` e vão para
  `python/licenses/python-build-standalone/`. Os textos do Linux foram trocados pelos da tag 20261110: xz agora é 0BSD, o
  zlib 1.4 trouxe novo copyright, o zstd entrou e o Tix saiu.
- **Atualização de 2026-11-20 (MV-9):** CPython 3.14.10 do mesmo PBS 20261110. O executável do Linux e do macOS passa a
  trazer o interpretador inteiro, e `_dbm` e `_tkinter` viram módulos separados: a poda tira os dois e o `libpython`, e o
  pacote Linux deixa de levar o Berkeley DB, o Tcl/Tk e o X11. No Windows entram o zstd (`_zstd`) e o zlib-ng no lugar do
  zlib; o libmpdec sobe para 4.0.0.
- **Windows (até o MV-8a):** CPython 3.12.10 embed oficial. O `LICENSE.txt` vai em `python/` e inclui as condições do código MSVC.
- **Linux:** python-build-standalone 20250409 até o MV-8a, com SHA-256 fixado.
  - Conferi o arquivo oficial: o `lib-dynload` só tem `_crypt`. Os módulos `_ssl`, `_sqlite3`, `_curses`, `readline`
    (libedit), `_tkinter` (Tcl/Tk/X11), `_lzma`, `_uuid` e `_dbm` (Berkeley DB) estão ligados estaticamente ao
    `libpython3.12.so.1.0`.
  - **Nesta etapa:** `scripts/notices/python-build-standalone/` passa a guardar os 16 textos de licença que o PBS publica na
    tag 20250409, copiados sem alteração, com um README de procedência. `scripts/package-intelligence.py` copia a pasta para
    `python/licenses/python-build-standalone/`.
  - O teste `Pacote_embarcado` confere esses arquivos no Linux e a licença do CPython em todas as plataformas.
- **Runtime:** sem alteração.

### 2.4 D-08: CLIs de IA

| Verificação | Evidência | Resultado |
| --- | --- | --- |
| Não distribui binários de terceiros | `tauri.conf.json` e scripts de build | Nenhuma CLI de IA no instalador |
| Não modifica executáveis | `AgentRunService.Start`: localiza o binário (`ExecutableLocator`: PATH e pastas usuais) e o executa | Só leitura e execução |
| Modo somente leitura | `AiToolCatalog`: Claude Code em modo de plano, sem Edit/Write/Bash; Codex com sandbox `read-only`; Cursor com sandbox; Gemini sem `--yolo` | OK |
| Não coleta nem armazena credenciais de terceiros | `ProcessSpec` sem `Environment` (o processo herda o ambiente do usuário; o Spock não injeta chaves). Nenhum acesso a `~/.claude`, `~/.codex`, `~/.cursor` ou `~/.gemini` (busca no motor). As leituras de `.claude/` são da pasta do projeto, para o Harness | OK |

- **Pendência Anthropic:** os termos permitem binário sem modificação, login do próprio usuário e nenhuma intermediação.
  Falta registrar a conferência formal pelo jurídico e revisar materiais de marketing quanto ao uso do nome "Claude Code".
- **Pendência Cursor:** os termos de serviço não tratam da CLI nem de integração por terceiros. Confirmação com a Cursor
  pendente.
- O código não declara compatibilidade comercial nem autorização de nenhum fornecedor.

## 3. D-02: Terraform gerenciado por edição de build (preparado)

A situação jurídica do Terraform não muda: D-02 continua pendente. O que existe agora é uma trava que pode ser ligada no build
comercial.

- **Configuração:** `-p:SpockEdition=commercial` no `dotnet publish` (`scripts/build-runtime.ps1 -Edition commercial`) grava
  `AssemblyMetadata("SpockEdition")` no `Mariliz.Runtime`. Sem a propriedade, ou com `development`, tudo continua liberado,
  como está decidido hoje. Um valor desconhecido derruba o motor na partida, por erro de build explícito.
- **Política** (`runtime/Mariliz.Application/Iac/IacDistributionPolicy.cs`), na edição `commercial`:

  | Ferramenta | Instalação gerenciada | Uso de instalação do usuário |
  | --- | --- | --- |
  | OpenTofu | Permitida | Permitido |
  | Terraform | **Recusada** | Permitido, sob a licença da HashiCorp, com aviso |

- **Onde a trava vale:** na camada de aplicação (`IacRuntimeService`), não na tela.
  - `BuildOfferAsync` recusa proposta e instalação com `IAC_MANAGED_INSTALL_DISABLED` antes de consultar versões. Isso vale
    para a interface, para o protocolo e para qualquer outro chamador.
  - `Usable`/`Permitted` descartam instalações gerenciadas de Terraform que tenham sobrado de outra edição. Assim,
    `ResolveForUseAsync`, a única porta de `validate`, `plan`, `apply`, `state` e das ferramentas MCP dos agentes, não as
    executa.
  - `AdoptAsync` respeita `ExternalUse` (`IAC_EXTERNAL_USE_DISABLED`).
  - A remoção continua permitida.
- **Auditoria:** cada recusa grava `iac.runtime.install_blocked` ou `iac.runtime.adopt_blocked` com a edição. O status
  (`iac.runtime.status`) informa `edition` e, por ferramenta, `managedInstall`, `externalUse` e `note`.
- **Agentes não contornam:**
  - As ferramentas MCP de IaC são só `iac_status`, `iac_validate`, `iac_plan_summary`, `iac_history`, `iac_blueprint_check`
    e `iac_state`, lista fixada por teste. Nenhuma instala, adota ou executa comandos livres, e `iac_validate` passa por
    `ResolveForUseAsync`.
  - As CLIs de IA rodam em modo somente leitura (seção 2.4).
- **Interface:** esconde "Prefiro o Terraform da HashiCorp" e "Procurar versão mais nova" quando o motor diz que a edição
  não permite, e mostra o motivo. A recusa real é do motor.

## 4. D-05: por que a remoção do `_dbm` não foi implementada

O pedido desta etapa manda preparar a remoção do `_dbm` e só podar depois de confirmar que nada depende dele. A verificação foi
feita.

1. Imports e referências a `dbm` ou `shelve`: nenhum em `intelligence/` nem nos 41 pacotes de `intelligence/packages`.
2. Dependências transitivas: nenhum pacote do lock usa `dbm`.
3. Arquivos `libdb*` no pacote: **nenhum**. O arquivo oficial do PBS (SHA-256 `8c59b9ac…e790ee`) não tem `_dbm*.so` nem
   `libdb*`. `PyInit__dbm` e "Berkeley DB 6.0.19" estão dentro do `lib/libpython3.12.so.1.0`. O `main` do PBS (2026-11)
   continua incluindo o `bdb`.

Não há arquivo a podar: o Berkeley DB faz parte do interpretador. Remover exigiria recompilar o CPython, opção que o dono
descartou em 2026-11-15. Por isso, nenhum teste de "ausência do `_dbm`" foi criado; ele falharia com qualquer interpretador do
PBS. Fica a decisão adotada: a licença e as fontes vão em `python/licenses/berkeley-db/`, e o teste do pacote as confere.

## 5. D-07 e D-10: automação preparada

**Avisos (D-07).** `python docs/compliance/tools/generate_sbom.py --bundle <pasta> [--strict]`:

1. Identifica os componentes distribuídos.
2. Copia os textos originais de cada pacote, agrupados por texto idêntico, mais os arquivos longos (.NET, CPython, NSIS) e os
   textos mantidos em `scripts/notices/`.
3. Lista as lacunas no `notices-manifest.json`.
4. Grava `THIRD_PARTY_NOTICES.txt` e a pasta `licenses/`, prontos para virar recurso do Tauri.

Com `--strict`, sai com erro se houver lacuna. Eram 45: 8 pacotes sem texto, 34 NuGet só com expressão SPDX e 3 itens fora
de pacote. Depois da seção 8 resta **1**: as bibliotecas embutidas no AppImage, que dependem da D-06. Por isso, a geração ainda
**não é completa**.

Faltam, todos condicionados à aprovação da D-07:

- ligar a pasta em `bundle.resources` do `tauri.conf.json`;
- criar a tela "Sobre", que lê `THIRD_PARTY_NOTICES.txt`;
- incluir no smoke do instalador a conferência do arquivo.

**Política (D-10).** `docs/compliance/license-policy.json` traz a proposta da D-10, marcada como não aprovada.

- **Escopos:** distribuído, sob demanda, externo e desenvolvimento. Cada um tem listas de licenças permitidas, a revisar e
  proibidas.
- **Componentes registrados:** os itens já analisados no registro de riscos ficam como "revisão registrada".
- **Execução:** `generate_sbom.py --policy` avalia dependências diretas e transitivas e informa para cada problema o pacote, a
  versão, o ecossistema, o escopo, a licença, o motivo e a **origem** (o caminho a partir da dependência direta). Os status
  são OK, REVISÃO REGISTRADA, REVISÃO PENDENTE, PROIBIDA e DESCONHECIDA.
- **Falha:** `--enforce` sai com erro diante de PROIBIDA, DESCONHECIDA ou REVISÃO PENDENTE em distribuídos e sob demanda.
- **CI:** o job `compliance` roda os testes do gerador, o `--check` (inventário em dia com os lockfiles; os binários nativos
  por plataforma, que são só de build, ficam fora da comparação) e o `--policy` em modo relatório. O `--enforce` só deve
  entrar depois da aprovação da D-10.

Resultado atual: 0 bloqueantes. Revisões registradas: 13 distribuídos, 3 sob demanda e 3 de desenvolvimento (os pacotes
com EULA de taxa). Também aparecem 3 "desconhecidas" de desenvolvimento: GitHub Actions, `xunit.abstractions` e
`httpx2-jsfetch` (este não é instalado).

## 6. Testes executados (Windows 11, 2026-11-16)

| Teste | Resultado |
| --- | --- |
| `dotnet test` IaC (`IacRuntimeTests`, `OpenTofuRuntimeTests`, `IacOperationTests`, `IacStateTests`), com 5 testes novos da política | 87/87 aprovados |
| Suíte completa do motor (`dotnet test runtime/Mariliz.sln`) | 1453 + 5 aprovados, 21 ignorados (exigem pacote real ou rede). Na primeira rodada, `TypographyTests` acusou travessão literal em `SOURCES.md` (corrigido) e em `IacGenerationTests.cs`, que veio do master pelo PR #105 (agora escapado como `—`). `WorkflowRuntimeTests` falhou duas vezes sob carga e passou isolado: oscila e não tem relação com esta mudança |
| Smoke `Pacote_embarcado` com os recursos do build local do Windows (o CPython embed e a inteligência sobem, e a licença do CPython está presente) | Aprovado |
| Executável `Mariliz.Runtime` compilado com `SpockEdition=commercial`, pelo protocolo | `iac.runtime.status` com `edition: commercial` e Terraform `managedInstall: false`; `iac.runtime.offer` Terraform recusado com `IAC_MANAGED_INSTALL_DISABLED` |
| O mesmo executável sem a propriedade | Proposta real do Terraform 1.17.2 de `releases.hashicorp.com` |
| Montagem do Python do Linux (`fetch_python`) com o arquivo oficial | `licenses/berkeley-db/` e `licenses/python-build-standalone/` (17 arquivos) presentes. O orçamento de tamanho estoura só no Windows, que transforma o link simbólico do `libpython` em cópia de 39 MB; no CI Linux o link é removido |
| UI: `vitest` de infraestrutura e contratos, mais `tsc`, `eslint` e `prettier` | 96/96 aprovados, sem erros |
| `python -m unittest` dos testes do gerador (política e detecção) | 11/11 aprovados |
| `generate_sbom.py --check` | Inventário em dia com os lockfiles |
| `generate_sbom.py --policy` | 0 bloqueantes |
| `generate_sbom.py --bundle --strict` | Falha esperada: 45 lacunas (1 depois da seção 8) |
| SBOMs revalidados contra os schemas oficiais | 6/6 válidos |

**Não executado:**

- **Linux:** o smoke do pacote, o build do AppImage e do `.deb` e o job `compliance` dependem do CI. O GitHub Actions do
  repositório está sem orçamento, então o resultado do CI não serve de evidência até isso ser resolvido.
- **Build dos instaladores:** não foi gerado nesta etapa (só o motor foi publicado). Nenhum instalador foi publicado.

## 7. Pendências e riscos remanescentes

- **Técnicas:**
  - inventariar o AppImage gerado (D-06), a última lacuna de aviso;
  - rodar o CI Linux quando houver orçamento;
  - o build de release precisa usar `-Edition commercial` (o CI atual gera builds de teste, sem a propriedade).
- **Jurídicas:**
  - D-02: alcance de "embedded" e "competitive offering" na BUSL;
  - Sleepycat (alcance);
  - LGPL do AppImage, se ele for mantido;
  - redação do EULA (D-09);
  - termos da Anthropic e da Cursor;
  - busca de anterioridade da marca SPOCK (D-12).
- **Dependem do proprietário:** D-02, D-06, D-07, D-09, D-10, D-11 e D-12.

## 8. Atualização de 2026-11-16: merge do master e lacunas de aviso

- **Merge do master:** o PR conflitava com o master (#107 e #108 trouxeram `AWSSDK.Signin` e `AWSSDK.Pricing` e deixaram o
  gerador determinístico).
  - Os arquivos gerados foram regenerados depois do `dotnet restore`; o `--check` confere.
  - Em `IacGenerationTests.cs` ficou a correção equivalente do master (`(char)0x2014`).
- **Lacunas de aviso, de 45 para 1:**
  - `tools/fetch_upstream_licenses.py` baixa, dos repositórios oficiais, o texto de licença dos componentes cujo pacote não
    traz o arquivo. Cada texto é fixado num commit: o que o nuspec declara (Azure SDK, dotnet/dotnet, MSAL, IdentityModel,
    YamlDotNet) ou o commit da branch padrão no dia (aws-sdk-net com `License.txt` e `Notice.txt`, alloc-no-stdlib, dlopen2,
    libappindicator-rs, webview2-rs, mariliz-ui-kit, sqlite-vec, nsis-tauri-utils).
  - O `selectors` (servo/stylo) não tem arquivo de licença no repositório; os fontes trazem o cabeçalho da MPL 2.0. Para ele
    vai o texto oficial da Mozilla.
  - Tudo fica em `notices/upstream/`, com `sources.json` (URL, commit e SHA-256). Os textos não foram editados.
- **.NET linux-x64:** baixei o pacote oficial `Microsoft.NETCore.App.Runtime.linux-x64` 10.0.13 do nuget.org. Seus
  `LICENSE.TXT` e `THIRD-PARTY-NOTICES.TXT` são idênticos aos do win-x64 (só muda o fim de linha). Os arquivos de `notices/`
  cobrem as duas plataformas.
- **Lacuna restante:** bibliotecas embutidas no AppImage (D-06).
- **Testes depois do merge:**
  - `dotnet test runtime/Mariliz.sln`: 1462 + 5 aprovados, 25 ignorados.
  - Gerador: 11/11; `--check` em dia; `--policy` com 0 bloqueantes; os 6 SBOMs válidos.
  - UI: 836/837. A falha é `decisions-section.test.tsx` ("oferece as execuções recentes..."), por tempo-limite de 5 s. O
    teste é idêntico ao do master, passou 1 de 3 vezes isolado e não tem relação com este PR.

## 9. D-06 e D-07 implementadas (2026-11-16, decisões do dono)

- **D-06 (só `.deb`):** o job `desktop` gera `bundles: deb` no Linux, e o `APPIMAGE_EXTRACT_AND_RUN` saiu. O smoke do
  instalador Linux já usava o `.deb`. O WebKitGTK passou a "externo, dependência do sistema" no inventário, e o R-03 caiu para
  BAIXO.
- **D-07 (avisos no produto):**
  - `generate_sbom.py` grava `desktop/host/src-tauri/resources/notices/`, que fica versionado. Nele vão
    `THIRD_PARTY_NOTICES.txt`, `licenses/` (45 arquivos) e `notices-manifest.json` (484 componentes, `complete: true`, lista
    de componentes).
  - O resultado não depende da máquina: os textos longos vêm das cópias versionadas em `docs/compliance/notices/`.
  - O `--check` falha se um componente distribuído entrar ou sair sem regenerar os avisos. Pacotes NuGet só de build (como o
    `Microsoft.NET.ILLink.Tasks`, que aparece num restore com RID) deixaram de contar como distribuídos.
  - `tauri.conf.json` mapeia `resources/notices/` para `notices/`. O comando do host `third_party_notices` está liberado em
    `build.rs` e em `capabilities/default.json`.
  - A tela **Configurações → Sobre** mostra o significado de SPOCK, a versão, a contagem e os textos.
- **Evidência:**
  - O instalador NSIS comercial (`Spock Studio_0.1.0_x64-setup.exe`, 46,9 MB, build local) traz no `installer.nsi`
    `notices\THIRD_PARTY_NOTICES.txt`, o manifesto e os 45 arquivos de `notices\licenses\`.
  - O smoke `Pacote_embarcado`, rodado contra `target/release` (mesma estrutura da instalação), passou.
  - No executável de release, aberto com dados isolados e dirigido pelo CDP, a tela Sobre mostrou "484 componentes de
    terceiros" e os textos (762 mil caracteres). `third_party_notices` respondeu `complete=true`.
  - Esse teste real pegou uma falha que os testes unitários não pegavam: o comando era recusado pela ACL do Tauri até entrar
    em `build.rs` e nas capacidades.
- **Testes:**
  - `dotnet test runtime/Mariliz.sln`: 1466 + 5 aprovados, 26 ignorados.
  - Host: `cargo test` 46/46; `clippy` e `fmt` limpos.
  - UI: 843/843; `tsc`, `eslint` e `prettier` limpos.
  - Gerador: 11/11; `--check` em dia; `--policy` com 0 bloqueantes; os 6 SBOMs válidos.
- **Não verificado:** a instalação real do NSIS (o smoke executa o setup e criaria atalhos e registro de desinstalação nesta
  máquina) e o `.deb`, que dependem do CI.

