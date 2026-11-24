# Ciclo de execução do Terraform

Como uma mudança de infraestrutura anda no Spock, da necessidade à verificação, e onde a pessoa decide. As oito etapas
estão implementadas: dependência (entrega 1), validação e plano (2), aprovação e aplicação (3), state (4),
arquitetura e configuração (5) e, em volta do ciclo, drift e custo estimado (6), custo de um plano (8) e drift agendado com o que está na conta fora do state (9). Formato dos arquivos: [TERRAFORM_PROJECT_LAYOUT.md](TERRAFORM_PROJECT_LAYOUT.md).

## 1. As oito etapas

| # | Etapa | O que o Spock faz | Toca a nuvem? | Quem decide |
| --- | --- | --- | --- | --- |
| 1 | Descoberta | Lê conta, região, rede e serviços pelas conexões cloud | Só leitura | A pessoa liberou a leitura no vínculo |
| 2 | Arquitetura | A IA (opcional) propõe componentes e tamanhos, com justificativa, riscos, alternativas e premissas; custo é a lista do que é cobrado, sem valor | Não | A pessoa confere e completa o desenho |
| 3 | Configuração | Gera ou altera `infra/` no modelo, como mudança revisada (diff) | Não | A pessoa aprova o diff antes de gravar |
| 4 | Validação | `fmt -check -recursive` e `validate` em cada pasta com `.tf` | Não (`init -backend=false`) | Não se aplica |
| 5 | Plano | `init` e `plan -out` no workspace do ambiente | Só leitura (refresh) | Não se aplica |
| 6 | Aprovação | Mostra criar/mudar/destruir e pede aprovação **daquele plano** | Não | A pessoa, com confirmação extra se houver destruição |
| 7 | Aplicação | `apply <plano aprovado>` | **Sim** | Só depois da etapa 6 |
| 8 | Verificação | Novo `plan -detailed-exitcode` (esperado "sem mudanças"), outputs, leitura dos recursos | Só leitura | Não se aplica |

Gerar arquivos não autoriza executá-los; gerar plano não autoriza aplicá-lo; conectar conta não autoriza criar recurso.

## 2. Execução (entrega 2, implementada)

Validar e planejar passam pela camada Spock Infrastructure Management (`IacOperationService`) e chegam à ferramenta pela
IaC Runtime Abstraction (`IIacRuntime`), com o adaptador `CliIacRuntime`, o mesmo para `tofu` e `terraform`.

**Políticas, antes de qualquer processo:**

- projeto autorizado no Spock;
- ferramenta do projeto resolvida por `IacRuntimeService.ResolveForUseAsync` (OpenTofu por padrão, hash conferido,
  ferramenta e versão fixadas no primeiro uso); sem ferramenta pronta, nada é instalado nem executado;
- pasta do plano entre as pastas com `.tf`/`.tofu` descobertas (nada de `..` nem caminho livre);
- ambiente (workspace) com `^[a-z0-9][a-z0-9_-]{0,31}$`; o Spock só seleciona workspace existente, nunca cria;
- conexão cloud, se escolhida, vinculada ao projeto; dela vão para o processo só nomes (`AWS_PROFILE`, `AWS_REGION`,
  `ARM_SUBSCRIPTION_ID`, `ARM_TENANT_ID`, `GOOGLE_CLOUD_PROJECT`, `GOOGLE_APPLICATION_CREDENTIALS` como caminho);
- uma operação de IaC por vez em cada projeto (`IAC_OPERATION_RUNNING`).

**Cópia da configuração.** A raiz de IaC (a pasta comum a todas as que têm `.tf`, no modelo `infra/`) é copiada para
`<dados>/projects/<id>/iac/operations/<op>/config/`, sem `.terraform/`, planos e logs. O projeto nunca é tocado: nem
`.terraform/`, nem `.terraform.lock.hcl`, nem state, nem workspace selecionado. O hash da cópia (`configHash`, sobre
caminhos e conteúdo, sem state) fica na operação para a aprovação da entrega 3 amarrar plano e configuração.

**Validar** (`iac.validate`, `iac_validate`): `fmt -check -recursive -list=true` na cópia e, em cada pasta,
`init -backend=false` e `validate -json`. Resultado por pasta (válida, formatada, diagnósticos com arquivo e linha). A
pasta da operação é apagada no fim: validar não deixa nada além do registro.

**Planejar** (`iac.plan`): na pasta escolhida da cópia, `init` (com backend), `workspace select` (se pedido),
`plan -input=false -detailed-exitcode -out=<op>/plan.tfplan` e `show -json` do plano. O state local, se existir, é
copiado só para o plano ler e apagado da cópia logo depois; o plano não grava state. Ficam a cópia da configuração e o
arquivo de plano (que contém valores em texto puro: nunca sai pelo protocolo, pelo MCP nem pela trilha).

Variáveis de todo processo: `CHECKPOINT_DISABLE=1`, `TF_IN_AUTOMATION=1`, `TF_INPUT=0`, `TF_DATA_DIR` da operação e
`TF_PLUGIN_CACHE_DIR` por ferramenta (`<dados>/iac/plugin-cache/<ferramenta>`), para não baixar os providers a cada vez.

**Histórico e trilha.** `<dados>/projects/<id>/iac/operations.json` guarda as 50 operações mais recentes (a que sai leva
a pasta e o plano junto). A trilha recebe `iac.validate` (pastas, inválidas, fora do formato) e `iac.plan` (pasta,
ambiente, contagens, achados, `planId`, se havia state local), com ids e contagens, nunca valores.

Diferença do desenho anterior: as operações de IaC têm histórico próprio (tela Infraestrutura, `iac_history`, trilha e
Auditoria do projeto) e não aparecem na tela Execuções, que é dos workflows de engenharia.

## 3. Aprovação vinculada ao plano (entrega 3, implementada)

O plano fica em `<dados>/projects/<id>/iac/operations/<op>/plan.tfplan`, com a cópia da configuração ao lado. O
resumo vem de `show -json` (`IacPlanReader`); valores marcados como sensíveis e valores de atributo nunca vão para tela,
trilha ou agente.

Na hora de planejar, o adaptador lê o backend que o `init` configurou (só o tipo e os caminhos). Backend remoto ou
`local` com caminho absoluto: a ferramenta grava onde o state vive. State local dentro da pasta (sem bloco `backend`, ou
`local` com caminho relativo): desde a entrega 4 o Spock aplica na cópia e devolve o state ao projeto, conferindo
conflito ([TERRAFORM_STATE_MANAGEMENT.md](TERRAFORM_STATE_MANAGEMENT.md), seção 4). Continuam bloqueados, com o motivo
em `applyBlocker`, só os casos em que o Spock não sabe onde a ferramenta gravaria.

`iac.apply` leva o `planId` que a pessoa viu e as confirmações que ela marcou. `IacOperationService.ApplyAsync` recusa,
nesta ordem e sem rodar nada:

| Regra | Erro |
| --- | --- |
| Operação não é um plano bem-sucedido | `IAC_NOT_A_PLAN` |
| `planId` diferente do plano mostrado | `IAC_PLAN_MISMATCH` |
| Plano já aplicado (vale uma vez) | `IAC_PLAN_ALREADY_APPLIED` |
| Plano sem mudanças | `IAC_PLAN_NO_CHANGES` |
| Caso de state local que o Spock não sabe devolver | `IAC_APPLY_BLOCKED` |
| State local mudou desde o plano | `IAC_STATE_CHANGED` |
| Há plano mais novo da mesma pasta e ambiente | `IAC_PLAN_SUPERSEDED` |
| Plano com mais de 24 horas | `IAC_PLAN_EXPIRED` |
| Destrói ou substitui recurso sem `destroyConfirmed` | `IAC_DESTROY_CONFIRMATION_REQUIRED` |
| Achado grave (fora destruição) sem `risksAccepted` | `IAC_RISKS_CONFIRMATION_REQUIRED` |
| Ambiente `prd`, `prod`, `production` ou `producao` sem o nome digitado | `IAC_WORKSPACE_CONFIRMATION_REQUIRED` |
| Arquivo do plano ou cópia da configuração alterados (hash) | `IAC_PLAN_CHANGED` |
| Configuração do projeto mudou desde o plano (`configHash`) | `IAC_PLAN_STALE` |
| Ferramenta ou versão do projeto mudou desde o plano | `IAC_RUNTIME_CHANGED` |
| Conexão cloud do plano não está mais vinculada | `IAC_CONNECTION_NOT_LINKED` |

O `configHash` cobre caminhos e conteúdo da configuração, sem state, `.terraform/`, planos, logs e o
`.terraform.lock.hcl` (o `init` pode reescrevê-lo na cópia; o arquivo de plano já registra os providers).

A aprovação vira o registro `iac.apply_approved` (plano, `configHash`, ambiente, quantos destruídos e se confirmou,
quantos achados graves e se aceitou) e fica na operação de aplicação (`approval`).

Na tela, o plano mais recente de cada pasta e ambiente mostra "Aprovar e aplicar este plano": a lista do que será
destruído com a caixa de confirmação, a caixa de achados graves, o campo do nome do ambiente de produção e, no fim, uma
confirmação que diz que a nuvem vai mudar. Só a pessoa aprova; não existe ferramenta MCP de apply.

## 4. Aplicação e prova do resultado (entrega 3, implementada)

`CliIacRuntime.ApplyAsync`, na cópia guardada do plano, com um `TF_DATA_DIR` novo:

1. `init` e `workspace select`; confere de novo o backend.
2. `apply -input=false <plan.tfplan>`: só o arquivo aprovado, nunca `-auto-approve` sem plano. A saída vai para
   `apply.log` na pasta da operação (evidência local; não sai pelo protocolo).
3. **Verificação**: `plan -detailed-exitcode` sobre o mesmo state. Sem mudanças, a aplicação é "aplicado e
   verificado". Com mudanças, o resumo delas fica em `apply.remaining`: o que faltou numa falha parcial ou a
   divergência depois de um apply que terminou.
4. `state list` (só a contagem) e `output -json` (só os outputs não sensíveis, cada um resumido em até 300 caracteres).

Uma aplicação só conta como concluída com exit 0 **e** o plano seguinte limpo; o processo ter começado não basta. O
plano aplicado é marcado (`appliedBy`) e a pasta dele é apagada, porque o arquivo guardava valores em texto puro. A
trilha recebe `iac.apply` (aplicado, verificado, pendências, recursos no state), e a Auditoria do projeto mostra
"Plano X aplicado na nuvem" com o resultado da verificação.

Timeouts: apply até 55 minutos no motor; o host espera 90 minutos por `iac.apply`.

## 5. Arquitetura e configuração (entrega 5, implementada)

O caminho é necessidade → desenho → checagens → arquivos no modelo → diff aprovado → execução reversível. Nenhum HCL sai
do modelo de linguagem: a IA preenche um **desenho** (`IacBlueprint`), e modelos fixos (`IacAwsGenerator`) escrevem os
arquivos já no formato do `fmt`.

**Desenho.** Nuvem `aws`; nome do projeto (até 18 caracteres, por causa do limite de 32 do nome do load balancer);
região, conta e perfil; a rede **existente** (VPC, subnets privadas e públicas: o Terraform não cria rede); e os
componentes do modelo de referência: um serviço em contêiner (ECS Fargate atrás de load balancer só HTTPS, com
certificado ACM), um banco RDS (postgres ou mysql) e filas SQS (cada uma com fila de mensagens mortas). Alarmes e tópico
SNS entram sempre. Bucket de state opcional: sem ele, a raiz nasce sem backend remoto e a nota manda usar o bootstrap.

**IA, opcional** (`iac.architect.draft`, finalidade Fluxos de engenharia). A necessidade vai delimitada como conteúdo.
Do projeto, o modelo recebe só nome, região e quantas subnets existem, nunca ids. Na resposta, conta, perfil, rede,
certificado, e-mail e bucket são **sobrepostos** pelos fatos informados pela pessoa: o que o modelo escrever ali é
descartado. As checagens rodam na resposta; se falhar o que é dele (nome, tamanhos, portas, filas), o Spock pede uma
segunda resposta com o que falhou; se faltar um fato da conta, não pede de novo e a tela mostra o que completar. A
trilha recebe `iac.architecture_drafted` com tentativas e tokens, nunca o texto. Sem provedor de IA configurado, o
desenho é montado à mão na mesma tela ou proposto pelo agente da sessão.

**Agente da sessão, sem chave** (modo Sessão, MM-2). O agente que a pessoa já usa (Claude Code ou outro com o MCP do
Spock) chama `iac_architecture_prepare`, que devolve as regras e o formato do desenho da nuvem escolhida (AWS com as mesmas
regras do prompt do motor; Azure e Google com os componentes da geração), confere com `iac_blueprint_check` e grava a
proposta em `.spock/iac-proposal.json` (`.mariliz/` num projeto configurado antes da troca de nome), no formato da resposta
do modelo. O motor não chama modelo: `iac.architect.proposal` lê o arquivo e roda as mesmas checagens. Na tela, "Usar este
desenho" leva o desenho para o formulário, inclusive a nuvem e os fatos da conta que o agente trouxe (o que vier vazio fica
como a pessoa preencheu), e daí segue a mesma revisão do diff. Gerar os `.tf` continua sendo da pessoa.

**Checagens** (`IacBlueprintChecks`, as mesmas para IA, tela e MCP): formatos de região, conta, perfil, VPC e subnets;
combinação de CPU e memória do Fargate; porta, health check e 1 a 10 tarefas; certificado ACM da mesma região; duas
subnets públicas para o load balancer e duas privadas para o banco; motor, versão, classe e 20 a 1000 GB do banco; até 8
filas sem repetir; e-mail; bucket. Rede só é exigida com serviço ou banco.

**Opções por nuvem** (`iac.generate.options`): o que a tela oferece em cada nuvem (tamanhos de CPU e memória, motores e
faixa de armazenamento do banco, classe padrão, exemplo de região, certificado próprio e IP público) e onde há proposta
com IA e estimativa de custo (só AWS). As checagens leem desta mesma tabela os motores e o armazenamento, e um teste
confere que todo tamanho e padrão anunciado passa nelas; a tela guarda só os nomes dos campos e o mapeamento do
formulário para o desenho.

**Arquivos** (`iac.generate.plan`), na pasta de IaC que o projeto já tem (sem o `bootstrap`) ou em `infra/`: raiz com
`providers.tf` (versão mínima 1.11, provider fixado, `allowed_account_ids`, backend S3 com `use_lockfile`),
`variables.tf` (com `validation`), `resource.tf` (`environment_guard`, `name_prefix` e tags, um bloco por módulo),
`output.tf`, `terraform.tfvars.example` e `README.md` (pré-requisitos, plan e apply, primeiro deploy, verificações,
operação); e módulos `sns`, `cloudwatch`, `sqs`, `ecr`, `secrets`, `iam`, `alb`, `ecs` e `rds`. Senhas nunca passam
pelo Terraform: o segredo da aplicação é criado sem valor e a senha do banco é gerada e guardada pelo próprio RDS
(`manage_master_user_password`). Banco privado, criptografado, com proteção contra exclusão e snapshot final; ECR com
tag imutável; load balancer só HTTPS com TLS 1.3; tarefas que só aceitam o load balancer; o CI é o dono da revisão da
task definition (`ignore_changes`). O `.gitignore` da raiz ganha as linhas de state, planos e `terraform.tfvars`.

O plano devolve o diff de cada arquivo, as checagens, **o que é cobrado** (a lista, sem valor; o valor vem da estimativa
da seção 6) e notas (arquivo da pasta que não é do modelo, NAT para tarefas em subnet privada, ordem do primeiro deploy).
Desenho com checagem falhando volta sem arquivo e sem `planId`.

**Gravar** (`iac.generate.save`) exige o `planId` revisado (`STALE_PLAN` se o desenho ou os arquivos mudaram) e roda
como execução `infrastructure` reversível, no escopo de escrita de infraestrutura (`.tf`, `.tofu`, `.tfvars.example`,
o `README.md` de uma pasta e o `.gitignore` da raiz). A trilha recebe `iac.generated`. Os arquivos gerados passam no
`fmt -check` e no `validate` do OpenTofu com o provider AWS de verdade (teste ao vivo com `MARILIZ_LIVE_IAC_PROVIDERS`).

**Azure e Google Cloud (entrega 10).** O mesmo desenho (`provider` `azure` ou `gcp`) gera o mesmo modelo de pasta com
os serviços equivalentes, por modelos fixos (`IacAzureGenerator`, `IacGoogleGenerator`, escolhidos por `IacGenerators`):

| Componente | AWS | Azure | Google Cloud |
| --- | --- | --- | --- |
| Serviço em contêiner | ECS Fargate atrás de ALB só HTTPS (certificado ACM) | Container App com HTTPS gerenciado e identidade gerenciada | Cloud Run com HTTPS gerenciado e conta de serviço própria |
| Imagens | ECR | Azure Container Registry sem usuário administrador | Artifact Registry com tags imutáveis |
| Segredo da aplicação | Secrets Manager sem valor | Key Vault com RBAC; segredo gravado fora | Secret Manager sem versão |
| Banco | RDS com senha guardada pelo RDS | PostgreSQL Flexible Server só com login do Entra ID, na subnet delegada | Cloud SQL só com IP privado e login IAM |
| Filas | SQS com fila de mensagens mortas | Service Bus sem chave local, 5 entregas antes da fila de mensagens mortas | Pub/Sub com tópico de mensagens mortas |
| Alarmes | CloudWatch e SNS | Azure Monitor e grupo de ação | Cloud Monitoring e canal de e-mail |
| Ambiente | workspace e prefixo de nome | workspace e um grupo de recursos por ambiente | workspace; a raiz liga as APIs usadas |
| State | S3 com `use_lockfile` | Blob Storage com login do Entra ID | GCS com trava nativa |

No desenho, `account` é a subscription do Azure (obrigatória, trava o provider) ou o projeto do Google, e `profile` é o
tenant do Azure. As checagens seguem cada nuvem: memória o dobro da CPU no Container Apps, 1, 2 ou 4 vCPU no Cloud Run,
SKU e tamanhos de armazenamento do Flexible Server, tier do Cloud SQL, rede só quando há banco (subnet delegada e zona
DNS privada no Azure; rede com acesso privado a serviços no Google). Nos dois, o HTTPS usa o domínio gerenciado: o campo
de certificado tem de ficar vazio. A proposta com IA, a leitura de rede pela conexão e a estimativa de custo continuam
só na AWS. Os arquivos gerados passam no `fmt -check` e no `validate` do OpenTofu com os providers azurerm e google de
verdade, inclusive o Cloud SQL com MySQL.

**Descoberta da rede.** A conexão AWS ganhou as leituras `aws.ec2.vpcs` e `aws.ec2.subnets` (`ec2:DescribeVpcs`,
`ec2:DescribeSubnets`, `ec2:DescribeRouteTables`). Pública é a subnet cuja tabela de rotas manda `0.0.0.0/0` para um
internet gateway. A tela lê pela conexão vinculada e a pessoa escolhe a VPC; as subnets entram no desenho já separadas.

## 6. Drift e custo estimado (entrega 6, implementada)

**Drift** (`iac.drift`, botão "Verificar drift" ao lado de validar e planejar). É um `plan -refresh-only` sobre a cópia da
configuração, com a mesma pasta, o mesmo ambiente e a mesma conexão do plano: compara o state com a nuvem sem propor
mudança e sem gravar o state. Do `show -json` sai o `resource_drift`, lido por `IacPlanReader.ReadDrift`: endereço, tipo,
se o recurso foi **alterado** ou **apagado** por fora e os **nomes** dos atributos de nível de cima que mudaram (até 20).
Os valores, de antes e de agora, ficam no arquivo de plano, apagado com a cópia no fim da operação. A operação entra no
histórico (`drift`), com a trava do state quando a ferramenta recusa por lock, e a trilha recebe `iac.drift` com as
contagens. Aceitar o drift no state (`apply -refresh-only`) fica fora do Spock: ou o código passa a declarar a mudança,
ou um plano normal, aprovado, a desfaz. A mesma checagem do plano vale aqui: dois projetos não leem o mesmo state.

**Custo estimado** (`iac.cost.estimate`, botão na revisão da infraestrutura gerada). Os preços vêm da **lista pública da
AWS** (Price List Query API, `pricing:GetProducts`) pela conexão AWS vinculada ao projeto, com a nova capacidade
`aws.pricing` liberada no vínculo; nada da conta é lido. Cada item do desenho vira uma consulta com atributos exatos
(serviço, região, tipo de uso sem o prefixo da região, unidade). Dois produtos com preços diferentes para a mesma
consulta deixam o item **sem valor**, e a estimativa se diz incompleta: o Spock não escolhe um número.

| Fixo no mês (entra no total) | Por uso (só o preço da unidade) |
| --- | --- |
| Load balancer por hora; Fargate por vCPU-hora e GB-hora das tarefas; segredos do Secrets Manager; instância e armazenamento gp3 do RDS; alarmes do CloudWatch | LCU do load balancer; armazenamento do ECR; ingestão de logs; requisições do SQS |

Premissas mostradas junto: dólar, preços sob demanda da região, mês de 730 horas, sem impostos, câmbio, free tier,
Savings Plans ou descontos; transferência de dados e backup além do tamanho do banco fora; NAT Gateway ou endpoints VPC
fora quando as tarefas não têm IP público. A consulta fica na trilha como leitura cloud de `aws.pricing`. O RDS gerado
declara `storage_type = "gp3"`, o mesmo que a estimativa precifica.

Validado contra a API real (`AwsPricingLiveTests`, com `MARILIZ_LIVE_AWS_PROFILE`): cada item do desenho completo em
sa-east-1, também com Multi-AZ e MySQL, acha preço único, e a leitura `aws.pricing` da verificação de permissões responde.

### Fora do state e drift agendado (entrega 9, implementada)

**Fora do state.** Quando a verificação de drift usa uma conexão AWS vinculada, o Spock compara o state com a conta. O
plano `-refresh-only` já traz o state que leu (`prior_state`); dele saem só os identificadores dos tipos da tabela
abaixo (`IacUnmanaged.StateIds`), em todos os módulos, e nenhum outro atributo. Para cada tipo que o state gerencia, e só
se o vínculo liberou a leitura, a conexão lista a conta, e o que não está no state aparece como "fora do state", com id
e nome. Tipo que o state não gerencia não é lido: listar a conta inteira mostraria recursos de outros times. Leitura não
liberada, que falhou ou passou de 200 itens vira nota no resultado.

| Tipo no state | Identificador | Leitura da conexão | Não conta |
| --- | --- | --- | --- |
| `aws_instance` | `id` | `aws.ec2.instances` | instâncias encerradas |
| `aws_vpc` | `id` | `aws.ec2.vpcs` | a VPC padrão |
| `aws_subnet` | `id` | `aws.ec2.subnets` | |
| `aws_db_instance` | `identifier` | `aws.rds.instances` | |
| `aws_ecr_repository` | `name` | `aws.ecr.repositories` | |
| `aws_ecs_cluster` | `name` | `aws.ecs.clusters` | |
| `aws_lambda_function` | `function_name` | `aws.lambda.functions` | |
| `aws_cloudwatch_metric_alarm` | `alarm_name` | `aws.cloudwatch.alarms` | |
| `aws_s3_bucket` | `bucket` | `aws.s3.buckets` | buckets de outra região |

Recurso fora do state não é erro: ou ele passa a ser declarado no código (import), ou é de outro dono. O Spock não
importa nada.

**Drift agendado.** Por projeto, a pessoa liga a verificação automática (`iac.drift.schedule.set`) a cada 6 h, 12 h,
24 h ou 7 dias, com a pasta, o ambiente e a conexão escolhidos na tela, conferidos na hora de ligar como o botão
conferiria. O agendamento fica em `iac/drift-schedules.json` na pasta do Spock. Um laço do motor (`IacDriftScheduler`,
uma volta por minuto) roda a verificação vencida enquanto o app está aberto: a primeira logo depois de ligar, as
seguintes quando o intervalo vence. Projeto ocupado com outra operação fica para a volta seguinte; outra falha conta como
rodada, fica no agendamento (`lastError`) e na trilha (`iac.drift_schedule_failed`). A verificação é a mesma do botão,
entra no histórico pedida por `schedule`, e a tela mostra um alerta quando a mais recente achou drift ou recurso fora do
state. Agentes não ligam nem desligam o agendamento.

Validado de ponta a ponta (`Drift_aponta_subnets_da_conta_fora_do_state`): com uma subnet real importada para um state
local (o import só grava o state), o drift pela conexão aponta as outras subnets da região como fora do state.

### Custo de um plano (entrega 8, implementada)

Qualquer plano gerado no Spock, não só o da geração, ganha a estimativa do que ele muda (`iac.cost.plan`, botão "Estimar
o custo do plano" no resultado do plano). Ao gerar o plano, `IacPlanBillables` tira do `show -json` os recursos cobrados
que mudam e **só os atributos que definem preço**, de antes e de depois, e o plano os guarda (`billables`). Nome, senha,
tag e qualquer outro valor ficam no arquivo de plano.

| Tipo | Atributos de preço | Cotado como |
| --- | --- | --- |
| `aws_instance` | `instance_type` | EC2 Linux compartilhado por hora |
| `aws_nat_gateway` | `connectivity_type` | NAT Gateway por hora |
| `aws_lb` | `load_balancer_type` | load balancer application ou network por hora |
| `aws_db_instance` | `instance_class`, `engine`, `multi_az`, `allocated_storage`, `storage_type` | instância por hora e armazenamento gp3 ou gp2 por GB-mês (postgres, mysql, mariadb) |
| `aws_ecs_service` | `desired_count`, `launch_type` e `cpu`/`memory` da task definition referenciada | Fargate por vCPU-hora e GB-hora |
| `aws_secretsmanager_secret` | | segredo por mês |
| `aws_cloudwatch_metric_alarm` | | alarme de resolução padrão por mês |

A task definition de um serviço ECS é achada pela referência na configuração do plano, no mesmo módulo. Mudança que não
toca atributo de preço (uma tag) não entra. A resposta traz, por recurso, o custo mensal de antes e de depois, o total
de antes, de depois e a diferença, e os tipos que o plano muda e a estimativa não cota (sem custo fixo, como IAM, ou por
uso). Valor que só se conhece depois do apply deixa aquele lado sem valor, com o motivo, e a estimativa se diz
incompleta. `multi_az` não declarado é cotado como Single-AZ, o padrão da AWS, e a premissa diz isso. Plano gerado antes
desta entrega não tem os atributos e pede um plano novo (`IAC_PLAN_COST_UNAVAILABLE`).

Validado de ponta a ponta (`Custo_de_um_plano_aws_real_com_precos_reais`): o OpenTofu planeja EC2, RDS, serviço Fargate
e segredo com o provider AWS real pela conexão (nada é aplicado) e o Spock cota o plano com a Price List Query API real.

## 7. O que já existe para isso

| Necessidade | Já existe |
| --- | --- |
| Binário certo e íntegro por projeto | `IacRuntimeService.ResolveForUseAsync` (entrega 1) |
| Processo sem shell, com timeout e árvore encerrada | `SystemProcessRunner` (agora com variáveis de ambiente) |
| Execução com diário e eventos | `RunTracker`, `FileRunStore`, eventos `run.*` |
| Aprovação amarrada a hash | `ApprovalBinding` (workflows), padrão `plan`/`fix` com `STALE_PLAN` (segurança) |
| Confirmação de destruição | `ConfirmAction` na interface |
| Leitura cloud para descoberta e verificação | `CloudReadService` |
| Trilha | `IAuditLog`, Auditoria do projeto |

## 8. Ferramentas para agentes (MCP)

| Ferramenta | Entrega | Efeito |
| --- | --- | --- |
| `iac_status` | 1 | Lê disponibilidade, ferramenta e versão do projeto e exigências |
| `iac_validate` | 2 | Roda `fmt -check` e `validate` sobre a cópia; não lê backend nem state, não toca a nuvem |
| `iac_plan_summary` | 2 e 3 | Lê o último plano (ou um pelo id): contagens, endereços, achados, se já foi aplicado ou por que não pode ser |
| `iac_history` | 3 | Lista validações, planos e aplicações, com quem pediu e o resultado da verificação |
| `iac_state` | 4 | Onde vive o state de cada raiz, trava, criptografia, riscos e backend recomendado (só a configuração) |
| `iac_blueprint_check` | 5 | Confere um desenho com as checagens da tela, separa o que o agente ajusta dos fatos da conta que a pessoa informa, e lista os arquivos que a geração escreveria e o que é cobrado; não grava nada |
| `iac_architecture_prepare` | MM-2 | Roteiro do modo Sessão: regras e formato do desenho da nuvem, onde gravar a proposta e se já existe uma; não grava nada |
| `iac_plan_summary` (custo) | 8 | O resumo do plano lista os recursos com custo fixo que ele muda (tipo e tamanho); o valor em dólares fica na tela |
| `iac_drift_summary` | 6 e 9 | Lê a última verificação de drift: recursos alterados ou apagados por fora, com os nomes dos atributos, o que está na conta fora do state e se veio do agendamento; não roda a verificação |
| `iac_plan` | Depois | Gerar plano por agente pede o vínculo cloud liberado para agentes; hoje só a pessoa planeja |

Não haverá `terraform_apply` para agentes: o MCP nunca contorna a aprovação humana.
