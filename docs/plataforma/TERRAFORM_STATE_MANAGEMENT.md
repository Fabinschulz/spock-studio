# Gestão do state do Terraform

O state descreve tudo o que o Terraform gerencia e pode conter dados confidenciais (senhas geradas, chaves, endereços
internos). Esta página diz como o Spock ajuda a escolher, proteger e usar o state. Implementação na entrega 4
([INFRASTRUCTURE_MANAGEMENT.md](INFRASTRUCTURE_MANAGEMENT.md)); o formato vem do modelo
([TERRAFORM_PROJECT_LAYOUT.md](TERRAFORM_PROJECT_LAYOUT.md), seção 5).

## 1. Regras que não mudam

- O Spock **nunca** sobrescreve, move, apaga ou recria um state sem autorização explícita daquela operação.
- **Um state por projeto e ambiente.** Dois projetos ou dois ambientes nunca dividem chave.
- State é dado sensível: conteúdo não vai para logs, telas, trilha nem agentes. O Spock lê do state só endereços e
  tipos de recurso (`state list`, `show -json` filtrado), nunca atributos.
- O Spock não guarda cópia do state remoto. Backup é responsabilidade do backend (versionamento).
- Nenhum comando de cirurgia (`state rm`, `state mv`, `import`, `force-unlock`) roda sem um pedido explícito da pessoa,
  com o comando exato mostrado antes.

## 2. Estratégias

| Estratégia | Quando | Lock | Versionamento |
| --- | --- | --- | --- |
| **S3** (modelo de referência) | AWS; padrão do modelo | Nativo (`use_lockfile = true`, Terraform ≥ 1.11) | Versionamento do bucket |
| **Azure Storage** (`azurerm`) | Azure | Lease do blob, nativo | Versionamento do blob |
| **Google Cloud Storage** (`gcs`) | Google Cloud | Nativo | Versionamento do bucket |
| Local | Só para o `bootstrap/` que cria o bucket, ou experimento descartável | Arquivo local | Nenhum |

O Spock não presume backend: lê o bloco `backend` de `providers.tf`. Sem backend, a tela recomenda o remoto do provedor
da conexão vinculada e explica o porquê (lock, versionamento, time).

## 3. Bootstrap do bucket (como no modelo)

Uma raiz `infra/bootstrap/` com state local cria o bucket uma vez por conta:

- nome `<org>-terraform-state-<conta>-<regiao>`;
- `prevent_destroy`, versionamento, criptografia no servidor, bloqueio total de acesso público;
- o state local do bootstrap fica no `.gitignore`; se for perdido, o bucket é reimportado, nunca recriado.

O Spock gera essa raiz e roda o mesmo ciclo de plano e aprovação ([TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md)).

## 4. Isolamento

- Chave do state: `workspace_key_prefix = "<projeto>"`, então `<projeto>/<env>/terraform.tfstate`.
- Workspace `default` recusado pela guarda do modelo.
- `TF_DATA_DIR` do Spock separado por projeto e ambiente.
- Antes de cada `plan`, o Spock confere que o backend declarado e o workspace selecionado são os do ambiente pedido e
  que nenhum outro projeto do Spock usa a mesma combinação de bucket e prefixo; se usar, recusa.
- `allowed_account_ids` (ou `subscription_id`/`project` fixos) impede aplicar com a credencial da conta errada.

## 5. Lock, recuperação e backup

- Lock preso: o Spock mostra quem/quando (do erro do Terraform) e só oferece `force-unlock <id>` com confirmação.
- Escrita errada: recuperar uma versão anterior do objeto no bucket é feito pela pessoa no console ou CLI do provedor;
  o Spock mostra o caminho exato e não faz sozinho.
- Mudança de backend (`init -migrate-state`): operação própria, com plano de migração mostrado e confirmação.

## 6. Leitura do state pelo Spock

| Uso | Comando | O que sai |
| --- | --- | --- |
| Consulta de recursos gerenciados | `state list` | Endereços |
| Verificação pós-apply | `plan -detailed-exitcode` | Se há diferença |
| Drift | `plan -refresh-only -json` | Endereços e tipo de mudança, sem valores |
| Outputs | `output -json` | Valores não sensíveis |
