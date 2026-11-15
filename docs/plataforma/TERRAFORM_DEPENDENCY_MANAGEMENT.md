# Gestão da dependência Terraform

Como o Spock descobre, oferece, instala, verifica, atualiza e remove o Terraform em máquinas de pessoas diferentes,
sem pressupor que ele exista e sem nada acontecer sem autorização. Entrega 1 da gestão de infraestrutura
([INFRASTRUCTURE_MANAGEMENT.md](INFRASTRUCTURE_MANAGEMENT.md)). Formato dos projetos Terraform:
[TERRAFORM_PROJECT_LAYOUT.md](TERRAFORM_PROJECT_LAYOUT.md).

## 1. Decisão: gerenciador próprio, sem instalação global

| Alternativa | Por que não |
| --- | --- |
| Instalador do sistema (winget, Homebrew, apt) | Exige privilégio ou muda o PATH global; versão diferente por máquina; o Spock não controla a origem |
| tfenv / tfswitch | Mais uma ferramenta a instalar, scripts de shell de terceiros e PATH global |
| Empacotar o binário no instalador do Spock | +90 MB por plataforma, versão presa à do Spock e a licença BUSL do Terraform passaria a ser redistribuída pelo Spock |
| **Download oficial, verificado, em pasta própria** | Escolhida: só a HashiCorp como fonte, assinatura conferida, várias versões lado a lado, nenhum PATH alterado |

Não havia mecanismo de dependências no Spock para reaproveitar: o `ExecutableLocator` só procura executáveis, e o
instalador da especificação roda `npx`. O que foi reaproveitado: `IExecutableLocator` (procura do Terraform externo),
`IProcessRunner` sem shell (agora com variáveis de ambiente opcionais), `AtomicFile`, `IAuditLog`, `WorkspaceAuthorizer`
e o padrão "proposta com id que a autorização precisa repetir" do `security.plan`/`security.fix`.

## 2. Peças

| Peça | Onde | Papel |
| --- | --- | --- |
| `TerraformDependencyService` | `Application/Iac` | Detecção, compatibilidade, consentimento, oferta, instalação, adoção, versão por projeto, remoção, trilha |
| `ITerraformToolchain` | `Application/Iac/Interfaces` | Porta das instalações gerenciadas e da distribuição oficial |
| `HashiCorpTerraformToolchain` | `Infrastructure/Terraform` | Download de `releases.hashicorp.com`, verificação, extração, manifesto |
| `OpenPgpKeyring` | `Infrastructure/Terraform` | Verificação de assinatura OpenPGP destacada (RSA, SHA-256/512), sem biblioteca externa |
| `hashicorp-release-key.asc` | `Infrastructure/Terraform` (recurso embutido) | Chave pública de assinatura da HashiCorp |
| `TerraformConfigReader` | `Infrastructure/Terraform` | Lê `required_version` dos `.tf` do projeto como texto |
| `FileTerraformSettingsStore` | `Infrastructure/Terraform` | `<dados>/iac/terraform.json`: recusa, adoções e versão por projeto |
| `IacRequestHandler` | `Application/Protocol` | Métodos `iac.terraform.*`, só para a interface |
| `IacMcpTools` | `Application/Mcp` | `terraform_status`, só consulta |
| Tela Infraestrutura | `desktop/ui/src/features/infrastructure` | Consentimento, versões, versão do projeto, ciclo de vida |

## 3. Detecção

A cada consulta (`iac.terraform.status`), sem rede:

1. **Plataforma**: sistema e arquitetura nos nomes da HashiCorp (`windows|darwin|linux` × `amd64|arm64`, mais `386` e
   `arm` onde existem). Fora disso: `unsupportedPlatform`.
2. **Instalações gerenciadas**: cada pasta `<dados>/tools/terraform/<versão>/` com `install.json` válido. O hash
   SHA-256 do binário é refeito e comparado com o manifesto; o caminho vem da pasta, nunca do manifesto.
3. **Instalação externa**: o `ExecutableLocator` procura `terraform` no PATH e nas pastas usuais; a versão vem de
   `terraform version -json`, rodado sem shell, com `CHECKPOINT_DISABLE=1` (o binário não consulta a HashiCorp).
4. **Compatibilidade**: mínimo do Spock **1.5.0** (blocos `import` e saída JSON estável de `plan`/`show`) e as
   exigências do projeto (`required_version` de todos os `.tf`, ignorando `.terraform/`, `node_modules/`, `bin/`,
   `obj/` e afins; sintaxe `=`, `!=`, `>`, `>=`, `<`, `<=`, `~>` e vírgula).

Estados: `ready`, `externalAvailable`, `missing`, `declined`, `incompatible`, `unsupportedPlatform`.

**Instalação compatível existente vence.** Se há um Terraform externo compatível, a tela oferece usá-lo
("Usar o Terraform X desta máquina") e não oferece instalar outro. Se a pessoa pedir uma proposta mesmo assim, a
proposta abre com "Já existe o Terraform X compatível nesta máquina... Usá-lo dispensa esta instalação".

**Externo só depois de adotado.** Rodar `version -json` é a única execução de um binário externo antes da escolha da
pessoa. Adotar grava caminho, versão e hash; se o binário mudar (atualização, troca), a adoção cai e a tela pede nova
confirmação. A adoção exige a versão que a pessoa viu: se o binário responder outra, nada é adotado.

## 4. Consentimento

```
missing ──"Autorizar instalação"──► proposta (iac.terraform.offer) ──"Instalar Terraform X"──► install(offerId, versão)
   │
   └──"Não instalar agora"──► declined (gravado; nada é oferecido nem tentado até a pessoa pedir)
```

- O texto da tela é o do pedido de escopo ("O Spock possui recursos de gerenciamento de infraestrutura...
  Nenhuma instalação será realizada sem sua autorização.").
- **A autorização vale para a proposta mostrada.** O `offerId` é o SHA-256 (24 primeiros hex) de versão, plataforma,
  URL do pacote, URL dos checksums, URL da assinatura e impressão digital da chave. A instalação refaz a proposta e
  recusa com `IAC_OFFER_STALE` se qualquer parte mudou.
- A proposta mostra versão, plataforma, origem, chave que assina, data de publicação, changelog e os impactos.
- **Recusa** grava a data em `iac/terraform.json`; o resto do Spock segue igual e nada roda em segundo plano.
  "Instalar o Terraform" continua disponível na tela para quando a pessoa quiser.
- Só a interface chama `iac.terraform.*` (allowlist do host Rust e da UI). O servidor MCP não tem ferramenta de
  instalação.

## 5. Instalação verificada

1. Metadados de `https://api.releases.hashicorp.com/v1/releases/terraform` (lista, ou a versão pedida).
2. Toda URL precisa ser `https://releases.hashicorp.com/terraform/<versão>/...`; o cliente HTTP não segue
   redirecionamento.
3. Baixa `terraform_<v>_SHA256SUMS` e a assinatura `.72D7468F.sig` (até 1 MB cada).
4. **Assinatura**: OpenPGP v4, tipo binário, RSA com SHA-256/512, emissor pelo subpacote de impressão digital (33). O
   emissor tem de ser a chave primária ou uma subchave do chaveiro embutido:
   - primária `C874 011F 0AB4 0511 0D02 1055 3436 5D94 72D7 468F` (HashiCorp Security);
   - assinatura atual feita pela subchave `374E C75B 4859 1360 4A83 1CC7 C820 C6D5 CD27 AB87`.
5. **Checksum**: o SHA-256 do zip, calculado durante o download (até 256 MB), tem de bater com a linha da lista
   assinada.
6. **Extração**: só a entrada `terraform` / `terraform.exe` na raiz do zip; nenhum outro caminho chega ao disco.
   No Linux e no macOS, permissão `u+rwx`.
7. Manifesto `install.json` (versão, plataforma, hash do zip e do binário, URL, chave que assinou, data), tudo numa
   pasta `.staging-*` movida para `<versão>/` no fim. Qualquer falha apaga a pasta temporária.

Falhas: `IAC_DOWNLOAD_FAILED`, `IAC_SIGNATURE_INVALID`, `IAC_CHECKSUM_MISMATCH`, `IAC_ARCHIVE_INVALID`, todas
registradas como `iac.terraform.install_failed` com o código.

**Antes de cada uso** (`ResolveForUseAsync`, a porta das próximas entregas) o hash do binário é conferido de novo;
binário alterado deixa de ser usado até ser removido e reinstalado.

Limites da verificação: as assinaturas de vínculo das subchaves não são conferidas (o chaveiro é fixo no código, e a
confiança vem dele); a validade da chave não é checada. **Rotação**: se a HashiCorp trocar a chave, a instalação passa
a falhar com `IAC_SIGNATURE_INVALID` até uma versão do Spock trazer o arquivo novo; nunca há download sem assinatura.

## 6. Versões por projeto e atualização

- Várias versões convivem em `<dados>/tools/terraform/`.
- Cada projeto tem uma versão **fixada** (`iac.terraform.pin`, por versão e origem) ou **automática** (a mais nova
  compatível). A automática vira fixa no primeiro uso (`ResolveForUseAsync`), registrado como
  `iac.terraform.pinned` com `automatic: true`: instalar uma versão nova depois disso não muda o projeto.
- Fixar respeita o `required_version` do projeto (`IAC_PROJECT_INCOMPATIBLE`).
- **Atualizar é instalar ao lado**, com a mesma autorização. A proposta de atualização mostra a versão atual e a nova,
  avisa quando muda a versão menor (ler o changelog) e lista os projetos fixados em cada versão. Nenhum projeto é
  movido; a pessoa fixa a versão nova onde quiser.
- **Remover** pede confirmação na tela e `confirmed: true` no protocolo, e é recusado se algum projeto estiver fixado
  na versão (`IAC_VERSION_IN_USE`). O nome da versão é validado antes de virar caminho.

## 7. Trilha de auditoria

| Ação | Detalhe |
| --- | --- |
| `iac.terraform.declined` | sem detalhe |
| `iac.terraform.installed` | versão, plataforma, URL, SHA-256 do zip, chave que assinou, se foi atualização |
| `iac.terraform.install_failed` | versão, plataforma, código do erro |
| `iac.terraform.adopted` | versão |
| `iac.terraform.pinned` / `unpinned` | projeto, versão, origem, se foi automática (aparece na Auditoria do projeto) |
| `iac.terraform.removed` | versão |

## 8. Protocolo

| Método | Parâmetros | Resposta |
| --- | --- | --- |
| `iac.terraform.status` | `projectId?` | `{ terraform: TerraformStatus }` (caminhos com `~`) |
| `iac.terraform.offer` | `version?`, `projectId?` | `{ offer: TerraformOffer }` (fala com a rede) |
| `iac.terraform.install` | `offerId`, `version` | status |
| `iac.terraform.decline` | nenhum | status |
| `iac.terraform.adopt` | `version` (a que a pessoa viu) | status |
| `iac.terraform.pin` | `projectId`, `version?` (sem ela: automático), `source?` | status |
| `iac.terraform.remove` | `version`, `confirmed` | status |

Fixtures: `contracts/fixtures/iac/` (geradas da saída real do motor, caminhos anonimizados). O host espera até 660 s
por `iac.terraform.install` (o download tem até 10 min no motor).

## 9. Compatibilidade e licença

| Sistema | Arquiteturas | Situação |
| --- | --- | --- |
| Windows | amd64, arm64, 386 | amd64 validado de ponta a ponta (instalação real, binário respondeu) |
| macOS | amd64, arm64 | Implementado; a permissão de execução é dada na extração; não rodado em Mac real |
| Linux | amd64, arm64, arm, 386 | Implementado; não rodado em Linux real |

O Terraform é distribuído pela HashiCorp sob a Business Source License 1.1 desde a 1.6. O Spock não redistribui o
binário: a pessoa o baixa da HashiCorp, na própria máquina, com autorização explícita. Se o Spock virar produto
comercial que ofereça Terraform como serviço a terceiros, a licença precisa ser revista pelo dono; o OpenTofu (MPL 2.0,
mesma interface de linha de comando) é a alternativa e caberia como segunda distribuição atrás da mesma porta
`ITerraformToolchain`.

## 10. Testes

- `TerraformDependencyTests`: sintaxe de restrição, assinatura real da HashiCorp aceita e qualquer byte trocado
  recusado, instalação com servidor falso e chave de teste, autorização forjada, checksum trocado, lista sem assinatura
  válida, zip sem o binário, binário adulterado, externo adotado e alterado, externo abaixo do mínimo, leitura de
  `required_version`, fixação no primeiro uso e atualização lado a lado, remoção bloqueada, ferramenta MCP só de leitura.
- `TerraformLiveTests` (com `MARILIZ_LIVE_TERRAFORM=1`): baixa a versão publicada da HashiCorp, verifica e roda o
  binário instalado.
- `infrastructure-page.test.tsx`: nada é baixado antes de autorizar, instalação só da proposta mostrada, recusa,
  versão do projeto, etapas do ciclo.
