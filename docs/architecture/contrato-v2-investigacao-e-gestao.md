# Contrato v2: regras do motor fora do Python e da UI

Decisão do dono do produto (2026-11-13), a partir de PY-N4, UI-N11 e UI-N12 da auditoria (`docs/auditoria-plataforma-2026-11.md`).
Em todos os três casos, outra camada repetia uma regra do motor .NET, e renomear ou mudar a regra no motor degradava a outra camada em silêncio.

## Fase 1: grupos de fatos descritos pelo motor (PY-N4) — feita

- O catálogo dos grupos de fatos fica no motor (`Management/Assistant/FactGroups.cs`): nome, papéis (`base`: entra sempre; `default`: por onde começar quando a pergunta não indica nada; `sign`: sinal de atenção listado sem modelo) e as palavras da pergunta que apontam para cada grupo.
- O pedido à inteligência passa a ser `ai.investigation.start.v2`, com `groups` obrigatório (um descritor por grupo presente nos fatos). O v1 deixou de existir: motor e inteligência saem juntos no mesmo instalador, e um campo opcional manteria a lista escrita à mão como plano B.
- A inteligência não tem mais nenhum nome de grupo no código: `focus` e a resposta sem modelo leem os papéis do pedido. Grupo de fato sem descritor ou papel desconhecido é recusado (`CONTRACT_INVALID`).
- `contracts/fixtures/management/fact-groups.json` é a cópia do catálogo que os testes e a avaliação da inteligência leem. `FactGroupsContractTests` garante que é igual ao catálogo do motor.
- Comportamento preservado: a ordem do catálogo é a ordem em que o laço olhava os grupos antes. Os testes de foco e o dataset de avaliação passam sem mudança de expectativa.

## Fase 2: o motor diz o que a UI derivava (UI-N11) — feita

Campos aditivos (quem já lê continua funcionando):
- `counted` em cada marco (`management.milestones` e o relatório): propriedade de `MilestoneProgress` (`Items - ClosedWithoutDelivery`). A tela e o Markdown leem o número em vez de recalculá-lo.
- `interrupted` em cada item de `run.list`/`run.detail`: `running`/`validating` no diário sem ninguém executando agora. A UI deixou de derivar de `state` + `live`.
- Fixtures golden regeneradas (`milestones.json`, `report.json`, `run-list.json`, `run-detail.json`); o diff delas só tem os campos novos.
- Ficou de fora, de propósito: o resumo de atividade que a UI soma por categoria entre integrações. Tirar a soma da UI pede decidir antes como contar duas integrações apontando para o mesmo recurso (hoje contam duas vezes); sem essa regra, mover a soma para o motor só mudaria o lugar do problema.

## Fase 3: um só vocabulário para tela e Markdown do relatório (UI-N12) — a fazer, só na UI

O Markdown do relatório **é apresentação** e continua na UI: levá-lo ao motor inverteria a divisão de responsabilidades (o motor é dono das regras e dos números; a UI, de como eles aparecem). A duplicação se resolve dentro da UI: os rótulos de situação dos marcos e de gravidade que a tela e o `report-markdown.ts` repetem passam a vir de um módulo só.
