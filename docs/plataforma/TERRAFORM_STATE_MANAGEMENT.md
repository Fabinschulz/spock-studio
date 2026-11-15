# Gestão do state

O state descreve tudo o que a ferramenta (OpenTofu ou Terraform) gerencia e pode conter dados confidenciais (senhas
geradas, chaves, endereços internos). Esta página diz como o Spock lê, protege, devolve e isola o state. Entrega 4 de
[INFRASTRUCTURE_MANAGEMENT.md](INFRASTRUCTURE_MANAGEMENT.md); o formato vem do modelo
([TERRAFORM_PROJECT_LAYOUT.md](TERRAFORM_PROJECT_LAYOUT.md), seção 5).

## 1. Regras que não mudam

- O Spock nunca sobrescreve, move, apaga ou recria um state sem a aprovação de um apply daquele plano, e nunca
  sobrescreve um state que mudou desde o plano (seção 4).
- **Um state por projeto e ambiente.** Dois projetos do Spock nunca planejam sobre o mesmo state remoto (seção 5).
- State é dado sensível: conteúdo não vai para logs, telas, trilha nem agentes. O Spock lê do state só endereços
  (`state list`), nomes de workspace e outputs não sensíveis.
- O Spock não guarda cópia do state remoto; backup é do backend (versionamento). O state local ganha `.backup` a cada
  devolução, como a própria ferramenta faz.
- `state rm`, `state mv`, `import` e `init -migrate-state` não são feitos pelo Spock. `force-unlock` só com o id que a
  ferramenta informou e com confirmação da pessoa (seção 6).

## 2. Onde vive o state de cada raiz (`iac.state`, `iac_state`)

`IacStateService.ReportAsync` lê a configuração como texto (`IacStateReader`), sem rodar a ferramenta. Raiz é a pasta
com bloco `provider` ou `backend`; módulos não entram.

| Modo | Quando | O que o Spock faz no apply |
| --- | --- | --- |
| `remote` | `backend "s3"`, `"azurerm"`, `"gcs"` e outros | A ferramenta grava no backend |
| `localAbsolute` | `backend "local"` com `path` absoluto fora do projeto | A ferramenta grava naquele caminho |
| `localInProject` | Sem backend, ou `local` com caminho relativo | Aplica na cópia e devolve o state ao projeto (seção 4) |

Do bloco `backend` só saem as configurações da lista segura (`bucket`, `key`, `workspace_key_prefix`, `region`,
`encrypt`, `use_lockfile`, `dynamodb_table`, `profile`, `resource_group_name`, `storage_account_name`,
`container_name`, `use_azuread_auth`, `prefix`, `path`, `workspace_dir`, `hostname`, `organization`). `access_key`,
`secret_key`, `sas_token`, `token` e afins nunca são lidos. O texto é lido com `\r\n` normalizado: a validação com o
motor real pegou o parser falhando em arquivos salvos no Windows.

Riscos apontados por raiz:

| Regra | Nível | Quando |
| --- | --- | --- |
| `shared` | Grave | Outro projeto do Spock já usa o mesmo state |
| `state-in-git` | Grave | Um `terraform.tfstate` da pasta não está no `.gitignore` (`git check-ignore`) |
| `local-state` | Atenção | Raiz principal com state local: sem trava compartilhada nem versões |
| `not-encrypted` | Atenção | S3 sem `encrypt = true` |
| `no-lock` | Atenção | S3 sem `use_lockfile` nem `dynamodb_table` |
| `dynamodb-lock` | Nota | `dynamodb_table` está obsoleto: `use_lockfile` resolve no próprio S3 |
| `bootstrap-local` | Nota | Bootstrap com state local, como no modelo |

**Recomendação.** Com state local na raiz principal, o Spock recomenda o backend da nuvem do projeto: a da conexão
cloud vinculada ou, sem vínculo, a do bloco `provider` (`aws` para `s3`, `azurerm` para `azurerm`, `google` para `gcs`).

## 3. Bootstrap do bucket (`iac.bootstrap.plan`, `iac.bootstrap.save`)

Gera `<raiz de IaC>/bootstrap/` no formato do modelo, para AWS, Azure ou Google:

| Nuvem | Recurso | Proteções |
| --- | --- | --- |
| AWS | `aws_s3_bucket` com versionamento, criptografia `AES256` e bloqueio de acesso público | `prevent_destroy`; `allowed_account_ids` quando a conta é conhecida |
| Azure | Resource group, `azurerm_storage_account` e container privado | `prevent_destroy`, TLS 1.2, sem blob público, versionamento e retenção de 30 dias |
| Google | `google_storage_bucket` | `prevent_destroy`, acesso uniforme, `public_access_prevention = "enforced"`, versionamento, `force_destroy = false` |

- Providers fixados em versão exata, conferida nos registros do Terraform e do OpenTofu em 2026-11-15: `hashicorp/aws`
  6.73.0, `hashicorp/azurerm` 5.14.0, `hashicorp/google` 8.11.0 (`IacBootstrapTemplates`).
- Arquivos `providers.tf`, `variables.tf` (com `validation`), `resource.tf` e `output.tf`, com cabeçalhos de seção e
  comentários do porquê, mais as linhas que faltarem no `.gitignore` da raiz (`*.tfstate`, `*.tfstate.*`, `*.tfplan`,
  `**/.terraform/`, `terraform.tfvars`, `crash.log`).
- Com a conexão cloud vinculada, conta, região, perfil, subscription e projeto vêm dela (`allowed_account_ids` com a
  conta que a conexão identificou).
- A pessoa revisa o diff de cada arquivo e o bloco `backend` sugerido para a raiz principal. `iac.bootstrap.save` só
  aceita o `planId` revisado (`STALE_PLAN` se valores ou arquivos mudaram) e grava como execução do Spock (tipo
  `infrastructure`): cópia antes, diário, conferência por hash e **desfazer** pela tela da execução.
- Gravar não cria nada na nuvem. O bucket é criado depois pelo ciclo normal da pasta `bootstrap`: validar, planejar e
  aprovar o apply. O state do bootstrap fica local e é devolvido ao projeto (seção 4).
- Migrar a raiz principal para o bucket (`init -migrate-state`) continua fora do Spock: a pessoa cola o bloco `backend`
  e migra.

Os três bootstraps passam no `fmt -check` e no `validate` do OpenTofu de verdade, com os providers baixados
(`IacRuntimeLiveTests.Bootstraps_gerados_passam_no_validate_do_OpenTofu`, com `MARILIZ_LIVE_IAC_PROVIDERS=1`). Foi esse
teste que pegou o alinhamento do `allowed_account_ids` fora do `fmt`.

**Escopo de escrita.** Até aqui o Spock só escrevia na área gerenciada (`.claude/`, `.mariliz/`) e no `.gitignore`
(correções de segurança). O bootstrap ganhou um escopo próprio, `WriteScope.Infrastructure`: só arquivos `.tf` e
`.tofu` fora de `.git/` e `.terraform/`, e o `.gitignore` da raiz. State, `.tfvars` e código continuam fora.

## 4. State local devolvido ao projeto

Plano e apply rodam sobre uma cópia da configuração. Com state local dentro da pasta:

1. **Plano**: a cópia leva o state do projeto; o plano registra o arquivo do workspace (`terraform.tfstate` ou
   `terraform.tfstate.d/<ws>/terraform.tfstate`) e o hash do state que leu (`localStateHash`). A cópia do state sai da
   pasta da operação logo depois.
2. **Apply**: o state do projeto tem de ter o mesmo hash do plano (`IAC_STATE_CHANGED` se mudou: aplicar o plano
   antigo apagaria a mudança). Ele entra na cópia, o apply roda, e o state novo **volta ao projeto**: o anterior vira
   `terraform.tfstate.backup` e o novo é gravado por arquivo temporário e troca.
3. **Conflito**: se o state do projeto mudou durante o apply, nada é sobrescrito. O state novo fica em
   `<dados>/projects/<id>/iac/operations/<apply>/recovered/terraform.tfstate` e a operação diz onde
   (`IAC_STATE_CONFLICT`).
4. Mesmo numa falha parcial o state volta: ele registra o que já foi criado na nuvem.

Continuam bloqueados, com o motivo no plano: backend `local` com caminho próprio e workspace com nome, e caminho
relativo que sai da pasta.

## 5. Isolamento entre projetos

- Identidade do state: `s3://bucket/workspace_key_prefix/key`, `azurerm://conta/container/key`, `gcs://bucket/prefix`,
  `local://<caminho absoluto>`; state local dentro do projeto é do projeto.
- `<dados>/iac/state-registry.json` guarda quais projetos usam cada identidade, gravado a cada plano bem-sucedido.
- Antes de planejar, se outro projeto do Spock já usa a mesma identidade, o plano é recusado (`IAC_STATE_SHARED`) e o
  relatório de state marca o risco `shared`.
- Workspace `default` recusado pela guarda do modelo; `TF_DATA_DIR` separado por operação.
- `allowed_account_ids` (ou `subscription_id` e `project` fixos) impede aplicar com a credencial da conta errada.

## 6. Consulta, trava e recuperação

- **Consulta** (`iac.state.resources`): `workspace list` e `state list` sobre a cópia, com a conexão vinculada se
  escolhida. Devolve nomes de workspace e endereços. Registrado como `iac.state_read` (contagem).
- **Trava**: quando o plano esbarra num lock, o id que a ferramenta informou fica na operação (`lockId`) e a tela
  oferece "Liberar a trava do state", com o aviso de que ninguém pode estar aplicando. `iac.state.unlock` exige
  `confirmed: true` e o id no formato da ferramenta (`IAC_UNLOCK_CONFIRMATION_REQUIRED`, `IAC_LOCK_ID_INVALID`), roda
  `force-unlock -force <id>` e registra `iac.state_unlocked`.
- **Escrita errada no remoto**: recuperar uma versão anterior do objeto no bucket é feito pela pessoa no console ou na
  CLI do provedor (o bootstrap liga o versionamento para isso).
- **Mudança de backend** (`init -migrate-state`): fora do Spock por enquanto.

## 7. Estratégias

| Estratégia | Quando | Trava | Versões |
| --- | --- | --- | --- |
| S3 (modelo) | AWS | `use_lockfile = true` (Terraform 1.11, OpenTofu 1.10) | Versionamento do bucket |
| Azure Storage (`azurerm`) | Azure | Lease do blob | Versionamento do blob |
| Google Cloud Storage (`gcs`) | Google | Nativa | Versionamento do bucket |
| Local no projeto | Bootstrap, ou uma pessoa só | Só local | `.backup` a cada devolução |
