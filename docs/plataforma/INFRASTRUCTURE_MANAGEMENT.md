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
[REMEDIATION_REPORT.md](REMEDIATION_REPORT.md) fechava todo P0 e P1 como corrigido ou bloqueado por dependência externa
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
| Spock Infrastructure Management | Tela Infraestrutura (`desktop/ui/src/features/infrastructure`); `IacOperationService` (políticas, execução, histórico, trilha); `IacRuntimeService` (instalação, consentimento, ferramenta e versão por projeto); `IacRequestHandler` (`iac.*`, só interface); `IacMcpTools` (agentes) |
| IaC Runtime Abstraction | `IIacRuntime` (`FormatCheckAsync`, `ValidateAsync`, `PlanAsync`; `ApplyAsync` na entrega 3) e `IIacRuntimeFactory`; `IIacToolchain` para a distribuição de cada ferramenta |
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
| 3 | Aprovação vinculada ao plano, destaque de destruição, apply do plano aprovado, verificação pós-apply | Desenhada |
| 4 | State: assistente de backend, bootstrap do bucket no modelo, proteção e backup | Desenhada em [TERRAFORM_STATE_MANAGEMENT.md](TERRAFORM_STATE_MANAGEMENT.md) |
| 5 | Geração: da necessidade à arquitetura e aos `.tf` no modelo; uso das leituras cloud como entrada | Desenhada; usa o fluxo de ideias (`idea.*`) para a etapa de arquitetura |
| 6 | Drift e custos | Depois: custo só com fonte real (preços públicos do provedor) e marcado como estimativa |

Priorização dos recursos por provedor: primeiro o que o modelo de referência já cobre na AWS (SQS, SNS, CloudWatch,
ECR, Secrets Manager, IAM, ALB, ECS Fargate e o bucket de state). Azure e Google entram pelo mesmo modelo, começando por
grupo de recursos/projeto, rede e o equivalente de contêiner gerenciado (Container Apps, Cloud Run).

## 6. Experiência na tela Infraestrutura

- **Ciclo de uma mudança**: Descoberta → Arquitetura → Configuração → Validação → Plano → Aprovação → Aplicação →
  Verificação, cada etapa marcada como "Só analisa", "Prepara a mudança" ou "Muda a nuvem", e "Próxima entrega" enquanto
  não existir.
- **Dependência Terraform**: estado, mensagem, consentimento, proposta verificável, instalações com origem e verificação.
- **Projeto**: o que os `.tf` exigem, a versão usada (automática ou fixada) e a escolha.
- **Provedores**: quantas conexões cloud existem e quantas estão conectadas, com o link para Conexões cloud.

Ainda não aparecem: lista de projetos de infraestrutura com ambientes, histórico de planos e aplicações, alertas de
drift. Entram com as entregas 2 a 4.

## 7. Como adicionar um provedor

1. Conector cloud do provedor (ver [CLOUD_CONNECTORS_ARCHITECTURE.md](CLOUD_CONNECTORS_ARCHITECTURE.md), seção 6).
2. No modelo: provider oficial com versão exata, trava de conta (`allowed_account_ids`, `subscription_id`, `project`),
   backend de state com lock e versionamento.
3. Variáveis de nome que o executor repassa ao processo (nunca segredo).
4. Checagens de segurança específicas (seção 3 de [CLOUD_INFRASTRUCTURE_SECURITY.md](CLOUD_INFRASTRUCTURE_SECURITY.md)).
5. Testes com plano gravado (`plan -json` de exemplo) e validação real com identidade de teste.

## 8. Limitações atuais

- Só a entrega 1 está implementada: o Spock instala e escolhe o OpenTofu ou o Terraform, mas ainda não roda
  `validate`, `plan` nem `apply`.
- macOS e Linux implementados e não rodados em máquina real.
- Sem custo estimado, drift ou geração de `.tf` ainda.
