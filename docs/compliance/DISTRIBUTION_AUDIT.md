# Auditoria de redistribuição e instaladores

Data: 2026-11-15. Ela responde a duas perguntas: o que o usuário recebe ao instalar o Spock, e o que o Spock pode fazer com cada
componente. Licenças e riscos estão em [LICENSE_AUDIT.md](LICENSE_AUDIT.md) e [LICENSE_RISK_REGISTER.md](LICENSE_RISK_REGISTER.md).

## 1. O que vai em cada instalador

Fonte: `desktop/host/src-tauri/tauri.conf.json` (`bundle`), `.github/workflows/ci.yml` (job `desktop`),
`scripts/build-runtime.ps1` e `scripts/package-intelligence.py`. A conferência foi feita no último build local
(`target/release/bundle/nsis`, `resources/`, `binaries/`).

### Windows x64: `Spock Studio_<versão>_x64-setup.exe` (NSIS, `installMode: currentUser`, sem assinatura)

| Parte do instalador | Conteúdo | Componentes de terceiros | Forma |
| --- | --- | --- | --- |
| Stub do setup | Instalador NSIS | NSIS 3.x (zlib/libpng, módulo LZMA CPL-1.0 com exceção), `nsis_tauri_utils` (MIT OR Apache-2.0) | Incorporado |
| `Spock Studio.exe` (host) | Rust + Tauri, com a interface embutida | 200 crates com link estático (MIT, Apache-2.0, BSD, Unicode-3.0, Zlib, 4 MPL-2.0); 106 pacotes npm no bundle Vite (MIT, BSD, ISC, Apache-2.0 OR MIT, 0BSD) | Incorporado |
| `mariliz-runtime.exe` (sidecar) | Motor .NET 10 self-contained, single-file | Runtime .NET win-x64 (MIT AND .NET Library License); 47 pacotes NuGet (26 MIT, 21 Apache-2.0) | Incorporado |
| `python/` | CPython 3.12.10 embeddable oficial | CPython (Python-2.0), OpenSSL 3.0.16, libffi, SQLite, bzip2, xz, expat 2.7.1, mpdecimal, zlib, `vcruntime140.dll`, `vcruntime140_1.dll` | Redistribuído sem modificação; o `python312._pth` é editado pelo build para habilitar `import site` |
| `intelligence/` | Código próprio + `packages/` | 41 pacotes PyPI em bytecode `.pyc` sem fonte, com os `dist-info` (e os arquivos de licença) preservados | Redistribuído; forma alterada para bytecode, conteúdo sem modificação |
| Ícones | Ícones do app | Próprios | — |
| `notices/` | Avisos de terceiros (D-07) | `THIRD_PARTY_NOTICES.txt`, `licenses/` e manifesto, gerados por `generate_sbom.py` | Mostrados em Configurações → Sobre |
| Não incluídos | — | WebView2 (o instalador baixa o bootstrapper da Microsoft se faltar), OpenTofu, Terraform, providers, agent-skills, CLIs de IA | — |

### Linux x64: `.deb` (D-06: AppImage fora da 1ª versão)

| Parte | Conteúdo | Componentes de terceiros | Forma |
| --- | --- | --- | --- |
| Host | Rust + Tauri + interface | 261 crates (os do Windows, menos os específicos dele, mais GTK/glib/webkit2gtk-rs, zbus, x11 etc.); os mesmos 106 pacotes npm | Incorporado |
| Sidecar | .NET 10 linux-x64, single-file | Runtime .NET linux-x64 (MIT); os mesmos 47 NuGet | Incorporado |
| `python/` | python-build-standalone 3.12.10+20250409, podado | CPython, OpenSSL 3.0.16, SQLite 3.47.1, ncurses 6.5, libedit, libffi 3.4.6, expat 2.6.3, mpdecimal 4.0.0, zlib 1.3.1, xz 5.2.12, bzip2, uuid, libX11/libxcb e **Berkeley DB 6.0.19 (`_dbm`, Sleepycat, ligado estaticamente ao `libpython3.12.so`)**. Tcl/Tk são podados | Redistribuído |
| `intelligence/` | Igual ao Windows (rodas manylinux com `strip`) | 41 pacotes PyPI | Redistribuído |
| Só no `.deb` | Dependências declaradas (`libwebkit2gtk-4.1-0`, `libgtk-3-0`) | Do sistema | Não redistribuído |

### Atualizado automaticamente

Nada. Não há `tauri-plugin-updater` nem atualização automática de componentes. Versões novas de OpenTofu ou Terraform são
propostas e só instaladas com consentimento. Cada projeto só troca de versão quando o usuário fixa
(`IacRuntimeService.Impacts`).

### Baixado no primeiro uso ou depois

| Componente | Quando | Fonte | Verificação | Onde fica |
| --- | --- | --- | --- | --- |
| WebView2 Runtime | Na instalação, se faltar | Microsoft (bootstrapper Evergreen) | Da Microsoft | Sistema |
| OpenTofu | Quando o usuário autoriza a proposta | `get.opentofu.org` + releases do GitHub | Assinatura OpenPGP da lista de checksums + SHA-256 | `<dados>/tools/opentofu/<versão>/` |
| Terraform | Quando o usuário escolhe e autoriza | `releases.hashicorp.com` (sem redirecionamento) | Assinatura OpenPGP + SHA-256 | `<dados>/tools/terraform/<versão>/` |
| Providers (ex.: `hashicorp/aws`) | No `init` do projeto | registry.opentofu.org ou registry.terraform.io, conforme a ferramenta | `.terraform.lock.hcl` do projeto | `<dados>/iac/plugin-cache/<ferramenta>/` |
| `@tech-leads-club/agent-skills` | Ao configurar a estrutura de especificação | npm, via `npx -y` | Arquivos, hash e scripts validados após a instalação | Projeto do usuário |

## 2. O que o Spock pode fazer com cada componente

Legenda: ✅ permitido · ⚠️ permitido com condições · ⚖ depende de parecer · ❌ não permitido ou não recomendado · — não se aplica.

| Componente | Distribuir no instalador | Disponibilizar para download | Instalar com autorização | Atualizar | Várias versões | Remover | Redistribuir modificado |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Crates, npm, NuGet e PyPI permissivos | ⚠️ com avisos e licenças | ⚠️ com avisos | — | ✅ | — | ✅ | ⚠️ com avisos; Apache: marcar alterações |
| Crates e pacotes MPL-2.0 | ⚠️ com avisos e link para o fonte | ⚠️ idem | — | ✅ | — | ✅ | ⚠️ arquivos MPL alterados ficam MPL e com fonte disponível |
| Runtime .NET win-x64 | ⚠️ só dentro do app, EULA protetivo, avisos | ❌ separado do app | — | ✅ | — | ✅ | ❌ |
| Runtime .NET linux-x64 | ⚠️ com avisos | ⚠️ com avisos | — | ✅ | — | ✅ | ⚠️ MIT |
| CPython embed (Windows) | ⚠️ LICENSE.txt e EULA protetivo para o código da Microsoft | ⚠️ idem | — | ✅ | — | ✅ | ⚠️ PSF: resumo das mudanças; o Distributable Code da Microsoft não pode ser alterado |
| Visual C++ Runtime | ⚠️ só junto com o app, só em Windows, EULA protetivo | ❌ separado | — | ✅ | — | ✅ | ❌ |
| python-build-standalone | ⚠️ com as licenças das bibliotecas nativas | ⚠️ idem | — | ✅ | — | ✅ | ⚠️ (a poda atual é aceitável) |
| Berkeley DB (`_dbm`, dentro do libpython) | ⚠️ Sleepycat: texto e onde obter o fonte (já em `python/licenses/berkeley-db/`); alcance ⚖ | ⚠️ idem | — | — | — | ❌ não sai pela poda (só recompilando o CPython) | ⚖ |
| GTK/WebKitGTK (AppImage) | ⚠️ LGPL: textos, fonte ou oferta, bibliotecas substituíveis | ⚠️ idem | — | ✅ | — | ✅ | ⚠️ alterações publicadas sob LGPL |
| NSIS | ✅ (stub no setup) | ✅ | — | ✅ | — | — | ⚠️ módulo LZMA alterado fica CPL |
| WebView2 Runtime | ⚠️ só embutindo o bootstrapper ou o instalador offline, sob os termos da Microsoft (hoje não embute) | ❌ não fornecer links da Microsoft a terceiros | ✅ bootstrapper oficial (padrão atual) | Pela Microsoft (Evergreen) | — | — | ❌ |
| OpenTofu | ⚠️ MPL: informar o fonte e manter a licença (hoje não embute) | ⚠️ idem | ✅ (implementado) | ✅ com consentimento (implementado) | ✅ (implementado) | ✅ (implementado) | ⚠️ arquivos alterados ficam MPL, com fonte |
| Terraform | ❌ sem licença comercial | ❌ | ⚖ | ⚖ | ⚖ | ✅ | ❌ |
| Providers e módulos | ❌ não embutir (desnecessário) | — | ✅ pelo `init` da ferramenta | ✅ pelo lock do projeto | ✅ | ✅ | ⚠️ MPL |
| agent-skills (CLI MIT, skill CC-BY-4.0) | ⚠️ com atribuição CC-BY (hoje não embute) | ⚠️ idem | ✅ via npx oficial | ✅ (pedido explícito) | — | ✅ | ⚠️ CC-BY: indicar alterações |
| CLIs de IA e Git | ❌ não embutir (proprietários, ou GPL no caso do Git) | — | — (instalação do usuário) | — | — | — | — |

## 3. Obrigações por situação

| Situação | Obrigações para o Spock |
| --- | --- |
| Incorporar no instalador (host, sidecar, interface) | Avisos de copyright e textos de licença de todos os componentes; NOTICE dos Apache; link para o fonte dos MPL; EULA com as cláusulas de .NET e Visual C++ |
| Redistribuir binário de terceiro (CPython, python-build-standalone, bibliotecas do AppImage) | Reproduzir as licenças que acompanham o binário; LGPL e Sleepycat exigem também o fonte ou uma oferta |
| Download sob demanda da fonte oficial | Nenhuma obrigação de redistribuição. Informar a licença ao usuário (já feito), respeitar os termos de uso do titular e, no caso do Terraform, a restrição de "embedded" |
| Atualizar ou manter várias versões | As mesmas da instalação, para cada versão |
| Remover | Nenhuma |
| Modificar e redistribuir | Hoje não ocorre. Se ocorrer: MPL (fonte dos arquivos alterados), LGPL (fonte da biblioteca alterada), Apache (marcar alterações), PSF (resumo das mudanças) |
