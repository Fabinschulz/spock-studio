# Modelo de projeto Terraform

O formato que o Spock segue sempre que cria, lê ou revisa Terraform. É a referência única: as outras páginas de
infraestrutura ([INFRASTRUCTURE_MANAGEMENT.md](INFRASTRUCTURE_MANAGEMENT.md),
[TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md),
[TERRAFORM_STATE_MANAGEMENT.md](TERRAFORM_STATE_MANAGEMENT.md)) apontam para cá em vez de repetir as regras.

**De onde veio.** O dono escolheu como modelo a pasta `infra/` de um projeto interno de integração em .NET que roda no
ECS Fargate com filas SQS, load balancer e alarmes (repositório privado da organização). Esta página registra a
estrutura e as convenções dele para que ninguém precise consultar aquele repositório. O que era próprio daquele
projeto (número da conta, nome do bucket, perfil de SSO, IDs do GitHub, e-mails, chamados) virou marcador:

| Marcador | O que é |
| --- | --- |
| `<projeto>` | Nome curto do sistema, em minúsculas com hífen (no modelo, o nome do conector) |
| `<org>` | Prefixo da organização no nome do bucket de state |
| `<conta>` | Conta AWS de 12 dígitos aceita por esta raiz |
| `<regiao>` | Região padrão (no modelo, `sa-east-1`) |
| `<perfil>` | Perfil SSO da AWS CLI (IAM Identity Center) usado pelo backend e pelo provider |
| `<env>` | Ambiente: o workspace selecionado (`dev`, `hml`, `prd`) |

**OpenTofu ou Terraform.** O modelo vale igual para as duas ferramentas: mesma árvore, mesmos arquivos `.tf`, mesmos
providers e mesmos comandos (`tofu` no lugar de `terraform`). O Spock usa o OpenTofu por padrão, por ser gratuito e de
código aberto ([TERRAFORM_DEPENDENCY_MANAGEMENT.md](TERRAFORM_DEPENDENCY_MANAGEMENT.md)); o modelo de referência foi
escrito e aplicado com o Terraform. As diferenças estão na seção 4.

O pedido de escopo trazia uma árvore com `environments/development|staging|production`. Ela era ilustrativa; o modelo
do dono vence: **um diretório raiz, um workspace por ambiente**.

## 1. Árvore

```
infra/
├── README.md                    manual de operação (seção 11)
├── providers.tf                 bloco terraform {}, backend "s3" e provider
├── variables.tf                 entradas da raiz, cada uma com validação
├── resource.tf                  guarda do workspace, locals, um module por serviço, blocos moved
├── output.tf                    saídas que pessoas e o CI consomem
├── imports.tf                   opcional: adoção de recursos criados no console
├── terraform.tfvars.example     formato dos valores que não ficam no git (valores de mentira)
├── .terraform.lock.hcl          versionado
├── bootstrap/                   raiz separada que cria o bucket de state, com state local
│   ├── providers.tf
│   ├── variables.tf
│   ├── resource.tf
│   ├── output.tf
│   └── .terraform.lock.hcl
└── modules/
    └── <servico>/               um módulo por serviço da nuvem: sqs, sns, cloudwatch, ecr, secrets, iam, alb, ecs
        ├── resource.tf
        ├── variables.tf
        ├── outputs.tf           só quando o módulo exporta algo (cloudwatch não tem)
        └── .terraform.lock.hcl
```

Detalhes que o Spock reproduz como estão, porque fazem parte do modelo:

- Arquivo principal é `resource.tf`, nunca `main.tf`.
- Raiz e `bootstrap/` usam `output.tf` (singular); módulos usam `outputs.tf` (plural).
- Cada módulo é um serviço do provedor, não uma camada abstrata ("networking", "compute"). A rede existente não é
  criada: entra por variável.
- `terraform.tfstate`, `terraform.tfvars`, `.terraform/` e `*.tfplan` ficam no `.gitignore`.

## 2. Estilo dos arquivos

- Cada bloco tem um cabeçalho de seção: `# ------------------------- <Nome> -------------------------------`.
- Comentários em português dizem **por quê** (limite, armadilha, decisão), nunca o que a linha faz. Exemplos do
  modelo: "As chaves são endereços de recurso: renomear uma destrói a fila", "Recurso separado: a política inline
  criaria um ciclo".
- Toda variável tem `description` em português, `type` explícito e, quando o formato importa, `validation` com
  `regex` e `error_message` que mostra o formato certo (`vpc-0123456789abcdef0`, ARN do ACM na região certa, SHA de 40
  caracteres, ao menos duas subnets em zonas diferentes).
- Toda saída tem `description` dizendo quem a consome ("configure como a variável de repositório ... no GitHub").
- `terraform fmt` é a formatação; nada fora dela.

## 3. Ambientes

- **O workspace é o ambiente.** Não há pasta por ambiente.
- A raiz recusa o workspace `default`:

  ```hcl
  resource "terraform_data" "environment_guard" {
    input = terraform.workspace

    lifecycle {
      precondition {
        condition     = terraform.workspace != "default"
        error_message = "Selecione o workspace do ambiente antes do plan/apply (ex.: terraform workspace select dev)."
      }
    }
  }
  ```

- Nomes de recurso: `<projeto>-<env>-<recurso>`, montados em `locals.name_prefix` dentro de cada módulo.
- Limites de nome do provedor viram limite do nome do ambiente (no modelo, até 7 caracteres por causa dos 32 do nome do
  load balancer) e são escritos no README.
- Tags comuns num único `locals.tags` da raiz, repassado a todo módulo: `Project`, `Environment`
  (`terraform.workspace`), `ManagedBy = "terraform"`, `Repository`.
- Valor usado em dois lugares (ex.: namespace de métricas lido pelo alarme e escrito pela aplicação) nasce num
  `local` da raiz: uma só origem.
- Ambiente novo: `terraform workspace new <env>`.

## 4. Versões e provider

```hcl
terraform {
  required_version = ">= 1.11"            # lock nativo do S3 (use_lockfile)

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "6.66.0"                  # versão exata, não intervalo
    }
  }

  backend "s3" {
    bucket               = "<org>-terraform-state-<conta>-<regiao>"
    key                  = "terraform.tfstate"
    workspace_key_prefix = "<projeto>"    # state em <projeto>/<env>/terraform.tfstate
    region               = "<regiao>"
    profile              = "<perfil>"
    encrypt              = true
    use_lockfile         = true
  }
}

provider "aws" {
  region              = var.aws_region
  profile             = var.aws_profile
  allowed_account_ids = ["<conta>"]       # qualquer outra conta é recusada
}
```

- `required_version` é o piso que o Spock lê para escolher o binário do projeto
  ([TERRAFORM_DEPENDENCY_MANAGEMENT.md](TERRAFORM_DEPENDENCY_MANAGEMENT.md)).
- O provider é fixado em versão exata, e o `.terraform.lock.hcl` é versionado na raiz, no bootstrap **e em cada
  módulo**. Módulo novo: copie o lock de um módulo existente antes do `init`, porque módulos não fixam a versão.
- `aws_region` e `aws_profile` são variáveis com padrão; o perfil é de SSO, nunca chave de acesso.

**Com OpenTofu:**

- `required_version` é comparado com a versão do OpenTofu. O `>= 1.11` do modelo (lock nativo do S3) é atendido pelo
  OpenTofu 1.11 em diante; o `use_lockfile` do backend S3 existe no OpenTofu desde a 1.10.
- `hashicorp/aws` resolve em `registry.opentofu.org/hashicorp/aws` (o mesmo provider, publicado pelo registro do
  OpenTofu). O `.terraform.lock.hcl` passa a ter entradas desse registro: um lock gerado pelo Terraform não vale para o
  OpenTofu e vice-versa. Projeto novo nasce com o lock da ferramenta escolhida; trocar de ferramenta regenera os locks
  de todas as pastas (`tofu init -upgrade` em cada uma) e é decisão explícita.
- Um projeto usa uma ferramenta só (a fixada no Spock). A pasta pode ter arquivos `.tofu`, que só o OpenTofu lê; o
  Spock não os gera para não prender o projeto.
- Comandos da seção 11 com `tofu` no lugar de `terraform`; o README gerado diz qual ferramenta o projeto usa.

## 5. State

- `bootstrap/` cria o bucket de state uma vez por conta e guarda o próprio state **local** (no `.gitignore`). Se esse
  state se perder, o bucket é reimportado com um bloco `import`, nunca recriado.
- O bucket nasce com `prevent_destroy`, versionamento (recuperar de uma escrita errada), criptografia `AES256` e
  bloqueio total de acesso público.
- Backend S3 com `encrypt = true` e `use_lockfile = true` (sem tabela DynamoDB).
- Isolamento: uma chave por projeto e ambiente (`workspace_key_prefix`). Dois projetos nunca dividem chave.

Regras do Spock sobre state em [TERRAFORM_STATE_MANAGEMENT.md](TERRAFORM_STATE_MANAGEMENT.md).

## 6. Módulos

- Entradas comuns: `environment` (com `validation` `^[a-z0-9-]+$`) e `tags` (`map(string)`, padrão `{}`).
- Coleções são `locals` em forma de mapa e os recursos usam `for_each` sobre elas. **As chaves são endereços de
  recurso**: renomear uma destrói o recurso; renomeie só com bloco `moved`.
- Saídas são mapas indexados pela mesma chave (`queues["orders-inbound"].url`), para a raiz e os outros módulos
  consumirem sem índice numérico.
- Ordem de dependência explícita na raiz: um módulo recebe as saídas de outro (`module.sqs.queues` entra no `iam`).
  Quando dois módulos se referenciariam em ciclo, um deriva o ARN do outro pelo nome em vez de receber a saída.
- Um recurso que criaria ciclo dentro do módulo vira recurso separado (ex.: `aws_sqs_queue_redrive_allow_policy`).

## 7. O que fica fora do repositório

- `terraform.tfvars` (no `.gitignore`); o `terraform.tfvars.example` mostra o formato com valores de mentira e um
  comentário por bloco.
- Rede, banco e certificado que já existem entram por variável (IDs validados por regex); o Terraform não os cria.
- Segredos são criados **sem valor**. O valor é gravado fora do Terraform, a partir de um arquivo fora do repositório
  que é apagado depois. O state nunca recebe o segredo.
- A versão da imagem só vale no primeiro apply: o serviço ignora a revisão da task definition (`ignore_changes`)
  porque o CI é o dono dela.

## 8. Adoção e movimentação

- `imports.tf` adota recursos criados no console, com `import` + `for_each` ligado só no workspace onde eles existem
  (`terraform.workspace == "dev"`). O ID importado pode ser um ARN; o valor de segredo nunca passa por ali.
- Recurso que muda de módulo leva um bloco `moved` na raiz, com o comentário do motivo (no modelo: sem ele o alarme
  seria recriado e a assinatura de e-mail teria de ser confirmada de novo).

## 9. Segurança embutida no modelo

| Onde | Regra |
| --- | --- |
| Provider | `allowed_account_ids` em toda raiz |
| ECR | Tag imutável, scan no push, criptografia, ciclo de vida (guarda as N imagens mais recentes) |
| SQS | Criptografia gerenciada (`sqs_managed_sse_enabled`), fila de mensagens mortas por fluxo |
| IAM | Uma política por papel e por fila (produtor, consumidor), sem curinga de serviço; papel do CI por OIDC com o subject imutável (IDs numéricos do dono e do repositório), sem chave de acesso |
| ALB | Só HTTPS (443) com certificado ACM; health check explícito |
| ECS | Security group por task; entrada no banco por regra de security group, não por CIDR |
| Segredos | Criados sem valor; leitura só pelos papéis que precisam |
| Cotas | Limites do provedor que o desenho alcança (ex.: 10 políticas por papel) escritos no README |

## 10. Verificação

- Sem testes automatizados em `infra/`. As verificações estáticas não precisam de credencial:
  - `terraform fmt -check -recursive infra`
  - `terraform validate` em **toda pasta com `.tf`** (raiz, `bootstrap/` e cada módulo), depois de
    `terraform init -backend=false` num checkout novo.
- O CI descobre sozinho as pastas com `.tf` e roda as duas checagens a cada pull request que toca `infra/`.
- Uma mudança é provada assim: plan revisado antes do apply, um segundo `terraform plan -detailed-exitcode` que diga
  `No changes` depois do apply, e a leitura dos recursos de volta no provedor. A saída vira evidência guardada junto da
  especificação da mudança.

## 11. Fluxo de operação e README

Fluxo de um ambiente:

```bash
terraform -chdir=infra init
terraform -chdir=infra workspace select <env>
terraform -chdir=infra plan -out=<env>.tfplan
terraform -chdir=infra apply <env>.tfplan
terraform -chdir=infra plan -detailed-exitcode
```

`-target` só aparece quando quem aplica precisa de permissão diferente (ex.: um administrador aplica IAM antes, porque
o perfil do dia a dia não escreve IAM), e o README diz isso.

O `infra/README.md` do modelo tem estas seções, nesta ordem: tabela "Caminho → O que é" (um módulo por linha, com o
que ele cria), "Variáveis que você informa", "Pré-requisitos" (versão mínima, provider fixado, perfil SSO e o comando
de login por device code), "Bootstrap do bucket de estado", "Plan e apply de um ambiente", primeiro deploy passo a
passo com quem executa cada passo, "Verificações", e seções "Operando ..." com o que fazer quando cada alarme
dispara, como trocar segredo, voltar versão e renomear chave sem destruir recurso.

## 12. Como o Spock aplica o modelo

| Situação | O que o Spock faz |
| --- | --- |
| Gerar o bootstrap do state | `<raiz de IaC>/bootstrap/` na árvore, estilo e proteções da seção 5, para AWS, Azure ou Google ([TERRAFORM_STATE_MANAGEMENT.md](TERRAFORM_STATE_MANAGEMENT.md), seção 3) |
| Gerar Terraform para um projeto | Cria `infra/` (ou a raiz de IaC que o projeto já tem) com esta árvore, os cabeçalhos de seção, `validation` nas variáveis, guarda de workspace, `name_prefix` e tags em `locals`, um módulo por serviço (SNS, CloudWatch, SQS, ECR, Secrets Manager, IAM, ALB, ECS Fargate e RDS), `terraform.tfvars.example` e o README nas seções da seção 11, a partir de um desenho conferido e de modelos fixos, nunca de HCL escrito pela IA ([TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md), seção 5) |
| Ler um `infra/` existente | Descobre as pastas com `.tf`, lê `required_version` e `required_providers`, os workspaces e o backend; aponta o que diverge do modelo como sugestão, sem reescrever |
| Validar | `fmt -check -recursive` e `validate` por pasta, exatamente como a seção 10, com a ferramenta fixada no projeto (OpenTofu por padrão) |
| Planejar | Na raiz, no workspace escolhido, com `-out` guardado pelo Spock ([TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md)) |
| Provar | Segundo plan com `-detailed-exitcode` depois do apply, registrado como evidência da operação |

**Azure e Google Cloud.** O modelo é AWS. Para os outros provedores o Spock mantém a árvore, os workspaces, o estilo e
as verificações e troca só o que é do provedor: backend `azurerm` (Storage Account com versionamento e lease como
lock) ou `gcs` (bucket com versionamento; o lock é nativo), e a trava de conta vira `subscription_id` fixo no provider
`azurerm` ou `project` fixo no provider `google`. Essa adaptação é proposta do Spock, não parte do modelo do dono.
