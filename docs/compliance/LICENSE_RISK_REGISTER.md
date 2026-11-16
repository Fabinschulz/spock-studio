# Registro de riscos de licenciamento

Data: 2026-11-15 (atualizado em 2026-11-16). Escopo e critérios: [LICENSE_AUDIT.md](LICENSE_AUDIT.md). Os 934 componentes de risco BAIXO estão em
[data/inventory.csv](data/inventory.csv), coluna `risco`, cada um com motivo e obrigação. Este registro detalha o que não é
BAIXO e as obrigações coletivas que valem para todos os componentes permissivos.

Tipos: **J** jurídico · **D** distribuição · **T** técnico · **E** ausência de evidência. ⚖ indica que a decisão depende
de parecer.

| ID | Nível | Tipo | Componente | Prioridade |
| --- | --- | --- | --- | --- |
| R-01 | ALTO | J | Terraform (BUSL 1.1), instalação e operação gerenciadas | P0 |
| R-02 | MÉDIO | J | Berkeley DB 6.0.19 (Sleepycat) no Python do Linux (obrigação cumprida no pacote) | P1 |
| R-03 | ALTO | D | GTK/WebKitGTK (LGPL) embutidos no AppImage | P0 |
| R-04 | MÉDIO | D | Avisos de ~480 componentes permissivos não distribuídos | P0 |
| R-05 | MÉDIO | D/J | .NET Runtime win-x64 (.NET Library License) e linux-x64 (MIT) | P0 |
| R-06 | MÉDIO | D/J | CPython embed e Visual C++ Runtime (Distributable Code) | P0 |
| R-07 | MÉDIO | D | Componentes MPL-2.0 distribuídos (6) | P1 |
| R-08 | BAIXO (era MÉDIO) | E | Textos de licença obtidos nos repositórios oficiais | — |
| R-09 | BAIXO (era MÉDIO) | D | python-build-standalone: avisos das bibliotecas nativas (textos no pacote) | — |
| R-10 | MÉDIO | J | Integração com o Claude Code CLI | P1 |
| R-11 | PENDENTE | J | Marca "Spock" e logotipo | P1 |
| R-12 | MÉDIO | J | Conteúdo CC-BY-4.0 da skill `tlc-spec-driven` | P2 |
| R-13 | PENDENTE | E | Cursor Agent CLI: termos de integração | P2 |
| R-14 | PENDENTE | J | APIs de modelo: termos comerciais | P2 |
| R-15 | BAIXO | D | OpenTofu: LICENSE descartado na extração | P2 |
| R-16 | BAIXO | T | Divergência de licença em `crossbeam-channel` | P1 |
| R-17 | ALTO | J (custo) | `JsonSchema.Net` 9.5.0, `Json.More.Net` 3.0.1, `JsonPointer.Net` 7.0.3 (testes) | P1 |

---

### R-01: Terraform (BUSL 1.1): ALTO, jurídico ⚖

- **Evidência.** `HashiCorpTerraformToolchain.cs:8-13` (download de releases.hashicorp.com), `IacRuntimeService.cs:269-271`
  (proposta e licença), `IacOperationService` (plan/apply, políticas, histórico). Fontes: LICENSE do hashicorp/terraform
  (licenciante IBM), Additional Use Grant e definições de "Competitive offering" e "Embedded".
- **Motivo.** A licença proíbe oferecer o Terraform a terceiros "on a hosted or embedded basis" em produto pago que concorra
  com as ofertas pagas da IBM. "Embedded" inclui depender do download do Terraform para operar. O Spock é pago e oferece
  plan/apply com governança.
- **Impacto comercial.** Se o Spock for considerado competitivo, distribuir a função de Terraform gerenciado exige licença
  comercial. Sem ela, há risco de violação.
- **Ação.** Parecer jurídico. Até lá, instalação gerenciada só do OpenTofu nas versões comerciais e Terraform só se já
  instalado pelo usuário. Corrigir o texto da proposta.
- **Prioridade.** P0.

### R-02: Berkeley DB (Sleepycat) no python-build-standalone: MÉDIO (era ALTO), jurídico

- **Evidência.** `scripts/package-intelligence.py:31-45` (PBS 20250409). `downloads.py` e `targets.yml` da tag
  20250409: `_dbm` vincula ao Berkeley DB 6.0.19, "the last version licensed under the Sleepycat license". No arquivo
  oficial (SHA-256 fixado no script) não há `_dbm*.so`: `PyInit__dbm` e "Berkeley DB 6.0.19" estão **dentro de
  `lib/libpython3.12.so.1.0`**, ligados estaticamente. O `main` do PBS (2026-11) ainda inclui o `bdb` no alvo Linux.
- **Motivo.** A Sleepycat exige informar como obter o código-fonte completo do DB "and any accompanying software that uses the
  DB software". O alcance sobre o produto proprietário é incerto ⚖.
- **Impacto comercial.** Obrigação de fonte com alcance discutível, no pacote Linux.
- **Ação tomada (D-05, opção 1).** A poda não resolve, porque o DB faz parte do próprio interpretador. A distribuição passa a
  cumprir a Sleepycat:
  - `scripts/package-intelligence.py` copia `scripts/notices/berkeley-db/` para `python/licenses/berkeley-db/` no pacote Linux.
  - Vão o `LICENSE` original do Berkeley DB 6.0.19 (tirado do tarball com o SHA-256 fixado pelo PBS) e um `SOURCES.md` com os
    endereços e hashes do fonte do DB, do CPython 3.12.10, da receita do PBS e do interpretador redistribuído, mais a oferta de
    cópia pelo suporte.
  - O smoke `Pacote_embarcado` falha se esses arquivos faltarem, tanto no pacote quanto no instalador aberto.
- **Pendências.**
  1. Parecer sobre o alcance de "any accompanying software that uses the DB software". O código do Spock nunca chama `dbm`. O
     executável que contém o DB é o `libpython`, cujo fonte completo está indicado.
  2. Publicar o canal de suporte citado no `SOURCES.md`.
  3. Guardar uma cópia dos tarballs de fonte junto das releases.
- **Prioridade.** P1.

### R-03: GTK/WebKitGTK (LGPL) no AppImage: ALTO, distribuição

- **Evidência.** `.github/workflows/ci.yml:79` (`bundles: appimage,deb`). O código do tauri-bundler (`linuxdeploy.rs`) copia
  os processos do WebKit e roda `linuxdeploy --plugin gtk`.
- **Motivo.** No AppImage, o Spock redistribui bibliotecas LGPL-2.1+: WebKitGTK, GTK, GLib, libsoup e, com
  `bundleMediaFramework`, GStreamer. No `.deb` elas são dependências do sistema.
- **Impacto comercial.** A LGPL exige os textos, o código-fonte correspondente (ou oferta escrita) e permitir substituir e
  depurar as bibliotecas. O EULA não pode proibir isso.
- **Ação.** Decidir (D-06): só `.deb` na 1ª versão, ou o AppImage com o pacote de conformidade LGPL. Inspecionar o AppImage
  gerado para listar cada biblioteca e sua licença (não feito: falta build Linux).
- **Prioridade.** P0.

### R-04: Avisos de componentes permissivos não distribuídos: MÉDIO, distribuição

- **Evidência.**
  - `desktop/ui/dist/assets/*.js` tem 0 comentários `@license` (106 pacotes npm de produção).
  - O host Rust linka 200 crates estaticamente no Windows e 261 no Linux.
  - O sidecar tem 47 pacotes NuGet.
  - `tauri.conf.json` não tem licença nem recurso de avisos.
- **Motivo.** MIT, BSD, ISC, Apache e Unicode exigem copyright e texto da licença em cópias distribuídas. Apache exige também
  o NOTICE.
- **Impacto comercial.** Descumprimento generalizado, mas de baixo custo de correção.
- **Ação.** Embarcar a versão final de [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) e de `notices/`, acessível em "Sobre",
  gerada pelo `generate_sbom.py` no build.
- **Prioridade.** P0.

### R-05: .NET Runtime self-contained: MÉDIO, distribuição/jurídico

- **Evidência.** `scripts/build-runtime.ps1:31` e `ci.yml:117` (`--self-contained -p:PublishSingleFile=true`).
  `license-information-windows.md` (coreclr.dll e DiaSymReader sob a .NET Library License). `LICENSE.TXT` e
  `THIRD-PARTY-NOTICES.TXT` no pacote do runtime.
- **Motivo.** No Windows, a .NET Library License exige distribuição só dentro do app, termos de usuário final protetivos,
  indenização, não remover avisos e não usar marcas da Microsoft. No Linux vale o MIT. Nos dois casos o single-file não leva os
  avisos.
- **Ação.** Incluir os dois arquivos (já copiados em `notices/` para win-x64; falta o linux-x64) e as cláusulas no EULA.
- **Prioridade.** P0 (EULA) e P1 (avisos do Linux).

### R-06: CPython embed e Visual C++ Runtime: MÉDIO, distribuição/jurídico

- **Evidência.** `resources/python/LICENSE.txt:283-320` ("Additional Conditions for this Windows binary build").
  `vcruntime140.dll` e `vcruntime140_1.dll` estão em `resources/python`.
- **Motivo.** Redistribuir exige que distribuidores e usuários finais concordem com termos que protejam o Distributable Code da
  Microsoft, só em plataformas Microsoft, sem alterar avisos. A cadeia PSF → Spock é aceita pela própria PSF nesse texto ⚖.
- **Ação.** EULA com cláusula equivalente. Manter o `LICENSE.txt` e referenciá-lo nos avisos.
- **Prioridade.** P0 (junto com o EULA).

### R-07: MPL-2.0 distribuídos: MÉDIO, distribuição

- **Componentes.** `cssparser` 0.37.0, `selectors` 0.38.0 e `dtoa-short` 0.3.5 (servo, via `tauri` → `tauri-utils` → `dom_query`),
  `option-ext` 0.2.0 (via `dirs`), `certifi` 2026.10.23 (CA bundle do Python), `orjson` 3.13.0 (MPL-2.0 AND
  (Apache-2.0 OR MIT)).
- **Motivo.** MPL §3.2: ao distribuir em forma executável, informar onde obter o fonte dos arquivos cobertos e manter a
  licença. §3.3 permite a obra maior proprietária. Nenhum arquivo foi modificado.
- **Ação.** Nos avisos, para cada um, o texto da MPL e o link para o fonte da versão (crates.io, PyPI ou repositório na tag).
- **Prioridade.** P1.

### R-08: Texto de licença ausente no pacote: BAIXO (era MÉDIO), ausência de evidência

- **Componentes.** `alloc-stdlib` 0.3.0, `dlopen2` 0.8.2, `libappindicator-sys` 0.9.0, `selectors` 0.38.0, `webview2-com`
  0.39.1, `webview2-com-sys` 0.39.1, `@mariliz/ui-kit` 0.6.0 (próprio), `sqlite-vec` 0.1.9 e `hoist-non-react-statics` 3.3.2
  (texto não reconhecido). Também os 34 NuGet que só declaram a expressão (AWS SDK Apache-2.0, Microsoft MIT).
- **Motivo.** A licença declarada existe, mas o texto a reproduzir não vem no pacote.
- **Ação feita (2026-11-16).** `tools/fetch_upstream_licenses.py` baixou os textos dos repositórios oficiais, fixados em
  commit, para `notices/upstream/`, com procedência em `sources.json`. O `selectors` recebeu o texto oficial da MPL 2.0, porque
  o repositório não tem arquivo de licença. O `hoist-non-react-statics` traz o próprio arquivo, que já vai nos avisos.
- **Resta.** Nada para esses componentes.

### R-09: python-build-standalone: avisos nativos: BAIXO (era MÉDIO), distribuição

- **Evidência.** `targets.yml` (20250409, Linux gnu).
  - Bibliotecas: OpenSSL 3.0.16, SQLite 3.47.1, ncurses 6.5, libedit, libffi 3.4.6, expat 2.6.3, mpdecimal 4.0.0, zlib 1.3.1,
    xz 5.2.12, bzip2 1.0.8, uuid, libX11/libxcb e Tcl/Tk.
  - Todas estão ligadas estaticamente ao `libpython3.12.so.1.0`, conforme conferido no arquivo oficial. A poda do script
    remove só os scripts do Tcl/Tk, não o código.
  - O readline (GPL) não é usado (`USE_LIBEDIT`) e o `_gdbm` é desativado.
- **Ação feita (2026-11-16).** Os 16 textos que o PBS publica na tag 20250409 estão em
  `scripts/notices/python-build-standalone/` e vão em `python/licenses/python-build-standalone/`. O smoke do pacote confere.
- **Resta.** Exibir os avisos ao usuário (D-07).

### R-10: Claude Code CLI: MÉDIO, jurídico

- **Evidência.** `AiToolCatalog.cs:16-18` (invocação `claude -p --permission-mode plan ...`). Página "Legal and compliance"
  da Anthropic.
- **Motivo.** A Anthropic permite oferecer o Claude Code em produtos sob os Commercial Terms, desde que o binário não seja
  modificado, o usuário autentique com a própria conta e o fornecedor não pague, revenda ou intermedeie o uso nem colete
  credenciais ou tokens. O nome "Claude Code" não pode compor a marca do produto.
- **Ação.** Registrar em D-08 que o Spock só invoca o binário do usuário e nunca lê os tokens. Revisar textos de marketing.
- **Prioridade.** P1.

### R-11: Marca "Spock" e logotipo: PENDENTE, jurídico ⚖

- **Identidade.** SPOCK significa System of Programming, Organization, Code and Knowledge.
- **Evidência.** `tauri.conf.json` (`productName: Spock Studio`) e `icons/source/spock-studio.svg`. A busca preliminar
  encontrou o registro nominativo "SPOCK" nº 1990155 no USPTO, de terceiro, só na classe 28.
- **Motivo.** A anterioridade nas classes de software (9 e 42) ainda não foi verificada no INPI nem no USPTO.
- **Ação.** Busca de anterioridade e parecer, conforme o escopo de [TRADEMARK_ASSESSMENT.md](TRADEMARK_ASSESSMENT.md).
- **Prioridade.** P1.

### R-12: Skill `tlc-spec-driven` (CC-BY-4.0): MÉDIO, jurídico

- **Evidência.** `SpecificationManifest.cs:13`. README e SKILL.md do agent-skills.
- **Motivo.** O conteúdo instalado no projeto do usuário é CC-BY-4.0. O Spock não o redistribui: o `npx` baixa do npm. Se o
  Spock passar a embarcar, gerar ou modificar esse conteúdo, deve dar atribuição.
- **Ação.** Mostrar a atribuição na proposta de instalação (autor, licença) e não embarcar o conteúdo sem atribuição.
- **Prioridade.** P2.

### R-13: Cursor Agent CLI: PENDENTE, ausência de evidência

- Os termos de serviço da Cursor (2026-09-03) não tratam da CLI nem de integração por terceiros.
- O Spock só invoca o binário do usuário.
- **Ação.** Confirmar com a Cursor ou documentar a interpretação.
- **Prioridade.** P2.

### R-14: APIs de modelo: PENDENTE, jurídico

- O Spock chama OpenAI, Anthropic e Google com a chave do próprio usuário (`ModelProviders.cs`) e não revende acesso.
- **Ação.** Revisar os termos comerciais de cada provedor quanto a uso por aplicativo de terceiros e retenção de dados.
- **Prioridade.** P2.

### R-15: OpenTofu: LICENSE descartado: BAIXO

- `OfficialReleaseToolchain.cs:260-267` extrai só o binário.
- Não há violação: o usuário recebe o pacote oficial e o Spock não redistribui.
- **Ação.** Manter o LICENSE ao lado do binário (P2-4).

### R-16: `crossbeam-channel` 0.5.17: BAIXO, técnico

- O crate é declarado `MIT OR Apache-2.0`, mas traz `LICENSE-THIRD-PARTY` com BSD-3-Clause (código derivado do Go).
- **Ação.** Reproduzir esse arquivo nos avisos. O gerador já inclui todos os arquivos de licença do pacote.

### R-17: EULA Open Source Maintenance Fee nas dependências de teste: ALTO, jurídico/custo

- **Evidência.** `runtime/tests/*/Mariliz.*.Tests.csproj` (`JsonSchema.Net` 9.5.0). O nuspec declara
  `<license type="file">OSMFEULA.txt</license>`, igual em `Json.More.Net` 3.0.1 e `JsonPointer.Net` 7.0.3
  (`~/.nuget/packages/jsonschema.net/9.5.0/OSMFEULA.txt`).
- **Motivo.** O fonte é MIT, mas o binário publicado no NuGet vem com um EULA. Ele cobra taxa mensal de quem usa o software
  "as part of revenue-generating activities" e tem faturamento anual ≥ US$10.000. Compilar a partir do fonte dispensa o
  acordo. Não há distribuição ao usuário final: os pacotes só validam contratos nos testes.
- **Impacto comercial.** Custo recorrente não previsto, ou descumprimento do EULA, quando o Spock gerar receita.
- **Ação.** D-11: pagar a taxa, compilar do fonte MIT ou trocar por um validador de JSON Schema sem taxa. Até a decisão, não
  adotar novas bibliotecas do json-everything.
- **Prioridade.** P1. Não bloqueia a distribuição.

## Componentes avaliados como BAIXO que merecem nota

- **NSIS:** zlib/libpng. O módulo LZMA usa CPL-1.0, com exceção expressa para código vinculado.
- **WebView2:** modo padrão `downloadBootstrapper`. Nada é redistribuído pelo Spock. Mudar para embutir o bootstrapper ou o
  instalador offline traz os termos de distribuição da Microsoft, incluindo indenização e aviso de SmartScreen.
- **Provider `hashicorp/aws`:** MPL-2.0. Baixado pelo motor de IaC, com caches separados por registro.
- **Git (GPL-2.0), Codex CLI e Gemini CLI (Apache-2.0):** programas externos do usuário, sem obrigação para o Spock.
- **Desenvolvimento:** `axe-core` (MPL-2.0), `caniuse-lite` (CC-BY-4.0), `argparse` (Python-2.0) e `swatinem/rust-cache`
  (LGPL-3.0) não são distribuídos.
