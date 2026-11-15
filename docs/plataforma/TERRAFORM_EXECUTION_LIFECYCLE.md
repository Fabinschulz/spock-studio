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

Diferença do desenho anterior: as operações de IaC têm histórico próprio e ainda não aparecem na tela Execuções; a
integração com o `RunTracker` fica para a entrega 3, junto do apply.

## 3. Plano guardado e aprovação vinculada

- `plan -out=<dados>/projects/<id>/iac/operations/<op>/plan.tfplan`, e `show -json` do arquivo para o resumo (entrega 2).
- O resumo é derivado de `resource_changes[].change.actions`: criar, atualizar, substituir (destrói e cria), destruir,
  só ler. Valores marcados como sensíveis no JSON (`*_sensitive`) nunca vão para tela, trilha ou agente.
- **Id do plano** = SHA-256 do arquivo `.tfplan` (16 primeiros hex, já gravado na entrega 2). A aprovação registra esse id, como o `ApprovalBinding` dos workflows
  faz com o hash das ações. O apply recalcula o hash e recusa se o arquivo mudou (`IAC_PLAN_CHANGED`); um plano novo
  invalida a aprovação anterior.
- **Destruição em destaque**: qualquer `delete` ou substituição exige uma segunda confirmação com a lista dos recursos
  (`ConfirmAction`), e o pedido de apply carrega `destroyConfirmed: true`.
- Plano expira: se o state mudou (outro apply, lock), o Terraform recusa o plano velho e o Spock pede novo plano.

## 4. Aplicação e resultado

- `apply -input=false <plano>`: só o arquivo aprovado, nunca `-auto-approve` sem plano.
- Saída capturada linha a linha no diário da execução (sem valores sensíveis), código de saída, duração.
- **Concluído só com prova**: exit 0 **e** o plano de verificação sem mudanças (`-detailed-exitcode` = 0). Exit 0 com
  plano ainda mostrando mudanças vira "aplicado com divergência"; exit diferente de zero vira falha com os recursos que
  chegaram a mudar (lidos do state depois) marcados como falha parcial.
- Outputs (`output -json`) guardados sem os marcados como sensíveis.
- Recursos afetados registrados por endereço (`module.ecs.aws_ecs_service.this["api"]`), nunca por valor.

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
| `iac_status` | 1 (existe) | Lê disponibilidade, ferramenta e versão do projeto e exigências |
| `iac_validate` | 2 (existe) | Roda `fmt -check` e `validate` sobre a cópia; não lê backend nem state, não toca a nuvem |
| `iac_plan_summary` | 2 (existe) | Lê o último plano (ou um pelo id): contagens, endereços, achados |
| `iac_plan` | Depois | Gerar plano por agente pede o vínculo cloud liberado para agentes; hoje só a pessoa planeja |
| `iac_request_approval` | 3 | Pede aprovação de um plano; **a aprovação só acontece na tela** |
| `iac_history` | 3 | Lista operações do projeto |

Não haverá `terraform_apply` para agentes: o MCP nunca contorna a aprovação humana.
