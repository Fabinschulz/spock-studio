# Gestão de infraestrutura (Infrastructure Management)

O Spock deixa de só consultar AWS, Azure e Google Cloud e passa a ajudar a **planejar, provisionar e evoluir**
infraestrutura como código na linguagem do Terraform, executada pelo **OpenTofu** (padrão: gratuito, MPL 2.0) ou pelo
Terraform da HashiCorp, sempre com a pessoa decidindo cada mudança. Esta página é o mapa: arquitetura,
responsabilidades, o que já existe e a ordem das entregas.

| Documento | Assunto |
| --- | --- |
| [TERRAFORM_PROJECT_LAYOUT.md](TERRAFORM_PROJECT_LAYOUT.md) | **Modelo de projeto Terraform** que o Spock gera, lê e valida |
| [TERRAFORM_DEPENDENCY_MANAGEMENT.md](TERRAFORM_DEPENDENCY_MANAGEMENT.md) | OpenTofu como padrão, detecção, instalação autorizada e verificada, versões |
| [TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md) | Validação, plano, aprovação, apply e verificação |
| [TERRAFORM_STATE_MANAGEMENT.md](TERRAFORM_STATE_MANAGEMENT.md) | Backends, lock, isolamento e proteção do state |
| [CLOUD_INFRASTRUCTURE_SECURITY.md](CLOUD_INFRASTRUCTURE_SECURITY.md) | Regras de segurança e governança |
| [CLOUD_CONNECTORS_ARCHITECTURE.md](CLOUD_CONNECTORS_ARCHITECTURE.md) | Conectores cloud (leitura), que a gestão de infraestrutura usa |

## 1. Pré-condição verificada

O pedido exigia não começar com pendências críticas ou altas abertas. Em 2026-11-15
O relatório de remediação de 2026-11-15 (fora do repositório, no histórico do git) fechava todo P0 e P1 como corrigido ou bloqueado por dependência externa
com dono (A3 OAuth do Jira, E1 Jira real, E2 modelo real). Nenhum deles toca infraestrutura.

## 2. Responsabilidades

| Mecanismo | Faz | Não faz |
| --- | --- | --- |
| **Conectores cloud** (`Application/Cloud`) | Autenticar pelos mecanismos oficiais, identificar conta/projeto, descobrir e ler recursos, verificar permissões | Criar, alterar ou apagar recurso |
| **OpenTofu / Terraform** (`Application/Iac`) | Declarar infraestrutura, validar, planejar, aplicar plano aprovado, consultar state, detectar drift | Autenticar por conta própria, inventariar a conta, guardar credencial |
| **Dependência** | Achar, oferecer (OpenTofu primeiro), instalar e verificar o binário; ferramenta e versão por projeto | Mudar o PATH, instalar sem autorização |

Os dois se completam e não se duplicam:

- O Terraform usa as **mesmas credenciais** que o conector já resolve onde o SDK as procura (perfil SSO da AWS,
  `az login`, Application Default Credentials). A configuração Terraform recebe nomes (perfil, subscription, projeto),
  nunca segredo; o Spock repassa ao processo só variáveis de nome (`AWS_PROFILE`, `ARM_SUBSCRIPTION_ID`,
  `GOOGLE_CLOUD_PROJECT`).
- O que o conector descobre (VPC, subnets, security groups, conta, região) vira **entrada** do Terraform: as variáveis
  de rede existente do modelo ([TERRAFORM_PROJECT_LAYOUT.md](TERRAFORM_PROJECT_LAYOUT.md), seção 7) e o
  `allowed_account_ids` vêm de leituras auditadas, não de digitação.
- Drift: o Terraform compara state e nuvem (`plan -refresh-only`); o conector mostra o que existe fora do state.

## 3. Arquitetura

O desenho do dono (2026-11-15), em quatro camadas:

```
Spock Infrastructure Management     interface, políticas, aprovação, auditoria e execução
        │
IaC Runtime Abstraction             contrato independente da ferramenta
        │
OpenTofu (padrão)  |  Terraform (opcional)
        │
Cloud Providers                     AWS · Azure · Google Cloud
```

Assim o Spock evolui sem ficar preso à licença nem ao ciclo de atualizações de uma única ferramenta.

| Camada | No código |
| --- | --- |
| Spock Infrastructure Management | Tela Infraestrutura (`desktop/ui/src/features/infrastructure`); `IacOperationService` (políticas, execução, histórico, trilha); `IacStateService` (state, riscos, bootstrap); `IacRuntimeService` (instalação, consentimento, ferramenta e versão por projeto); `IacRequestHandler` (`iac.*`, só interface); `IacMcpTools` (agentes) |
| IaC Runtime Abstraction | `IIacRuntime` (`FormatCheckAsync`, `ValidateAsync`, `PlanAsync`, `ApplyAsync`) e `IIacRuntimeFactory`; `IIacToolchain` para a distribuição de cada ferramenta |
| OpenTofu / Terraform | `CliIacRuntime` (o mesmo adaptador de linha de comando para `tofu` e `terraform`); `OpenTofuToolchain` e `HashiCorpTerraformToolchain` (download verificado) |
| Cloud Providers | Conectores cloud (`Application/Cloud`): a conexão vinculada ao projeto entrega ao plano só nomes (perfil, região, subscription, projeto) |

O Terraform é opcional e **liberado**: quem escolher pode usá-lo no projeto, sem trava. A licença BUSL 1.1 da HashiCorp
fica com o jurídico da empresa, que negocia à parte; o Spock só informa a licença na proposta.

- **Sem execução arbitrária.** Cada operação é um método com contrato. Agentes nunca recebem uma ferramenta "rode este
  comando"; o adaptador usa `IProcessRunner` sem shell, com argumentos fixos montados pelo Spock.
- **Mesma porta para interface e MCP.** Como em `CloudReadService`, a regra fica no serviço; o MCP não tem atalho.
- **"Plugin" de provedor**: como nos conectores, o Spock não carrega plugins dinâmicos. O suporte a um provedor é o
  conjunto backend + provider + checagens descrito no modelo; ver "Como adicionar um provedor".

## 4. Conceitos

| Conceito | Onde fica |
| --- | --- |
| Provedor, conexão, credencial | Conectores cloud (já existem) |
| Projeto de infraestrutura | Um projeto do Spock com pasta `infra/` no modelo; o vínculo cloud diz qual conexão ele usa |
| Ambiente | Workspace do Terraform (`dev`, `hml`, `prd`), como no modelo |
| Módulos, variáveis, outputs, providers | Arquivos do modelo; lidos e gerados, nunca guardados fora do repositório |
| Ferramenta e versão | OpenTofu ou Terraform, fixados por projeto (`iac/terraform.json`) |
| Backend de state | Declarado em `providers.tf`; regras em [TERRAFORM_STATE_MANAGEMENT.md](TERRAFORM_STATE_MANAGEMENT.md) |
| Operação | Validação, plano, apply: cada uma vira execução do Spock com evidência (entregas 2 e 3) |

## 5. Entregas

| # | Entrega | Situação |
| --- | --- | --- |
| 1 | Dependência: OpenTofu como padrão e Terraform como opção, detecção, compatibilidade, instalação autorizada e verificada, adoção de instalação existente, ferramenta e versão por projeto, atualização lado a lado, remoção, trilha, tela, `iac_status` no MCP | **Implementada**; validada no Windows com instalação real das duas ferramentas |
| 2 | Validação e plano sobre uma cópia da configuração: `fmt -check` e `validate` por pasta, `plan -out` por workspace com a conexão cloud do projeto, resumo (criar/alterar/substituir/destruir), achados de segurança, histórico, trilha, `iac_validate` e `iac_plan_summary` no MCP | **Implementada**; validada com o OpenTofu real ([TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md)) |
| 3 | Aprovação vinculada ao plano (id, hashes, prazo, plano mais recente, ferramenta), confirmações a mais para destruição, achados graves e produção, apply só do arquivo aprovado, verificação por plano seguinte limpo, state e outputs não sensíveis, `iac_history` no MCP | **Implementada**; validada com o OpenTofu real (aplicar, verificar, destruir com confirmação, bloqueio de state local) |
| 4 | State: onde vive o state de cada raiz e os riscos (state no git, sem trava, sem criptografia, dividido), backend recomendado pela nuvem do projeto, bootstrap do bucket no modelo para AWS, Azure e Google (diff revisado, execução reversível), state local devolvido ao projeto com conflito, isolamento entre projetos, consulta do state, force-unlock com confirmação, `iac_state` no MCP | **Implementada**; bootstraps validados com o OpenTofu e os providers reais, state local devolvido num apply real ([TERRAFORM_STATE_MANAGEMENT.md](TERRAFORM_STATE_MANAGEMENT.md)) |
| 5 | Geração: da necessidade ao desenho (IA opcional, fatos da conta pela conexão), checagens determinísticas, `.tf` da AWS no modelo por modelos fixos (raiz e módulos SNS, CloudWatch, SQS, ECR, Secrets Manager, IAM, ALB, ECS Fargate e RDS), o que é cobrado sem preço inventado, diff revisado e execução reversível; leitura de VPCs e subnets pela conexão; `iac_blueprint_check` no MCP | **Implementada**; arquivos gerados validados com `fmt` e `validate` do OpenTofu e o provider AWS real ([TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md), seção 5) |
| 6 | Drift e custos: `plan -refresh-only` sobre a cópia com recursos alterados ou apagados por fora (só nomes de atributo), histórico e trilha, `iac_drift_summary` no MCP; estimativa mensal do desenho com os preços públicos da AWS pela conexão (`aws.pricing`), itens por uso fora do total, premissas e item sem preço único deixado sem valor | **Implementada**; drift validado com o OpenTofu real (recurso apagado por fora apontado, state intocado); preços consultados de verdade na Price List Query API com uma identidade IAM (cada item do desenho com preço único, também com Multi-AZ e MySQL; `MARILIZ_LIVE_AWS_PROFILE`) ([TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md), seção 6) |
| 7 | Perfis do `aws login` na conexão AWS (`AWSSDK.Signin`); o provider AWS do modelo lê o mesmo perfil | **Implementada**; validada com um perfil real na conexão e num plano do OpenTofu |
| 8 | Custo de um plano: atributos de preço de cada recurso cobrado guardados com o plano (EC2, NAT Gateway, load balancer, RDS, ECS Fargate, Secrets Manager, alarmes), custo mensal de antes e de depois por recurso, diferença, o que fica de fora e o que só se conhece depois do apply | **Implementada**; validada de ponta a ponta com o OpenTofu, o provider AWS e a Price List Query API reais ([TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md), seção 6) |
| 9 | Drift agendado e fora do state: verificação automática por projeto (6 h a 7 dias, com o app aberto), alerta na tela, e recursos da conta fora do state para os tipos que o state gerencia, pelas leituras liberadas no vínculo | **Implementada**; fora do state validado com subnets reais de uma conta AWS ([TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md), seção 6) |
| 10 | Geração para Azure e Google Cloud no mesmo modelo de pasta: Container Apps, ACR, Key Vault, PostgreSQL Flexible Server só com Entra ID, Service Bus e Azure Monitor; Cloud Run, Artifact Registry, Secret Manager, Cloud SQL com IP privado e login IAM, Pub/Sub e Cloud Monitoring; checagens por nuvem; seletor de nuvem na tela | **Implementada**; arquivos gerados validados com `fmt` e `validate` do OpenTofu e os providers azurerm e google reais ([TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md), seção 5) |

Priorização dos recursos por provedor: o modelo de referência da AWS (SQS, SNS, CloudWatch, ECR, Secrets Manager, IAM,
ALB, ECS Fargate e o bucket de state) e os equivalentes no Azure e no Google, sempre sem senha no Terraform e com rede
existente recebida por id.

## 6. Experiência na tela Infraestrutura

- **Ciclo de uma mudança**: Descoberta → Arquitetura → Configuração → Validação → Plano → Aprovação → Aplicação →
  Verificação, cada etapa marcada como "Só analisa", "Prepara a mudança" ou "Muda a nuvem"; as oito estão disponíveis.
- **Gerar infraestrutura**: necessidade para a IA (opcional), conta, região e perfil pela conexão AWS vinculada, VPC e
  subnets lidas da conta, componentes, revisão do diff com o que é cobrado, estimativa de custo pela conexão e gravação
  no projeto.
- **Custo do plano**: no resultado de um plano com recursos cobrados, "Estimar o custo do plano" mostra antes, depois e
  a diferença por mês, por recurso, pela conexão AWS do projeto.
- **Validar, planejar e verificar drift**: o drift aparece no histórico como "Com drift", "Fora do state" ou "Sem
  drift", com cada recurso alterado ou apagado por fora e o que está na conta fora do state; um alerta mostra a
  verificação mais recente com achados.
- **Verificação automática de drift**: ligar, intervalo, próxima e última verificação, usando a pasta, o ambiente e as
  credenciais escolhidos.
- **State** e a aprovação do plano, como nas entregas 2 a 4.
- **Dependência Terraform**: estado, mensagem, consentimento, proposta verificável, instalações com origem e verificação.
- **Projeto**: o que os `.tf` exigem, a versão usada (automática ou fixada) e a escolha.
- **Provedores**: quantas conexões cloud existem e quantas estão conectadas, com o link para Conexões cloud.

Ainda não aparecem: lista de projetos de infraestrutura com ambientes.

## 7. Como adicionar um provedor

1. Conector cloud do provedor (ver [CLOUD_CONNECTORS_ARCHITECTURE.md](CLOUD_CONNECTORS_ARCHITECTURE.md), seção 6).
2. No modelo: provider oficial com versão exata, trava de conta (`allowed_account_ids`, `subscription_id`, `project`),
   backend de state com lock e versionamento.
3. Variáveis de nome que o executor repassa ao processo (nunca segredo).
4. Checagens de segurança específicas (seção 3 de [CLOUD_INFRASTRUCTURE_SECURITY.md](CLOUD_INFRASTRUCTURE_SECURITY.md)).
5. Testes com plano gravado (`plan -json` de exemplo) e validação real com identidade de teste.

## 8. Limitações atuais

- macOS e Linux implementados e não rodados em máquina real.
- Geração com os componentes do modelo (serviço em contêiner, banco, filas) nas três nuvens; a proposta com IA, a leitura
  de rede e a estimativa de custo são só da AWS. No Azure o banco é só PostgreSQL. Azure e Google têm `fmt` e `validate`
  provados, não `plan` nem `apply`: não há identidade Azure nem Google nesta máquina.
- Custo só na AWS e só dos tipos da tabela da seção 6 de [TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md); o resto do
  plano aparece como fora da estimativa. EC2 é cotado como Linux compartilhado.
- Drift agendado só roda com o Spock aberto (não há serviço em segundo plano no sistema). Fora do state só na AWS e só
  para os tipos da tabela da seção 6 de [TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md).
