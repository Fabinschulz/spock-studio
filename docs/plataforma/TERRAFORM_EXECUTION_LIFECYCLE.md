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

## 2. Execução (entregas 2 e 3)

Cada operação vira uma **execução do Spock** (o mesmo `RunTracker` e diário dos workflows), com `projectId`, ambiente,
versão do Terraform e etapas, e aparece em Execuções, Linha do tempo e Auditoria.

O executor:

- obtém o binário por `IacRuntimeService.ResolveForUseAsync` (ferramenta e versão do projeto, OpenTofu por
  padrão, hash conferido, fixadas no primeiro uso);
- roda pelo `IProcessRunner` **sem shell**, com argumentos fixos montados pelo Spock, `-chdir` na pasta autorizada do
  projeto, `-input=false`, `-no-color`, e as variáveis `CHECKPOINT_DISABLE=1`, `TF_IN_AUTOMATION=1`, `TF_INPUT=0` mais
  as de nome da conexão (`AWS_PROFILE`, `ARM_SUBSCRIPTION_ID`, `GOOGLE_CLOUD_PROJECT`);
- usa `TF_DATA_DIR` próprio por projeto e ambiente dentro dos dados do Spock, para não misturar `.terraform/` entre
  workspaces;
- nunca aceita argumento livre vindo de agente ou de texto do modelo.

## 3. Plano guardado e aprovação vinculada

- `plan -out=<dados>/projects/<id>/iac/<execução>/plan.tfplan`, e `show -json` do arquivo para o resumo.
- O resumo é derivado de `resource_changes[].change.actions`: criar, atualizar, substituir (destrói e cria), destruir,
  só ler. Valores marcados como sensíveis no JSON (`*_sensitive`) nunca vão para tela, trilha ou agente.
- **Id do plano** = SHA-256 do arquivo `.tfplan`. A aprovação registra esse id, como o `ApprovalBinding` dos workflows
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
| `iac_status` | 1 (existe) | Lê disponibilidade, versão do projeto e exigências |
| `terraform_validate` | 2 | Roda `fmt -check` e `validate`; não toca a nuvem |
| `terraform_plan` | 2 | Gera plano; devolve o resumo e o id; precisa do vínculo cloud liberado para agentes |
| `terraform_plan_summary` | 2 | Lê um plano existente pelo id |
| `terraform_request_approval` | 3 | Pede aprovação de um plano; **a aprovação só acontece na tela** |
| `terraform_history` | 3 | Lista operações do projeto |

Não haverá `terraform_apply` para agentes: o MCP nunca contorna a aprovação humana.
