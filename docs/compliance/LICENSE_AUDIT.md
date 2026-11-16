# Auditoria de licenciamento, dependências e SBOM do Spock Studio

| | |
| --- | --- |
| Data | 2026-11-15 |
| Escopo | Repositório inteiro no commit `2bdba24` (master; dependências e empacotamento idênticos em `8c61780`), mais o último build local (instalador Windows, recursos do Python e sidecar) |
| Produto | Spock Studio 0.1.0, comercializado como software proprietário |
| Natureza | Auditoria técnica de conformidade. **Não é parecer jurídico.** As questões marcadas ⚖ precisam de um profissional de propriedade intelectual e licenciamento de software |
| Resultado | **Sem conformidade total.** Não há bloqueador de copyleft forte no produto distribuído. Há 5 riscos ALTO (2 no produto distribuído ou baixado por ele, 3 em dependências de teste com taxa), 13 MÉDIO e 3 PENDENTES, e as obrigações de aviso e EULA ainda não estão implementadas nos instaladores |

Documentos desta auditoria:

- [DEPENDENCY_INVENTORY.md](DEPENDENCY_INVENTORY.md): inventário consolidado. A tabela completa fica em [data/inventory.csv](data/inventory.csv).
- [LICENSE_RISK_REGISTER.md](LICENSE_RISK_REGISTER.md): riscos classificados, com evidência e ação.
- [DISTRIBUTION_AUDIT.md](DISTRIBUTION_AUDIT.md): o que o usuário recebe e o que o Spock pode fazer com cada componente.
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md): rascunho dos avisos, gerado a partir dos textos originais.
- [LICENSE_DECISIONS.md](LICENSE_DECISIONS.md): decisões tomadas e decisões que aguardam aprovação.
- [SBOM/](SBOM/): SPDX 2.3 e CycloneDX 1.6, por instalador e consolidado.

## 1. Resumo executivo

| Indicador | Valor |
| --- | --- |
| Componentes identificados | **955** (npm 445, Cargo 377, NuGet 66, PyPI 43 e 24 fora de gerenciador: binários, sistema, ferramentas, serviços, ativos) |
| Dependências diretas / transitivas | 102 / 853. Em gerenciadores: npm 37/408, Cargo 9/368, NuGet 29/37, PyPI 3/40. Os 24 itens fora de gerenciador contam como diretos |
| Distribuídos ao usuário final | **483**. Windows x64: 404. Linux x64: 462. A maior parte vai nos dois |
| Baixados sob demanda | 5: OpenTofu, Terraform, provider `hashicorp/aws`, `@tech-leads-club/agent-skills` e o bootstrapper do WebView2 |
| Externos (instalação ou conta do usuário) | 8: Git, Node/npx, Claude Code, Codex CLI, Cursor Agent, Gemini CLI, APIs de modelo e APIs de nuvem/integrações |
| Só desenvolvimento ou build | 460 (inclui 104 crates de build/proc-macro). 1 pacote do lock não é instalado (`httpx2-jsfetch`, só emscripten) |
| Licenças efetivas dos distribuídos | MIT 397, Apache-2.0 27, BSD-3-Clause 15, Unicode-3.0 15, MPL-2.0 5 (+1 em `orjson`), ISC 3, BSD-2-Clause 3, Python/PSF 3, Zlib, 0BSD, MIT-0, Sleepycat 1, .NET Library License 1, Visual C++ Redistributable 1, NSIS (Zlib+bzip2+CPL) 1 |
| Risco (todos / distribuídos) | CRÍTICO 0/0 · ALTO 5/1 · MÉDIO 13/11 · PENDENTE 3/1 · BAIXO 934/470 (atualizado em 2026-11-16) |
| Licença conferida no arquivo do pacote (distribuídos) | 426 conferem com o declarado; 34 NuGet só declaram a expressão SPDX; 12 conferidos manualmente; 8 sem arquivo de licença no pacote; 1 texto não reconhecido; 1 divergência (`crossbeam-channel` traz também BSD-3-Clause, de código derivado do Go) |
| Sem licença confirmada | Nenhum componente distribuído está sem licença declarada. 9 não trazem o texto no pacote (lista em THIRD_PARTY_NOTICES.md, "Lacunas") |
| SBOM | 6 arquivos, todos **válidos** contra os schemas oficiais SPDX 2.3 e CycloneDX 1.6, sem referência quebrada |

**Situação de Terraform e OpenTofu.** O OpenTofu (MPL-2.0) é o padrão. O Spock não o redistribui: baixa da release oficial
na máquina do usuário, com consentimento, e confere a assinatura. Risco BAIXO. O Terraform (BUSL 1.1, licenciante IBM desde
2025) é baixado e operado pelo Spock quando o usuário o escolhe. Isso fica no alcance da definição de "embedded" da licença
se o Spock for considerado uma oferta paga competitiva. Risco **ALTO** e pendência jurídica (seção 5).

**Situação de providers e módulos.** O Spock não embarca providers nem módulos. O template de layout referencia
`hashicorp/aws` 6.66.0 (MPL-2.0), que o próprio `tofu`/`terraform init` baixa no projeto do usuário. Cada ferramenta tem
seu cache. Risco BAIXO. Os termos do Terraform Registry (maio de 2025) limitam o download ao uso com o HashiCorp
Terraform, e o Spock respeita isso: o OpenTofu usa o registro do OpenTofu.

**O que precisa de ação antes do lançamento** (detalhes no plano de ação, seção 9):

1. Avisos de terceiros dentro dos instaladores. Hoje não vão: o bundle da interface não tem nenhum comentário `@license`, e o
   sidecar single-file não leva os avisos do .NET.
2. Contrato de licença de usuário final (EULA) do Spock, com as cláusulas que .NET Library License, Visual C++ e CPython para
   Windows exigem. O instalador NSIS hoje não mostra licença nenhuma.
3. Pacote Linux: cumprir a LGPL das bibliotecas GTK/WebKitGTK
   embutidas no AppImage, ou distribuir só o `.deb`.
4. Decisão jurídica sobre o Terraform gerenciado pelo Spock (BUSL 1.1).
5. Busca de anterioridade da marca "Spock" (não é licença de software, mas afeta a comercialização).
6. Dependências de teste `JsonSchema.Net`, `Json.More.Net` e `JsonPointer.Net` (NuGet): o binário vem com um EULA "Open
   Source Maintenance Fee", que cobra taxa mensal de quem as usa em atividade com receita anual ≥ US$10.000. Não vão ao
   usuário, mas o desenvolvimento do Spock comercial se enquadra.

## 2. Critérios

- **Fontes.** A licença declarada vem do gerenciador de pacotes: `pnpm licenses`, `cargo metadata`, nuspec do NuGet e
  `METADATA` do PyPI. Ela é conferida contra o texto dos arquivos `LICENSE*`/`COPYING*`/`NOTICE*` que vêm **dentro do
  pacote baixado**. A conferência reconhece MIT, Apache-2.0, BSD-2/3, ISC, 0BSD, MPL-2.0, GPL/LGPL/AGPL, BUSL, Unicode, Zlib,
  PSF, Unlicense, CC0, BSL-1.0, BlueOak e CC-BY-4.0. As ferramentas centrais (Terraform, OpenTofu, provider AWS, NSIS,
  WebView2, python-build-standalone, CPython, .NET, agent-skills, CLIs de IA) foram conferidas nas fontes oficiais da seção 10.
- **Conferência.** CONFERE: o texto bate com a expressão declarada. DIVERGE: o texto traz licença fora da expressão; foi
  registrado, sem escolher um lado. SEM ARQUIVO: o pacote não traz o texto. SÓ EXPRESSÃO: NuGet que declara a licença só por
  expressão SPDX. CONFERIDO MANUALMENTE: componentes fora de gerenciador.
- **Distribuído.** O componente chega à máquina do usuário dentro do instalador. Isso vale para npm de produção (bundle Vite),
  crates alcançáveis por dependências normais sem passar por proc-macro (link estático no host), NuGet do grafo do
  `Mariliz.Runtime` (sidecar single-file), pacotes Python instalados pelo `package-intelligence.py bundle` e binários copiados
  para `resources/`. Para crates, o critério é conservador: o linker pode descartar código não usado.
- **Expressão "A OR B".** As obrigações seguem o ramo mais simples: MIT antes de Apache, Apache antes de MPL. "A AND B" soma
  as obrigações.
- **Níveis de risco.** Seguem a definição do pedido de auditoria: CRÍTICO, ALTO, MÉDIO, BAIXO e PENDENTE. O tipo (jurídico,
  distribuição, técnico, ausência de evidência) é indicado no registro de riscos.

## 3. Arquitetura real do Spock

| Parte | Tecnologia | Empacotamento | Evidência |
| --- | --- | --- | --- |
| Host desktop | Rust 2021 + Tauri 2.12 (plugins dialog, shell, single-instance; `portable-pty`) | Executável nativo, crates com link estático; WebView2 no Windows, WebKitGTK no Linux | `desktop/host/src-tauri/Cargo.toml`, `tauri.conf.json` |
| Interface | React 19 + MUI 9 + `@mariliz/ui-kit` 0.6.0 (próprio, MIT) + xterm.js + zod | Bundle Vite em `desktop/ui/dist`, embutido no executável do host | `desktop/ui/package.json` |
| Motor (sidecar) | .NET 10 (`Mariliz.Runtime` e camadas), YamlDotNet, ML.Tokenizers, AWS SDK, Azure SDK, Google APIs | `dotnet publish` self-contained, single-file, como `externalBin` do Tauri | `runtime/*.csproj`, `scripts/build-runtime.ps1:31` |
| Inteligência | Python 3.12 + LangGraph 1.2.19 (42 pacotes no lock com hash) | Bytecode sem fonte em `resources/intelligence/packages` | `intelligence/requirements.lock`, `scripts/package-intelligence.py` |
| Interpretador | CPython 3.12.10: embed oficial no Windows; python-build-standalone 20250409 no Linux e no macOS | `resources/python` | `scripts/build-runtime.ps1:66`, `scripts/package-intelligence.py:31` |
| Instaladores | Windows: NSIS (`currentUser`), sem assinatura. Linux: AppImage e `.deb`. macOS fora da 1ª versão | `tauri build` no CI | `.github/workflows/ci.yml:67-79` |
| IaC | OpenTofu (padrão) ou Terraform, baixados sob demanda com assinatura OpenPGP conferida e instalados em `<dados>/tools/<ferramenta>/<versão>`, sem PATH | Download em tempo de execução | `runtime/Mariliz.Infrastructure/Iac/*` |
| MCP | O motor é servidor MCP (código próprio). Lê o `.mcp.json` dos projetos do usuário, sem embarcar servidores de terceiros | — | `runtime/Mariliz.Application/Mcp/McpServer.cs`, `Engineering/McpInventory.cs` |
| Agentes de IA externos | Claude Code, Codex CLI, Cursor Agent, Gemini CLI, invocados como processo em modo somente leitura | Externos | `runtime/Mariliz.Application/AiTools/AiToolCatalog.cs` |
| Estrutura de especificação | `npx -y @tech-leads-club/agent-skills install -s tlc-spec-driven` no projeto do usuário | Sob demanda | `runtime/Mariliz.Application/Specification/SpecificationManifest.cs` |
| Provedores de modelo e nuvem | APIs HTTP com credenciais do usuário (cofre DPAPI/keychain) | Externos | `ModelProviders.cs`, `Mariliz.Infrastructure.Cloud` |
| CI | GitHub Actions (checkout, setup-dotnet/node/python, pnpm, rust-toolchain, rust-cache, upload-artifact), Ruff | Não distribuído | `.github/workflows/ci.yml` |

Sem Dockerfiles, sem imagens-base, sem atualização automática (não há `tauri-plugin-updater`) e sem diretórios `vendor/`
ou `third_party/`.

## 4. Auditoria por licença

As respostas abaixo resumem o texto de cada licença no contexto do Spock. Os casos limítrofes estão marcados ⚖.

| Licença | Comercial / proprietário | Redistribuir no produto | Exige fonte | Manter avisos | Publicar modificações | Restrição de concorrência | Pagamento | Obrigação do Spock |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| MIT, ISC, BSD-2/3, Zlib, Unicode-3.0, 0BSD, MIT-0 | Sim | Sim | Não | Sim (0BSD e MIT-0: não; Zlib em binário: recomendado) | Não | Não | Não | Texto e copyright nos avisos; BSD-3: não usar os nomes para endosso |
| Apache-2.0 | Sim | Sim | Não | Sim, mais o NOTICE | Não (marcar arquivos alterados) | Não | Não | Texto, NOTICE e licença de patente; marcar modificações |
| PSF-2.0 / Python-2.0 | Sim | Sim | Não | Sim | Resumo das mudanças em obra derivada | Não | Não | LICENSE.txt do CPython nos avisos |
| MPL-2.0 (5 crates do host, `certifi`, `orjson`; OpenTofu e provider AWS sob demanda) | Sim | Sim (§3.3, obra maior) | **Só dos arquivos MPL**, ao distribuir em forma executável (§3.2) | Sim | Sim, dos arquivos MPL alterados | Não | Não | Informar onde obter o fonte dos componentes MPL (link para a versão); não alterar os arquivos |
| LGPL-2.1+ (GTK/WebKitGTK no AppImage) | Sim, por link dinâmico | Sim, com condições | **Sim**: fonte correspondente ou oferta escrita | Sim | Sim, das bibliotecas alteradas | Não | Não | Textos LGPL, fonte ou oferta, bibliotecas substituíveis e nada no EULA que proíba engenharia reversa para depurar essas bibliotecas ⚖ |
| Sleepycat (Berkeley DB no Python do Linux) | ⚖ | ⚖ | **Sim**, do DB e de "software que use o DB" | Sim | Sim | Não | Não | Texto e onde obter o fonte do DB e do CPython (já no pacote, D-05); parecer sobre o alcance |
| BUSL-1.1 (Terraform ≥ 1.6) | Uso em produção sim; oferta competitiva paga, hospedada ou embarcada, **não** | Não se aplica: o Spock não redistribui, mas "embedded" inclui depender do download para operar ⚖ | Não | Sim | — | **Sim** | Licença comercial da IBM fora do Additional Use Grant | Seção 5 |
| CPL-1.0 (módulo LZMA do NSIS) | Sim | Sim (exceção expressa para código vinculado) | Só ao modificar o módulo | — | Só do módulo | Não | Não | Nenhuma além de não alterar o módulo |
| .NET Library License (partes do runtime win-x64) | Sim | Sim, **só dentro do app** | Não | Sim, não remover | — | Não | Não | EULA com termos protetivos, indenização, não usar marcas da Microsoft |
| Visual C++ Redistributable (no CPython embed) | Sim | Sim (via PSF) ⚖ | Não | Não alterar avisos | — | Não | Não | EULA protetivo; só plataformas Microsoft |
| CC-BY-4.0 (conteúdo da skill tlc-spec-driven) | Sim | Sim, com atribuição | Não | Atribuição: autor, licença, alterações | — | Não | Não | Só se o Spock passar a embarcar ou gerar esse conteúdo |
| Termos de serviço (Claude Code, Cursor, APIs de modelo, nuvem) | Conforme cada contrato | Não redistribuído | — | — | — | Varia | Do usuário | Seguir as regras de integração (seção 7) |
| GPL-2.0 (Git), LGPL-3.0 (rust-cache) | — | Não distribuídos | — | — | — | — | — | Nenhuma: processo externo e CI |
| EULA Open Source Maintenance Fee (binários NuGet do json-everything; fonte MIT) | Sim, com taxa mensal para usuário com receita ≥ US$10k | Permite redistribuir conforme o MIT | Não | Sim | Não | Não | **Sim**, para o binário (não para o fonte compilado pelo usuário) | Pagar, compilar do fonte ou substituir (só testes) |

**Por situação de uso.** O uso interno e no desenvolvimento (460 componentes de dev/build, incluindo `axe-core` MPL-2.0,
`caniuse-lite` CC-BY-4.0 e `argparse` Python-2.0) não gera obrigação de distribuição. O uso em produção só gera
obrigação quando há distribuição. A distribuição de binários e a inclusão em instaladores geram as obrigações acima. O
download automático sob demanda, feito da fonte oficial na máquina do usuário, não torna o Spock distribuidor do binário;
a exceção é a definição de "embedded" da BUSL. Alterar e redistribuir um componente não acontece hoje: nenhum componente de
terceiro é modificado. Oferecer como serviço não se aplica: o Spock é desktop e não hospeda ferramenta de terceiros para
outros.

## 5. Terraform e OpenTofu

### Fatos documentados

- O código já trata as duas ferramentas como **downloads sob demanda**, nunca embarcadas. Fontes: Terraform só de
  `releases.hashicorp.com`, sem seguir redirecionamento (`HashiCorpTerraformToolchain.cs:8-13`); OpenTofu de
  `get.opentofu.org` + releases do GitHub (`OpenTofuToolchain.cs:13-14`). A lista de checksums é conferida pela assinatura
  OpenPGP contra chaves embutidas, e só o binário é extraído: LICENSE e README do zip são descartados
  (`OfficialReleaseToolchain.cs:260-267`).
- Nada é instalado sem consentimento. A proposta mostra a licença: "MPL 2.0 (código aberto)" ou "BUSL 1.1 (HashiCorp)"
  (`IacRuntimeService.cs:269-271`). O padrão sem `engine` é o OpenTofu (`TERRAFORM_DEPENDENCY_MANAGEMENT.md` §1).
- Várias versões convivem lado a lado. É possível remover uma versão (`OfficialReleaseToolchain.Remove`). Não há
  atualização automática: versão nova é proposta e cada projeto só troca quando o usuário fixa.
- Instalações externas são detectadas (`FindExternal`) e têm preferência quando atendem ao projeto.
- O Spock opera a ferramenta: `fmt`, `validate`, `plan` e `apply`, com políticas, histórico e trilha de auditoria
  (`IacOperationService`). Também a expõe a agentes via MCP (`IacMcpTools`). O cache de providers é separado por ferramenta
  (`CliIacRuntime.cs:131-133`).
- **Terraform: licença BUSL-1.1** a partir da 1.6.0. O licenciante no LICENSE do repositório oficial é a IBM desde o fechamento
  da aquisição (2025-02-27); a página hashicorp.com/bsl ainda mostra a HashiCorp (conflito de fonte registrado). Change
  License: MPL 2.0, quatro anos após cada versão. Additional Use Grant: uso em produção permitido, **exceto** oferecer o
  Terraform a terceiros "on a hosted or embedded basis" em concorrência com as versões pagas da IBM. Oferta competitiva: um
  produto oferecido a terceiros de forma paga que "significantly overlaps" as capacidades pagas da IBM. Produto gratuito não é
  competitivo. "Embedded" inclui empacotar a oferta de modo que o Terraform "must be accessed or downloaded" para ela operar.
  Uso interno não é competitivo.
- **OpenTofu: licença MPL-2.0** (Linux Foundation). "OpenTofu" é marca registrada da LF Projects. Uso referencial ("compatível
  com OpenTofu") é permitido; "OpenTofu <produto>" e logo/domínio não são.
- **Registros.** registry.opentofu.org é um índice que aponta para releases no GitHub, sem restrição de qual ferramenta
  consome. Os termos de registry.terraform.io (atualizados em 2025-05-28) permitem download "solely for use with, or in support
  of, HashiCorp Terraform" e proíbem armazenar parte significativa do conteúdo.

### Cenários

| # | Cenário | Situação | Avaliação |
| --- | --- | --- | --- |
| 1 | Usuário já tem Terraform | O usuário obteve e aceitou a BUSL por conta própria | BAIXO para a obtenção. O uso pelo Spock cai no cenário 2 |
| 2 | Spock detecta e usa instalação existente | O Spock invoca o binário do usuário para plan/apply | ⚖ Se o Spock pago for oferta competitiva, depender do Terraform para operar pode ser "embedded" mesmo sem baixá-lo. Interpretação técnica: o risco é menor que no cenário 3, mas não é zero |
| 3 | Spock baixa automaticamente o Terraform | Download da fonte oficial, na máquina do usuário, com consentimento | **ALTO** ⚖. A definição de "embedded" cita "downloaded for the competitive offering to operate" |
| 4 | Spock instala e gerencia versões | Já implementado: pasta própria, várias versões, remoção | Mesma avaliação do cenário 3 |
| 5 | Spock distribui o Terraform no instalador | **Não acontece** e não deve acontecer | Seria redistribuição sob BUSL: CRÍTICO se o Spock for competitivo, e ALTO em qualquer caso, sem licença comercial |
| 6 | OpenTofu como padrão | Já implementado | BAIXO. O Spock não redistribui. Se um dia embarcar o binário: MPL §3.2, com link para o fonte da versão e a licença junto (hoje o LICENSE é descartado na extração; recomenda-se mantê-lo) |
| 7 | Usuário escolhe o mecanismo | Já implementado: "Prefiro o Terraform da HashiCorp" | A escolha explícita, com a licença mostrada, transfere ao usuário a aceitação da BUSL, mas **não** muda a natureza da oferta do Spock ⚖ |
| 8 | Providers e módulos externos | Baixados pelo `init` no projeto do usuário; o template só referencia `hashicorp/aws` 6.66.0 | BAIXO. Provider AWS sob MPL-2.0 (LICENSE em main, "Copyright IBM Corp. 2014, 2026"). Providers e SDKs seguem MPL. Caches separados mantêm o Terraform no registro dele e o OpenTofu no do OpenTofu |

### Interpretação técnica (não jurídica)

O Spock é uma plataforma de engenharia paga. A infraestrutura como código é uma função auxiliar, mas inclui `plan`/`apply`,
políticas, histórico de operações, trilha de auditoria e acesso por agentes. Essas capacidades se parecem com partes pagas do
HCP Terraform / Terraform Enterprise: execuções, políticas, histórico e governança. A pergunta "significantly overlaps" não se
responde tecnicamente. O argumento "quem baixa é o usuário" está **enfraquecido** pela redação de "embedded". O texto da
proposta no Spock ("Uso gratuito na sua própria infraestrutura; oferecer o Terraform como serviço a terceiros pede revisão da
licença") descreve a restrição de forma incompleta: deixa de fora o caso "embedded".

### Questões para o jurídico ⚖

1. O Spock pago, com as funções de IaC atuais, é "competitive offering" segundo a BUSL e o FAQ da HashiCorp? As respostas do
   FAQ (Q12, Q14 e Q15) não carregaram na consulta; o FAQ se declara vinculante.
2. Operar uma instalação do Terraform que o usuário já tem (cenário 2) está fora de "embedded"?
3. Vale pedir um esclarecimento por escrito à IBM/HashiCorp (o FAQ prevê contato para embedding) ou uma licença comercial?

### Estratégia recomendada

Manter o **OpenTofu como único motor que o Spock baixa e instala** em versões comerciais. Até o parecer, o Terraform fica
restrito a instalações que o usuário já tem (cenários 1 e 2), mostrando a BUSL. A decisão é do dono
([LICENSE_DECISIONS.md](LICENSE_DECISIONS.md), D-02). Isso não exige mudar a arquitetura: as duas ferramentas já são
adaptadores do mesmo `IIacToolchain`. Recomenda-se também manter o LICENSE do OpenTofu ao extrair o binário, além de ajustar o
texto da proposta do Terraform para citar o caso "embedded".

## 6. Redistribuição e instaladores

Resumo: o instalador do Windows leva o host (Rust + interface), o sidecar .NET single-file, o CPython embed (com OpenSSL,
libffi, SQLite, Visual C++ runtime) e a inteligência em bytecode. O Linux leva o mesmo, com python-build-standalone no lugar do
embed e, no AppImage, as bibliotecas GTK/WebKitGTK. A matriz completa, com o que pode ser distribuído, baixado, instalado,
atualizado, mantido em várias versões, removido ou modificado, está em [DISTRIBUTION_AUDIT.md](DISTRIBUTION_AUDIT.md).

Achados que valem para todos os componentes permissivos:

- O bundle da interface (`desktop/ui/dist/assets/*.js`) tem **zero** comentários `@license`. Os avisos de MIT/BSD/Apache não
  chegam ao usuário por esse caminho.
- O sidecar single-file não carrega `LICENSE.TXT` nem `THIRD-PARTY-NOTICES.TXT` do runtime .NET.
- O host Rust tem 200 crates linkados estaticamente no Windows e 261 no Linux, e nenhum aviso é gerado.
- O instalador NSIS não tem página de licença (`tauri.conf.json`, sem `license`/`licenseFile`).
- Os pacotes Python levam seus `dist-info` com os arquivos de licença (84 arquivos em 41 pacotes) e o CPython leva o
  `LICENSE.txt`. Esses avisos estão presentes, mas não ficam visíveis ao usuário.

## 7. Código, ativos e conteúdo de terceiros

| Item | Origem | Licença | Avaliação |
| --- | --- | --- | --- |
| Ícones e logotipo (`icons/source/spock-studio.svg`, PNGs, ICO/ICNS, `favicon.svg`) | Desenho próprio, declarado original no SVG | Proprietária | Copyright BAIXO. **Marca: PENDENTE** (ver abaixo) |
| Fontes tipográficas | Nenhuma embarcada; só pilhas de fontes do sistema (`_tokens.scss:81-83`) | — | Sem obrigação |
| Ícones da interface | `@mariliz/ui-kit` (próprio) e MUI | MIT | BAIXO |
| Agentes, skill e regras (`Mariliz.Application/Assets`) | Próprios; dois arquivos "Adapted from github.com/Fabinschulz/harness-engineering (MIT)" | MIT, mesmo titular | BAIXO; manter a atribuição |
| `catalog/components.json`, `policy.json`, `hooks/mariliz-policy.py`, schemas | Próprios | — | — |
| Chaves públicas OpenPGP da HashiCorp e do OpenTofu | Publicadas pelos titulares para verificação | Dado factual | BAIXO |
| Verificador OpenPGP (`OpenPgpSignature.cs`) | Implementação própria sobre `System.Security.Cryptography` (sem BouncyCastle) | — | — |
| Vocabulário o200k_base | Pacote `Microsoft.ML.Tokenizers.Data.O200kBase` | MIT | BAIXO |
| Template Terraform/OpenTofu (`TERRAFORM_PROJECT_LAYOUT.md`) | Próprio | — | Gera código no projeto do usuário; só referencia o provider |
| Referências a `tlc-spec-driven` em testes e fixtures | Só o nome e o layout esperado | — | Nenhum conteúdo da skill (CC-BY-4.0) está copiado no repositório |
| Prototype e docs de redesign | Não distribuídos | — | Sem CDN nem fonte externa encontrada |

Uma busca por "copyright", "SPDX-License-Identifier", "adapted from", "derived from", "ported from", "stackoverflow" e
"gist.github" no código-fonte próprio encontrou só as duas atribuições acima. Similaridade de código não foi analisada
(ferramenta indisponível; ver Limitações).

**Marca SPOCK.** SPOCK significa System of Programming, Organization, Code and Knowledge. A busca preliminar encontrou, no
USPTO, um registro nominativo "SPOCK" de terceiro só na classe 28 (brinquedos). Nas classes 9 e 42, os agregadores não mostram
registro vivo do mesmo titular. As buscas completas no USPTO e no INPI não puderam ser feitas (páginas dinâmicas). Esses
resultados não indicam conflito nem ausência dele. ⚖ Busca de anterioridade e parecer pendentes (D-12); o escopo está em
[TRADEMARK_ASSESSMENT.md](TRADEMARK_ASSESSMENT.md).

## 8. Limitações da auditoria

- **Sem ferramentas especializadas** (syft, cdxgen, ScanCode, cargo-about, license-checker): nenhuma estava instalada e não
  houve autorização para instalar. O inventário foi gerado por `tools/generate_sbom.py`, que usa só os gerenciadores já presentes
  e a biblioteca padrão do Python. A validação usou o JsonSchema.Net 9.5.0, que já era dependência dos testes.
- **Detecção de licença por padrões de texto.** É confiável para os textos-padrão. Licenças incomuns aparecem como "TEXTO NÃO
  RECONHECIDO" e não foram classificadas automaticamente. Não houve varredura de cabeçalhos arquivo a arquivo dentro dos pacotes.
- **AppImage não inspecionado.** Não houve build Linux local. A lista de bibliotecas que o `linuxdeploy --plugin gtk` embute
  vem do código do tauri-bundler, não de um artefato.
- **python-build-standalone.** As versões e licenças vêm do `targets.yml` e do `downloads.py` da tag 20250409, não do
  `PYTHON.json` do arquivo baixado. Depois, conferi no arquivo oficial que o Berkeley DB e as demais bibliotecas estão
  ligados ao `libpython3.12.so.1.0`; os textos de licença publicados pelo PBS na tag passaram a ir no pacote
  ([IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md)).
- **Versões dinâmicas.** OpenTofu, Terraform, agent-skills e WebView2 têm versão escolhida em tempo de execução. O SBOM
  registra a regra, não um hash.
- **Hashes.** Para npm, Cargo e NuGet, os hashes vêm dos lockfiles (integridade do pacote). Para o PyPI, o lock fixa vários
  hashes por pacote (um por roda) e nenhum foi atribuído ao componente. Para binários, os hashes são do **último build local**
  (instalador antigo, ainda com o nome "Mariliz Studio"), não de uma release.
- **Similaridade de código** com projetos externos não foi analisada.
- **O FAQ da HashiCorp** só carregou os títulos das perguntas.

## 9. Plano de ação

### P0: bloqueadores de comercialização

| # | Problema | Solução | Critério de conclusão |
| --- | --- | --- | --- |
| P0-1 | Terraform baixado e operado por produto pago pode ser "embedded" na BUSL 1.1 (R-01) | Parecer jurídico. Até lá, desativar nas versões comerciais a **instalação gerenciada** do Terraform (cenários 3 e 4), mantendo OpenTofu e Terraform pré-instalado. Ou obter licença comercial ou esclarecimento escrito da IBM | Parecer arquivado em LICENSE_DECISIONS (D-02) e build comercial coerente com ele |
| P0-2 | Instaladores sem EULA, exigido pela .NET Library License (win-x64), pelos termos do Visual C++ e pelo CPython Windows (R-05, R-06) | Redigir o EULA do Spock com as cláusulas exigidas e exibi-lo no NSIS (`bundle.windows.nsis.license`) e no Linux | EULA revisado pelo jurídico e exibido nos instaladores |
| P0-3 | Avisos de terceiros não chegam ao usuário (R-04) | Incluir `THIRD_PARTY_NOTICES` (versão final), `notices/` e os textos ainda faltantes como recurso do instalador, acessíveis por "Sobre". Gerar a partir do inventário no build | Instalador contém os avisos; smoke do CI verifica o arquivo |
| P0-4 | Berkeley DB (Sleepycat) no Python do Linux (R-02) | **Feito (D-05):** licença e fontes vão em `python/licenses/berkeley-db/`, conferidas pelo smoke. Restam o parecer, o canal de suporte e a cópia dos fontes nas releases (agora P1) | Smoke `Pacote_embarcado` verde no CI Linux |
| P0-5 | AppImage redistribui GTK/WebKitGTK (LGPL) (R-03) | Distribuir só o `.deb` na 1ª versão, ou cumprir a LGPL no AppImage: textos, fonte correspondente ou oferta escrita, e EULA sem proibição de engenharia reversa dessas bibliotecas | Decisão D-06 registrada e o artefato coerente com ela |

### P1: conformidade obrigatória

| # | Problema | Solução | Critério de conclusão |
| --- | --- | --- | --- |
| P1-1 | Componentes MPL-2.0 distribuídos (`cssparser`, `selectors`, `dtoa-short`, `option-ext`, `certifi`, `orjson`) (R-07) | Nos avisos, link para o fonte de cada versão e o texto da MPL. Não modificar esses arquivos | Avisos com os links |
| P1-2 | 9 componentes distribuídos sem texto de licença no pacote, e 34 NuGet só com expressão (R-08) | Obter o texto no repositório da versão (ou o texto canônico da licença SPDX para os NuGet) | **Feito em 2026-11-16:** textos oficiais fixados em commit em `notices/upstream/`; lacunas de 45 para 1 (AppImage, D-06) |
| P1-3 | `crossbeam-channel` traz também BSD-3-Clause (código derivado do Go) | Incluir o `LICENSE-THIRD-PARTY` dele nos avisos (o gerador já reproduz todos os arquivos de licença do pacote) | Conferido na versão final dos avisos |
| P1-4 | Avisos do python-build-standalone e do runtime .NET linux-x64 | **Feito:** licenças do PBS em `scripts/notices/python-build-standalone/`; .NET linux-x64 com os mesmos arquivos do win-x64 (conferido) | Arquivos presentes |
| P1-5 | Integração com Claude Code (R-10) | Confirmar e documentar: binário sem modificação, login do usuário, sem ler ou armazenar tokens; nenhum uso de "Claude Code" na marca | Checagem registrada em LICENSE_DECISIONS (D-08) |
| P1-6 | Marca "Spock" (R-11) | Busca de anterioridade (INPI e USPTO, classes 9 e 42) e avaliação do logotipo | Parecer de marca |
| P1-7 | Texto da proposta do Terraform incompleto | Citar o caso "embedded" (oferta paga competitiva) e o licenciante atual (IBM) | Texto revisado |
| P1-8 | `JsonSchema.Net` 9.5.0 e dependências (testes) sob o EULA Open Source Maintenance Fee (R-17) | Decidir (D-11): pagar a taxa, compilar do fonte MIT ou trocar por validador sem taxa | Decisão registrada e testes coerentes com ela |

### P2: melhorias preventivas

| # | Ação | Critério de conclusão |
| --- | --- | --- |
| P2-1 | Rodar `generate_sbom.py --check` no CI: falha se o lockfile mudou sem o inventário | Passo no `ci.yml` |
| P2-2 | Política de licenças no CI: lista de permitidas (permissivas), revisão (MPL, LGPL, CC-BY, termos) e proibidas (GPL/AGPL/SSPL/BUSL em distribuição). Usar `cargo-deny` e `pnpm licenses` com aprovação de instalação | Política em LICENSE_DECISIONS e verificação ativa |
| P2-3 | Gerar o SBOM a partir do artefato real no CI (instalador e AppImage) com uma ferramenta dedicada (syft ou cdxgen), quando a instalação for autorizada | SBOM anexado a cada release |
| P2-4 | Manter o LICENSE do OpenTofu ao lado do binário extraído | Arquivo presente em `<dados>/tools/opentofu/<versão>/` |
| P2-5 | Revisar termos de Cursor, APIs de modelo e WebView2 se o modo de instalação mudar | Registro em LICENSE_DECISIONS |

### P3: evolução

| # | Ação | Critério de conclusão |
| --- | --- | --- |
| P3-1 | Revisão de licenças a cada atualização de dependência relevante (checklist de PR) | Checklist no template de PR |
| P3-2 | Repetir esta auditoria antes de cada versão maior e quando entrar macOS (notarização e licenças do python-build-standalone para Darwin) | Auditoria datada em docs/compliance |
| P3-3 | Incluir os arquivos de licença no tarball do `@mariliz/ui-kit` (hoje só `dist`, `README` e `package.json`) | Versão publicada com LICENSE |

## 10. Fontes consultadas (2026-11-15)

- Terraform: https://raw.githubusercontent.com/hashicorp/terraform/main/LICENSE · https://www.hashicorp.com/bsl · https://www.hashicorp.com/en/license-faq (só títulos) · https://www.hashicorp.com/en/blog/hashicorp-updates-licensing-faq-based-on-community-questions
- Terraform Registry, termos de 2025-05-28: https://registry.terraform.io/terms
- Provider AWS: https://raw.githubusercontent.com/hashicorp/terraform-provider-aws/main/LICENSE
- OpenTofu: https://raw.githubusercontent.com/opentofu/opentofu/main/LICENSE · https://opentofu.org/faq/ · https://lfprojects.org/policies/trademark-policy/ · https://raw.githubusercontent.com/opentofu/registry/main/POLICY.md · https://lfprojects.org/policies/terms-of-use/
- MPL 2.0: https://www.mozilla.org/en-US/MPL/2.0/
- NSIS: https://nsis.sourceforge.io/License e o `COPYING` do NSIS baixado pelo tauri-bundler (`%LOCALAPPDATA%/tauri/NSIS/COPYING`, exceção do LZMA na linha 141)
- Tauri e WebView2: https://v2.tauri.app/distribute/windows-installer/ · https://v2.tauri.app/distribute/appimage/ · https://raw.githubusercontent.com/tauri-apps/tauri/dev/crates/tauri-bundler/src/bundle/linux/appimage/linuxdeploy.rs · https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution · https://developer.microsoft.com/microsoft-edge/api/eula/webview2
- python-build-standalone 20250409: https://gregoryszorc.com/docs/python-build-standalone/main/running.html · `pythonbuild/downloads.py`, `cpython-unix/targets.yml`, `cpython-unix/extension-modules.yml` na tag
- CPython: https://docs.python.org/3.12/license.html · https://raw.githubusercontent.com/python/cpython/v3.12.10/PC/layout/main.py · `resources/python/LICENSE.txt` (seção "Additional Conditions for this Windows binary build")
- Visual C++: https://learn.microsoft.com/en-us/cpp/windows/redistributing-visual-cpp-files
- .NET: https://raw.githubusercontent.com/dotnet/runtime/main/LICENSE.TXT · https://raw.githubusercontent.com/dotnet/core/main/license-information-windows.md · https://raw.githubusercontent.com/dotnet/runtime/main/docs/project/licensing-assets.md · https://dotnet.microsoft.com/dotnet_library_license.htm
- agent-skills: https://registry.npmjs.org/@tech-leads-club/agent-skills · https://raw.githubusercontent.com/tech-leads-club/agent-skills/main/LICENSE · SKILL.md do `tlc-spec-driven` (frontmatter `license: CC-BY-4.0`)
- Anthropic: https://code.claude.com/docs/en/agent-sdk/overview · https://code.claude.com/docs/en/legal-and-compliance
- Codex CLI: https://github.com/openai/codex · Gemini CLI: https://github.com/google-gemini/gemini-cli · Cursor: https://cursor.com/en-US/terms-of-service
- PyPI: https://pypi.org/pypi/certifi/json e equivalentes de sqlite-vec, httpx2, httpcore2, langchain-protocol, zstandard
- Marca: https://tsdr.uspto.gov/statusview/sn74736764

Conflitos entre fontes registrados (sem escolha silenciosa):

- Licenciante da BUSL: IBM no LICENSE do repositório, HashiCorp em hashicorp.com/bsl.
- `@tech-leads-club/agent-skills`: MIT no npm, CC-BY-4.0 no conteúdo da skill.
- `certifi`: o GitHub mostra NOASSERTION; o arquivo é cabeçalho próprio mais o texto da MPL-2.0.
- `sqlite-vec`: metadados PyPI com texto livre; o repositório tem LICENSE-MIT e LICENSE-APACHE.
- `distro` e `jsonpointer`: o metadado PyPI está em texto livre; o arquivo de licença dá Apache-2.0 e BSD-3-Clause.
- Runtime .NET win-x64: o código é MIT, mas a distribuição Windows é parcialmente .NET Library License.
- `crossbeam-channel`: declarado MIT OR Apache-2.0, e também traz arquivo BSD-3-Clause.
