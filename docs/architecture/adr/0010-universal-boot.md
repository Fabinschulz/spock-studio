# ADR-0010 — Universal Boot: uma camada de orquestração sobre as capacidades que já existem

**Status:** proposta em 2026-11-26, implementada na fundação; aguarda o dono (decisões BD-1 a BD-4 em
[universal-boot](../../universal-boot/README.md)).

## Contexto

O Início escolhia um tipo de atividade por palavras-chave no cliente e navegava com o texto; quem chamava o motor era a
tela de destino. Não havia no motor roteador de intenção, catálogo de capacidades nem nível de autonomia, e o produto não
tem identidade de usuário nem organização. Cada área (engenharia, gestão, infraestrutura, ideias, IA, conexões) tem os
próprios serviços, aprovações e histórico, e todos devem continuar como estão.

## Decisão

1. **O Boot é uma camada, não um domínio.** Mora em `Mariliz.Application/Boot`, interpreta o pedido, confere fatos e
   permissão, monta um plano e executa só o que é leitura ou preparação (que grava apenas na pasta de dados do Spock).
   O resto vira "abrir a tela", e a tela mantém a aprovação dela.
2. **O catálogo de capacidades é código, conferido por teste.** Cada ficha cita métodos de `ProtocolMethods` e telas de
   `paths.ts`; o teste falha se algum não existir. Disponibilidade é calculada dos fatos de agora, nunca declarada.
3. **Uma só avaliação de permissão**, com teto no nível 1. A classe da operação decide o nível mínimo; nada acima de
   preparação é executado pelo Boot, e o método executado vem da ficha, não do pedido.
4. **Reconhecimento léxico e reprodutível primeiro, medido por eval**, com a regra de perguntar quando há empate e de dizer
   que não há capacidade quando nada passa do mínimo. Modelo, se vier, entra como segundo estágio medido pela mesma eval.
5. **Registro próprio e pequeno** (`projects/<id>/boots/`), com checkpoint a cada passo, em vez de reaproveitar o diário de
   execuções: o diário é de quem escreve no projeto, permite uma execução por projeto e tem desfazer; o Boot não escreve no
   projeto e precisa coexistir com uma execução em andamento. A correlação com execuções é por id, sem copiar dados.
6. **A sessão do protocolo liga o Boot ao despacho depois de montada** (`BindEngine`), porque o Boot precisa das rotas e
   não pode conhecer a sessão.

## Alternativas descartadas

- **Um agente universal ou um workflow LangGraph para o Boot.** Concentra responsabilidade, gasta modelo para o que é lista
  e regra, e esconde a permissão dentro de um prompt.
- **Um Boot por menu.** Há diferença real de ciclo de vida só entre pedido, projeto e recuperação; o resto é capacidade.
- **Guardar o Boot como tipo de execução** (`RunKinds`). Aparece na central de execuções, mas trava o projeto durante a
  leitura e mistura "o que o Spock fez" com "o que o Spock gravou no projeto".
- **Capacidades registradas por descoberta automática de ferramentas** (MCP, CLIs). Expõe permissão sem decisão; fica para
  quando houver política de acesso (ADR-0007).

## Consequências

- Capacidade nova entra como ficha, sem mexer no reconhecedor nem no planejador.
- O Boot pode dizer "não sei" e "falta isto", que é o comportamento desejado, mas também erra por vocabulário: a eval
  existe para medir isso.
- Sem identidade, o registro guarda a origem e não a pessoa; quando houver, o registro e a auditoria mudam juntos.
- Work Boot e Autonomous Boot dependem de backlog, dependências e prontidão, que ainda não existem; a fundação os recusa
  em vez de simulá-los.
