# Decisões de licenciamento

Data: 2026-11-15. Registro das decisões de arquitetura ligadas a licenças: as já tomadas no código, as recomendadas por esta
auditoria e as que **aguardam aprovação**. Nenhuma substituição, remoção ou contratação foi feita durante a auditoria.

| ID | Decisão | Situação |
| --- | --- | --- |
| D-01 | OpenTofu como motor padrão de IaC | Adotada (2026-11-15, decisão do dono) |
| D-02 | Terraform: instalação gerenciada pelo Spock | **Aguarda aprovação e parecer** |
| D-03 | Ferramentas de IaC por download oficial sob demanda, nunca embarcadas | Adotada |
| D-04 | CPython embarcado (embed no Windows, python-build-standalone no Linux) | Adotada; ajuste pendente (D-05) |
| D-05 | Remover o `_dbm` (Berkeley DB) do Python do Linux | **Aguarda aprovação** |
| D-06 | Formato do pacote Linux na 1ª versão (AppImage com LGPL ou só `.deb`) | **Aguarda aprovação** |
| D-07 | Avisos de terceiros gerados do inventário e embarcados | **Aguarda aprovação** |
| D-08 | Integração com CLIs de IA só invocando o binário do usuário | Adotada; confirmação pendente |
| D-09 | EULA do Spock | **Aguarda redação e aprovação** |
| D-10 | Política de licenças para novas dependências | **Aguarda aprovação** |
| D-11 | Validador de JSON Schema dos testes (json-everything sob o EULA de taxa) | **Aguarda aprovação** |
| D-12 | Nome e marca "Spock" | **Aguarda parecer de marca** |

## Adotadas

### D-01: OpenTofu como padrão

- **Ferramenta adotada:** OpenTofu (MPL-2.0, Linux Foundation). Sem custo nem licença a contratar, mesmo em produto comercial.
- **Justificativa:** mesma linguagem, providers e layout do Terraform, sem as restrições da BUSL.
- **Obrigações:** nenhuma de redistribuição enquanto o Spock só baixa da fonte oficial. Usar o nome "OpenTofu" só de forma
  referencial, como "compatível com OpenTofu"; ele é marca da LF Projects.
- **Evidência:** `docs/plataforma/TERRAFORM_DEPENDENCY_MANAGEMENT.md` §1 e `IacRuntimeService`.

### D-03: download oficial sob demanda

- **Descartados:** embarcar o binário no instalador (+100 MB por plataforma e, para o Terraform, redistribuição sob BUSL),
  gerenciadores do sistema (winget, apt, brew) e tfenv/tofuenv.
- **Justificativa de licença:** o Spock não vira distribuidor dos binários.
- **Restrição:** no Terraform, isso não afasta a definição de "embedded" (D-02).

### D-04: CPython embarcado

- **Justificativa:** o usuário não precisa instalar Python. As licenças são permissivas (PSF, OpenSSL Apache-2.0, MIT, BSD).
- **Obrigações:**
  - LICENSE.txt do CPython nos avisos.
  - EULA protetivo para o Distributable Code da Microsoft no Windows.
  - Licenças das bibliotecas nativas no Linux.

### D-08: CLIs de IA (Claude Code, Codex, Cursor, Gemini)

- **Regra atual:** o Spock só invoca o binário instalado pelo usuário, em modo somente leitura, sem embarcar nem modificar o
  binário.
- **Confirmação pendente (R-10):** registrar que o Spock não lê, não armazena e não intermedeia credenciais ou tokens das
  contas Claude.ai, e que nenhum material usa "Claude Code" como parte da marca.
- **Cursor:** termos de integração ainda não confirmados (R-13).

## Aguardando aprovação

### D-02: Terraform gerenciado pelo Spock ⚖

- **Situação atual:** o Terraform é "liberado, sem trava". O jurídico negocia à parte (decisão do dono de 2026-11-15).
- **Restrição documentada (BUSL 1.1, Additional Use Grant):** proíbe oferta paga competitiva "hosted or embedded". "Embedded"
  inclui depender do download do Terraform para operar.
- **Opções:**
  1. **Recomendada:** em builds comerciais, só OpenTofu é instalado pelo Spock. O Terraform é usado apenas se já estiver
     instalado pelo usuário, com a licença exibida. Assim fica até o parecer.
  2. Manter como está e obter parecer, ou esclarecimento escrito da IBM/HashiCorp, antes do lançamento.
  3. Contratar licença comercial.
- **Também:** corrigir o texto da proposta para citar o caso "embedded" e o licenciante atual (IBM).
- **Pendência jurídica:** "competitive offering" / "significantly overlaps" (LICENSE_AUDIT.md §5).

### D-05: remover o `_dbm` do python-build-standalone

- **Motivo:** Berkeley DB 6.0.19 sob Sleepycat (obrigação de fonte de alcance incerto). A inteligência não usa `dbm`.
- **Proposta:** acrescentar `_dbm` à poda de `lib-dynload` e remover `libdb*` em `scripts/package-intelligence.py`. O smoke do
  instalador confirma a remoção.
- **Custo:** baixo. **Alternativa:** parecer sobre a Sleepycat.

### D-06: pacote Linux

- **Opção A, recomendada para a 1ª versão:** só `.deb`. As bibliotecas GTK/WebKitGTK vêm do sistema, sem obrigação LGPL de
  redistribuição.
- **Opção B:** AppImage com o pacote de conformidade LGPL.
  - Textos das licenças.
  - Fonte correspondente das bibliotecas embutidas, ou oferta escrita.
  - Bibliotecas substituíveis.
  - EULA sem proibição de engenharia reversa para depurar essas bibliotecas.

### D-07: avisos de terceiros no produto

- **Proposta:** o build gera os avisos com `docs/compliance/tools/generate_sbom.py`, a partir dos lockfiles e dos textos
  originais.
  - Os avisos entram como recurso do Tauri (`resources/`), acessível por "Sobre".
  - O smoke do instalador confere a presença.
  - Os textos que faltam são completados antes (THIRD_PARTY_NOTICES.md, "Lacunas").
- **Alternativa:** ferramentas dedicadas (cargo-about, license-checker, pip-licenses, syft). Exigem autorização para instalar.

### D-09: EULA do Spock

- **Cláusulas exigidas por terceiros:**
  - Proteção ao código da Microsoft, pela .NET Library License e pelo Visual C++/CPython: sem engenharia reversa do código da
    Microsoft, exceto onde a lei permite; uso só com o app; indenização; sem uso de marcas da Microsoft.
  - Isenção de garantia de terceiros.
  - Se houver AppImage: nada que impeça modificar ou depurar as bibliotecas LGPL.
- **Exibição:** no NSIS (`bundle.windows.nsis.license`) e no pacote Linux.
- **Pendência jurídica:** redação.

### D-10: política de licenças para novas dependências

| Categoria | Licenças | Regra |
| --- | --- | --- |
| Permitidas em distribuição | MIT, MIT-0, Apache-2.0, BSD-2/3, ISC, 0BSD, Zlib, Unicode-3.0, Unlicense, CC0-1.0, BSL-1.0, PSF/Python-2.0 | Livre, com avisos |
| Revisão antes de distribuir | MPL-2.0, LGPL, EPL, CDDL, CC-BY, licenças com "file" ou LicenseRef, termos de serviço, EULAs de taxa (OSMF) | Aprovação registrada aqui |
| Proibidas em distribuição | GPL, AGPL, SSPL, BUSL, Elastic, Commons Clause, sem licença | Só como processo externo do usuário |
| Desenvolvimento | Qualquer licença aberta; EULAs com taxa passam por revisão | — |

- **Verificação:** `generate_sbom.py --check` no CI e, com autorização, `cargo-deny`.

### D-11: validador de JSON Schema dos testes

- **Situação:** `JsonSchema.Net` 9.5.0 (com `Json.More.Net` e `JsonPointer.Net`) vem com o EULA Open Source Maintenance Fee.
  Ele cobra taxa de usuários com receita anual ≥ US$10.000; o fonte continua MIT.
- **Opções:**
  1. Pagar a taxa ao json-everything.
  2. Compilar do fonte (dispensa o EULA, mas cria manutenção).
  3. Trocar por um validador sem taxa.
- **Restrição:** decisão de custo, sem impacto no produto distribuído.

### D-12: nome "Spock"

- **Situação:** busca de anterioridade no INPI e no USPTO (classes 9 e 42) e avaliação do logotipo pendentes antes da
  comercialização.
- **Contexto:** SPOCK é marca da CBS Studios na classe 28; há marcas Star Trek na classe 9.

## Pendências jurídicas consolidadas

1. BUSL 1.1: o Spock pago é "competitive offering"? O cenário 2 (Terraform do usuário) está fora de "embedded"? (D-02)
2. Sleepycat: alcance da obrigação de fonte, caso o `_dbm` seja mantido. (D-05)
3. LGPL no AppImage, caso a opção B seja escolhida. (D-06)
4. Redação do EULA com as cláusulas da Microsoft. (D-09)
5. Repasse do Visual C++ runtime recebido via PSF. (R-06)
6. Termos de integração da Anthropic (confirmação) e da Cursor. (D-08)
7. Marca "Spock". (D-12)
