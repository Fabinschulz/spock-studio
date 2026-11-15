# Ciclo de execução do Terraform

Como uma mudança de infraestrutura anda no Spock, da necessidade à verificação, e onde a pessoa decide. A entrega 1
(dependência) está implementada; as etapas abaixo são o desenho das entregas 2 e 3 e dizem o que já existe para
apoiá-las. Formato dos arquivos: [TERRAFORM_PROJECT_LAYOUT.md](TERRAFORM_PROJECT_LAYOUT.md).

## 1. As oito etapas

| # | Etapa | O que o Spock faz | Toca a nuvem? | Quem decide |
| --- | --- | --- | --- | --- |
| 1 | Descoberta | Lê conta, região, rede e serviços pelas conexões cloud | Só leitura | A pessoa liberou a leitura no vínculo |
| 2 | Arquitetura | Propõe serviços, justificativa, dependências, riscos, alternativas e custo **quando há fonte** | Não | A pessoa aprova a proposta |
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

## 5. O que já existe para isso

| Necessidade | Já existe |
| --- | --- |
| Binário certo e íntegro por projeto | `IacRuntimeService.ResolveForUseAsync` (entrega 1) |
| Processo sem shell, com timeout e árvore encerrada | `SystemProcessRunner` (agora com variáveis de ambiente) |
| Execução com diário e eventos | `RunTracker`, `FileRunStore`, eventos `run.*` |
| Aprovação amarrada a hash | `ApprovalBinding` (workflows), padrão `plan`/`fix` com `STALE_PLAN` (segurança) |
| Confirmação de destruição | `ConfirmAction` na interface |
| Leitura cloud para descoberta e verificação | `CloudReadService` |
| Trilha | `IAuditLog`, Auditoria do projeto |

## 6. Ferramentas para agentes (MCP)

| Ferramenta | Entrega | Efeito |
| --- | --- | --- |
| `iac_status` | 1 | Lê disponibilidade, ferramenta e versão do projeto e exigências |
| `iac_validate` | 2 | Roda `fmt -check` e `validate` sobre a cópia; não lê backend nem state, não toca a nuvem |
| `iac_plan_summary` | 2 e 3 | Lê o último plano (ou um pelo id): contagens, endereços, achados, se já foi aplicado ou por que não pode ser |
| `iac_history` | 3 | Lista validações, planos e aplicações, com quem pediu e o resultado da verificação |
| `iac_state` | 4 | Onde vive o state de cada raiz, trava, criptografia, riscos e backend recomendado (só a configuração) |
| `iac_plan` | Depois | Gerar plano por agente pede o vínculo cloud liberado para agentes; hoje só a pessoa planeja |

Não haverá `terraform_apply` para agentes: o MCP nunca contorna a aprovação humana.
