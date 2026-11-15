# Gestão da dependência: OpenTofu e Terraform

Como o Spock descobre, oferece, instala, verifica, atualiza e remove a ferramenta de infraestrutura como código em
máquinas de pessoas diferentes, sem pressupor que ela exista e sem nada acontecer sem autorização. Entrega 1 da gestão
de infraestrutura ([INFRASTRUCTURE_MANAGEMENT.md](INFRASTRUCTURE_MANAGEMENT.md)). Formato dos projetos:
[TERRAFORM_PROJECT_LAYOUT.md](TERRAFORM_PROJECT_LAYOUT.md).

## 1. OpenTofu como padrão

O Spock trabalha com duas ferramentas que entendem a mesma linguagem (HCL), os mesmos providers e o mesmo layout:

| Ferramenta | Licença | Custo | Papel no Spock |
| --- | --- | --- | --- |
| **OpenTofu** (Linux Foundation) | MPL 2.0, código aberto | Gratuito, sem licença a contratar, inclusive em produto comercial | **Padrão**: é o que o Spock oferece instalar e o que escolhe primeiro |
| Terraform (HashiCorp) | BUSL 1.1 desde a 1.6 | Gratuito para a própria infraestrutura; oferecer o Terraform como serviço que concorra com a HashiCorp exige acordo com ela | Escolha explícita ("Prefiro o Terraform da HashiCorp") ou instalação que a pessoa já tem |

Decisão do dono em 2026-11-15: preferir a ferramenta gratuita. Com o OpenTofu o Spock não precisa negociar licença
nenhuma, mesmo que vire produto. O Terraform continua suportado para quem já o usa ou precisa dele, e quem decide usá-lo
assume a licença da HashiCorp.

Onde a preferência aparece:

- `iac.terraform.offer` sem `engine` propõe o OpenTofu.
- A escolha automática do projeto põe o OpenTofu antes do Terraform (depois, gerenciada antes de externa e a versão mais
  nova primeiro).
- O texto de consentimento explica o OpenTofu, a licença e o custo zero.
- Uma instalação existente de qualquer das duas, compatível, dispensa instalar outra (seção 4).

## 2. Decisão: gerenciador próprio, sem instalação global

| Alternativa | Por que não |
| --- | --- |
| Instalador do sistema (winget, Homebrew, apt) | Exige privilégio ou muda o PATH global; versão diferente por máquina; o Spock não controla a origem |
| tfenv / tofuenv / tfswitch | Mais uma ferramenta a instalar, scripts de shell de terceiros e PATH global |
| Empacotar o binário no instalador do Spock | +100 MB por plataforma e versão presa à do Spock |
| **Download oficial, verificado, em pasta própria** | Escolhida: só a fonte oficial de cada ferramenta, assinatura conferida, várias versões lado a lado, nenhum PATH alterado |

Não havia mecanismo de dependências no Spock para reaproveitar: o `ExecutableLocator` só procura executáveis, e o
instalador da especificação roda `npx`. O que foi reaproveitado: `IExecutableLocator` (procura das instalações
externas), `IProcessRunner` sem shell (agora com variáveis de ambiente opcionais), `AtomicFile`, `IAuditLog`,
`WorkspaceAuthorizer` e o padrão "proposta com id que a autorização precisa repetir" do `security.plan`/`security.fix`.

## 3. Peças

| Peça | Onde | Papel |
| --- | --- | --- |
| `TerraformDependencyService` | `Application/Iac` | Detecção, compatibilidade, preferência, consentimento, oferta, instalação, adoção, ferramenta e versão por projeto, remoção, trilha |
| `ITerraformToolchain` | `Application/Iac/Interfaces` | Porta de uma ferramenta: instalações gerenciadas e distribuição oficial |
| `OfficialReleaseToolchain` | `Infrastructure/Terraform` | O comum às duas: pasta, download, redirecionamento controlado, verificação, extração, manifesto |
| `OpenTofuToolchain` | `Infrastructure/Terraform` | Versões de `get.opentofu.org`, arquivos das releases do GitHub do OpenTofu |
| `HashiCorpTerraformToolchain` | `Infrastructure/Terraform` | API e arquivos de `releases.hashicorp.com` |
| `OpenPgpKeyring` | `Infrastructure/Terraform` | Verificação de assinatura OpenPGP destacada (RSA, SHA-256/512), sem biblioteca externa |
| `opentofu-release-key.asc`, `hashicorp-release-key.asc` | `Infrastructure/Terraform` (recursos embutidos) | Chaves públicas de assinatura |
| `TerraformConfigReader` | `Infrastructure/Terraform` | Lê `required_version` dos `.tf` e `.tofu` do projeto como texto |
| `FileTerraformSettingsStore` | `Infrastructure/Terraform` | `<dados>/iac/terraform.json`: recusa, adoções e ferramenta/versão por projeto |
| `IacRequestHandler` | `Application/Protocol` | Métodos `iac.terraform.*`, só para a interface |
| `IacMcpTools` | `Application/Mcp` | `terraform_status`, só consulta |
| Tela Infraestrutura | `desktop/ui/src/features/infrastructure` | Consentimento, ferramentas, ferramenta do projeto, ciclo de vida |

Os nomes no código (`Terraform*`, `iac.terraform.*`) falam da linguagem e do ecossistema; a ferramenta é o campo
`engine` (`openTofu` ou `terraform`) em toda instalação, proposta, adoção e fixação.

## 4. Detecção

A cada consulta (`iac.terraform.status`), sem rede:

1. **Plataforma**: sistema e arquitetura nos nomes das distribuições (`windows|darwin|linux` × `amd64|arm64`, mais
   `386` e `arm` onde existem).
2. **Instalações gerenciadas**: cada pasta `<dados>/tools/opentofu/<versão>/` e `<dados>/tools/terraform/<versão>/`
   com `install.json` válido. O hash SHA-256 do binário é refeito e comparado com o manifesto; caminho e ferramenta
   vêm da pasta, nunca do manifesto.
3. **Instalações externas**: o `ExecutableLocator` procura `tofu` e `terraform` no PATH e nas pastas usuais; a versão
   vem de `version -json` (as duas ferramentas usam a chave `terraform_version`), rodado sem shell, com
   `CHECKPOINT_DISABLE=1` (o Terraform não consulta a HashiCorp; o OpenTofu não tem essa consulta).
4. **Compatibilidade**: mínimo do Spock **OpenTofu 1.6.0** e **Terraform 1.5.0** (blocos `import` e saída JSON estável
   de `plan`/`show`) e as exigências do projeto (`required_version` de todos os `.tf` e `.tofu`, ignorando
   `.terraform/`, `node_modules/`, `bin/`, `obj/` e afins; sintaxe `=`, `!=`, `>`, `>=`, `<`, `<=`, `~>` e vírgula). O
   OpenTofu compara `required_version` com a própria versão: o `>= 1.11` do modelo aceita o OpenTofu 1.11 em diante.

Estados: `ready`, `externalAvailable`, `missing`, `declined`, `incompatible`, `unsupportedPlatform`.

**Instalação compatível existente vence.** Se há uma instalação externa compatível (OpenTofu ou Terraform), a tela
oferece usá-la ("Usar o Terraform X desta máquina") e não oferece instalar outra. Se a pessoa pedir uma proposta mesmo
assim, a proposta abre com "Já existe o X compatível nesta máquina... Usá-lo dispensa esta instalação".

**Externa só depois de adotada.** Rodar `version -json` é a única execução de um binário externo antes da escolha da
pessoa. Adotar grava ferramenta, caminho, versão e hash; se o binário mudar, a adoção cai e a tela pede nova
confirmação. A adoção exige a versão que a pessoa viu: se o binário responder outra, nada é adotado.

## 5. Consentimento

```
missing ──"Autorizar instalação"──► proposta do OpenTofu ──"Instalar OpenTofu X"──► install(offerId, versão, openTofu)
   │                └──"Prefiro o Terraform da HashiCorp"──► proposta do Terraform ──► install(..., terraform)
   └──"Não instalar agora"──► declined (gravado; nada é oferecido nem tentado até a pessoa pedir)
```

- O texto da tela segue o do pedido de escopo, trocando "instalar o Terraform" por "instalar o OpenTofu nesta máquina:
  ferramenta gratuita e de código aberto (licença MPL 2.0, mantida pela Linux Foundation), compatível com os arquivos e
  providers do Terraform, sem licença a contratar".
- **A autorização vale para a proposta mostrada.** O `offerId` é o SHA-256 (24 primeiros hex) de ferramenta, versão,
  plataforma, URL do pacote, URL dos checksums, URL da assinatura e impressão digital da chave. A instalação refaz a
  proposta e recusa com `IAC_OFFER_STALE` se qualquer parte mudou.
- A proposta mostra ferramenta, licença, versão, plataforma, origem, chave que assina, data de publicação (quando a
  fonte informa), changelog e os impactos.
- **Recusa** grava a data; o resto do Spock segue igual e nada roda em segundo plano.
- Só a interface chama `iac.terraform.*` (allowlist do host Rust e da UI). O servidor MCP não tem ferramenta de
  instalação.

## 6. Instalação verificada

| | OpenTofu | Terraform |
| --- | --- | --- |
| Lista de versões | `https://get.opentofu.org/tofu/api.json` | `https://api.releases.hashicorp.com/v1/releases/terraform` |
| Arquivos | `https://github.com/opentofu/opentofu/releases/download/v<v>/` | `https://releases.hashicorp.com/terraform/<v>/` |
| Redirecionamento | Só para a CDN do GitHub (`release-assets.githubusercontent.com`, `objects.githubusercontent.com`), HTTPS, até 3 saltos | Nenhum |
| Pacote | `tofu_<v>_<os>_<arch>.zip` | `terraform_<v>_<os>_<arch>.zip` |
| Lista assinada | `tofu_<v>_SHA256SUMS` + `.gpgsig` (SHA-512) | `terraform_<v>_SHA256SUMS` + `.72D7468F.sig` (SHA-256) |
| Chave primária | `E3E6 E43D 84CB 852E ADB0 051D 0C0A F313 E5FD 9F80` (OpenTofu) | `C874 011F 0AB4 0511 0D02 1055 3436 5D94 72D7 468F` (HashiCorp Security) |
| Assina hoje | A primária | A subchave `374E C75B 4859 1360 4A83 1CC7 C820 C6D5 CD27 AB87` |
| Binário extraído | `tofu` / `tofu.exe` | `terraform` / `terraform.exe` |

Passos, iguais para as duas:

1. Toda URL de arquivo precisa estar na fonte oficial da versão; o cliente HTTP não segue redirecionamento sozinho.
2. Baixa a lista de checksums e a assinatura (até 1 MB cada).
3. **Assinatura**: OpenPGP v4, tipo binário, RSA com SHA-256/512, emissor pelo subpacote de impressão digital (33),
   que tem de ser a chave primária ou uma subchave do chaveiro embutido daquela ferramenta.
4. **Checksum**: o SHA-256 do zip, calculado durante o download (até 256 MB), tem de bater com a linha da lista
   assinada.
5. **Extração**: só o binário na raiz do zip; nenhum outro caminho (LICENSE, README) chega ao disco. No Linux e no
   macOS, permissão `u+rwx`.
6. Manifesto `install.json` (ferramenta, versão, plataforma, hash do zip e do binário, URL, chave que assinou, data)
   numa pasta `.staging-*` movida para `<versão>/` no fim. Qualquer falha apaga a pasta temporária.

Falhas: `IAC_DOWNLOAD_FAILED` (inclui redirecionamento recusado), `IAC_SIGNATURE_INVALID`, `IAC_CHECKSUM_MISMATCH`,
`IAC_ARCHIVE_INVALID`, todas registradas como `iac.terraform.install_failed` com ferramenta e código.

**Antes de cada uso** (`ResolveForUseAsync`, a porta das próximas entregas) o hash do binário é conferido de novo;
binário alterado deixa de ser usado. Instalar a mesma versão de novo pela tela substitui o binário que falhou na
verificação.

Limites da verificação: as assinaturas de vínculo das subchaves não são conferidas (o chaveiro é fixo no código, e a
confiança vem dele); a validade da chave não é checada; a assinatura cosign que o OpenTofu também publica não é usada.
**Rotação**: se uma das chaves mudar, a instalação daquela ferramenta passa a falhar com `IAC_SIGNATURE_INVALID` até
uma versão do Spock trazer o arquivo novo; nunca há download sem assinatura.

## 7. Ferramenta e versão por projeto, atualização

- Várias versões das duas ferramentas convivem em `<dados>/tools/`.
- Cada projeto tem ferramenta e versão **fixadas** (`iac.terraform.pin` com `engine`, `version`, `source`) ou
  **automáticas** (OpenTofu antes de Terraform, a mais nova compatível). A automática vira fixa no primeiro uso
  (`ResolveForUseAsync`), registrado como `iac.terraform.pinned` com `automatic: true`: instalar outra ferramenta ou
  versão depois disso não muda o projeto.
- Fixar respeita o `required_version` do projeto (`IAC_PROJECT_INCOMPATIBLE`).
- **Um projeto, uma ferramenta.** O state escrito por uma pode não ser lido pela outra em versões futuras, e o
  `.terraform.lock.hcl` registra providers por registro (`registry.opentofu.org` × `registry.terraform.io`). Trocar a
  ferramenta de um projeto é decisão explícita da pessoa
  ([TERRAFORM_PROJECT_LAYOUT.md](TERRAFORM_PROJECT_LAYOUT.md), seção 4).
- **Atualizar é instalar ao lado**, com a mesma autorização. A proposta de atualização mostra a versão atual e a nova,
  avisa quando muda a versão menor e lista os projetos fixados em cada versão daquela ferramenta.
- **Remover** pede confirmação na tela e `confirmed: true` no protocolo, e é recusado se algum projeto estiver fixado
  naquela ferramenta e versão (`IAC_VERSION_IN_USE`). O nome da versão é validado antes de virar caminho.

## 8. Trilha de auditoria

| Ação | Detalhe |
| --- | --- |
| `iac.terraform.declined` | sem detalhe |
| `iac.terraform.installed` | ferramenta, versão, plataforma, URL, SHA-256 do zip, chave que assinou, se foi atualização |
| `iac.terraform.install_failed` | ferramenta, versão, plataforma, código do erro |
| `iac.terraform.adopted` | ferramenta, versão |
| `iac.terraform.pinned` / `unpinned` | projeto, ferramenta, versão, origem, se foi automática (aparece na Auditoria do projeto) |
| `iac.terraform.removed` | ferramenta, versão |

## 9. Protocolo

| Método | Parâmetros | Resposta |
| --- | --- | --- |
| `iac.terraform.status` | `projectId?` | `{ terraform: TerraformStatus }` com `engines` (nome, licença, mínimo, preferida) e caminhos com `~` |
| `iac.terraform.offer` | `version?`, `projectId?`, `engine?` (padrão `openTofu`) | `{ offer: TerraformOffer }` (fala com a rede) |
| `iac.terraform.install` | `offerId`, `version`, `engine` | status |
| `iac.terraform.decline` | nenhum | status |
| `iac.terraform.adopt` | `engine`, `version` (a que a pessoa viu) | status |
| `iac.terraform.pin` | `projectId`, `engine?`, `version?` (sem ela: automático), `source?` | status |
| `iac.terraform.remove` | `engine`, `version`, `confirmed` | status |

Fixtures: `contracts/fixtures/iac/` (geradas da saída real do motor, caminhos anonimizados). O host espera até 660 s
por `iac.terraform.install` (o download tem até 10 min no motor).

## 10. Compatibilidade

| Sistema | Arquiteturas | Situação |
| --- | --- | --- |
| Windows | amd64, arm64, 386 | amd64 validado de ponta a ponta com as duas ferramentas (OpenTofu 1.13.9 e Terraform 1.17.2 instalados e respondendo) |
| macOS | amd64, arm64 | Implementado; não rodado em Mac real |
| Linux | amd64, arm64, arm, 386 | Implementado; não rodado em Linux real |

## 11. Testes

- `OpenTofuDependencyTests`: assinatura real do OpenTofu aceita com a chave embutida, proposta padrão do OpenTofu com
  licença, instalação pelo redirecionamento para a CDN do GitHub, redirecionamento para outro host recusado, projeto
  com as duas ferramentas (OpenTofu automático, Terraform fixado), `required_version` do modelo e arquivos `.tofu`.
- `TerraformDependencyTests`: sintaxe de restrição, assinatura real da HashiCorp, instalação com servidor falso e chave
  de teste, autorização forjada, checksum trocado, lista sem assinatura válida, zip sem o binário, binário adulterado e
  reinstalado, externo adotado e alterado, externo abaixo do mínimo, fixação no primeiro uso e atualização lado a lado,
  remoção bloqueada, ferramenta MCP só de leitura.
- `TerraformLiveTests` (com `MARILIZ_LIVE_TERRAFORM=1`): baixa o OpenTofu e o Terraform publicados, verifica e roda os
  binários instalados.
- `infrastructure-page.test.tsx`: nada é baixado antes de autorizar, proposta e instalação do OpenTofu por padrão,
  Terraform como escolha explícita, recusa, ferramenta do projeto, etapas do ciclo.
