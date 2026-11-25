# Spock Studio — Workspace unificado

> **Estado:** primeiro incremento entregue no aplicativo real (shell, navegação, Início, entrada de pedidos, painel de atividade, busca e comandos, primeiros passos). As 24 telas que já existiam continuam abrindo, agora dentro do novo shell. Os próximos incrementos e as decisões pendentes estão na seção 10.
>
> O tema, os tokens e a linguagem visual vêm do redesign Workbench anterior e não mudaram; a fonte de verdade deles é o código (`desktop/ui/src/shared/styles/_tokens.scss`). Os produtos de referência estudados não são nomeados aqui; deles saíram princípios, não telas.

## 1. Diagnóstico da interface anterior

Método: leitura do código (`desktop/ui/src`), execução do aplicativo real (Tauri + motor) e capturas de cada tela em 1440×900.

| # | Achado | Evidência |
| --- | --- | --- |
| 1 | O Início era uma lista fixa de cinco cartões, igual para quem nunca abriu um projeto e para quem trabalha nele todo dia. | `journeys.ts` era uma constante; a tela não lia o estado. |
| 2 | Não havia onde escrever o que se quer fazer. Para pedir algo era preciso saber que existe "Pedidos" (Engenharia) ou "Assistente" (Gestão). | Nenhum campo de texto no Início nem na busca; a busca só achava nome de tela e de projeto. |
| 3 | O menu mudava inteiro conforme um seletor no topo (Engenharia ou Gestão), e as telas comuns herdavam o último ambiente. | Em Execuções o topo marcava "Gestão" enquanto a trilha dizia "Plataforma". |
| 4 | A atividade só existia em telas próprias. Duas validações que falharam não apareciam em lugar nenhum fora de Execuções. | Nenhum resumo no Início; o único sinal era um número no item do menu. |
| 5 | Configurações era um link de 12 px na barra de status. | `StatusBar.tsx`. |
| 6 | O projeto aparecia três vezes no Início (topo, bloco "Projeto", "Projetos recentes"). | `home-page.tsx`. |
| 7 | "Harness" no menu, sem dizer para que serve. Os demais nomes já eram claros. | `navigation.tsx`. |
| 8 | Não havia orientação de primeiro uso: quatro coisas a configurar (projeto, padrões, modelo, ferramentas) em três lugares diferentes, sem ordem nem progresso. | — |

O que já estava bom e foi mantido: tokens e tema escuro, a paleta de comandos (Ctrl K), o painel inferior (terminal e saída), a barra de status, a trilha, as abas de página, os estados vazios com orientação e a cobertura de acessibilidade nos testes.

## 2. As quatro regiões

```
┌──────┬───────────────┬───────────────────────────────────┬──────────────┐
│  A   │  A (painel    │ topo: projeto · buscar ou pedir   │      C       │
│barra │   da área)    ├───────────────────────────────────┤  atividade   │
│ de   │               │  B  área de trabalho              │ tarefas|feed │
│áreas │               ├───────────────────────────────────┤              │
│      │               │ painel inferior (terminal, saída) │              │
├──────┴───────────────┴───────────────────────────────────┴──────────────┤
│ barra de status: motor · terminal · saída · atalhos · versão            │
└─────────────────────────────────────────────────────────────────────────┘
```

### A — Navegação: barra de áreas e painel da área

O seletor de ambiente do topo e o menu que mudava com ele viraram uma coisa só. A barra mostra as áreas, sempre as mesmas, com ícone **e nome** à vista; o painel ao lado lista as telas da área aberta.

| Área | Telas (abas entre parênteses) |
| --- | --- |
| Início | a própria tela, sem painel |
| Engenharia | Visão geral · Padrões do projeto (Estado e revisões, Catálogo) · Configurar · Pedidos (Pedido de engenharia, Agentes de IA) · Conhecimento · Segurança · Infraestrutura. Fora do menu: Arquitetura, Da ideia ao projeto |
| Gestão | Portfólio · Acompanhamento · Assistente · Relatório |
| Atividade | Execuções · Histórico (Linha do tempo, Auditoria). Fora do menu: a execução aberta |
| Conexões | Serviços · Nuvem · Modelos de IA |
| Configurações | Minha IA · Diagnóstico · Sobre |

- A área atual vem da rota (`areaOf`), não de uma preferência guardada: não existe mais "ambiente lembrado".
- O painel recolhe e expande (botão no pé da barra, Ctrl B), e a escolha vale para a próxima abertura.
- Abaixo de 864 px o painel começa recolhido e, aberto, fica por cima do conteúdo; Esc ou trocar de tela o fecha. Abaixo de 600 px a barra fica só com os ícones: o nome continua sendo o nome acessível e aparece como dica.
- Gestão e Engenharia ficam a um clique uma da outra, sobre o mesmo projeto. Sem projeto aberto, Gestão abre no Portfólio.

### B — Área de trabalho

Nada mudou dentro das telas, além do Início (seção 3) e do nome de uma delas (seção 5). Cada tela continua com trilha, título, abas quando há, e os seus controles.

### C — Painel de atividade

Aberto pelo botão "Atividade" do topo ou por Ctrl + ponto; começa fechado e lembra a escolha.

- **Tarefas**: o que espera aprovação ou intervenção primeiro, depois o que está andando. Uma execução interrompida por queda não conta como tarefa.
- **Feed**: as últimas execuções de todos os projetos, sem repetir as tarefas.
- Cada linha abre o detalhe da execução na própria tela, e o detalhe só lê. A lista completa, com filtros, continua em Execuções.
- O aviso de quantas execuções esperam aprovação ou estão andando aparece na área Atividade da barra, no botão do topo e no item Execuções, com a frase inteira no nome acessível.
- Abaixo de 1088 px o painel fica por cima do conteúdo, encostado à direita.

Os dados vêm de `run.list`, que já existia; nenhum contrato novo.

### D — Entrada de pedidos

Existe em dois lugares com as mesmas regras (`features/ask`):

- No **Início**, um campo de texto com três tipos de atividade: Pedido de engenharia, Pergunta sobre o projeto, Nova ideia.
- Em **qualquer tela**, a busca do topo (Ctrl K): uma frase com três ou mais palavras passa a oferecer os mesmos três caminhos, com o mais provável já marcado.

A entrada **só encaminha**. O texto segue no estado da navegação até a tela que trata aquele tipo de pedido, que abre com ele preenchido; nada é iniciado sem o clique nessa tela, e as opções dela (análise de impacto, uso do modelo, aprovação) continuam lá. O texto não entra na URL nem fica guardado.

O tipo sugerido vem de regras simples e explicáveis (`suggestAskKind`): palavras como "risco", "prazo", "andamento" ou uma pergunta sugerem o Assistente; "ideia" ou "novo projeto" sugerem Da ideia ao projeto; o resto é pedido de engenharia. Nenhum modelo de IA decide: o aplicativo funciona sem chave configurada, e a pessoa vê a sugestão e pode trocá-la. Depois que ela escolhe um tipo, a escolha dela fica.

Como às vezes quem escreve procurava um lugar, a entrada também mostra até três telas relacionadas ao texto (`suggestCommands`): "configure as integrações" oferece Serviços.

## 3. Início

De cima para baixo:

1. **Pergunta** ("Olá! O que você deseja realizar hoje?") e a garantia de que nada muda sem aprovação.
2. **Entrada de pedidos**.
3. **Sugestões** conforme o estado: sem projeto, "Conectar meu primeiro projeto" (ou "Abrir um projeto", se já houve algum), "Consultar meus projetos", "Conectar ferramentas"; com projeto, "Analisar o projeto", "Configurar meu ambiente", "Acompanhar minhas entregas", "Da ideia ao projeto" e as duas que não dependem de pasta.
4. **Primeiros passos**, enquanto houver algo por fazer: abrir um projeto, preparar o projeto para a IA, conectar um modelo de IA (opcional), conectar ferramentas (opcional). Cada passo diz por que vale a pena e só aparece como feito quando o motor confirma (`engineering.harness`, `model.status`, `connection.list`). Pode ser ocultado e trazido de volta. Nenhum passo opcional bloqueia o uso.
5. **Atividade recente**: até três tarefas e três execuções, só quando há o que mostrar.
6. **Projetos recentes**.

Não há indicadores nem painel de métricas no Início.

## 4. Busca e comandos

A paleta (Ctrl K) agora agrupa o que oferece:

- **Ações**: abrir pasta de projeto, mostrar a atividade, abrir o terminal, recolher o menu, ver os atalhos.
- **Telas**: todas, de todas as áreas, com uma frase dizendo para que servem. Acham-se pelo nome apresentado e pelo técnico ("harness" acha Padrões do projeto; "integrações" acha Serviços).
- **Projetos**: os recentes, pelo nome ou pelo caminho.
- **Pedir ao Spock Studio**: os três caminhos, quando o texto é uma frase.

Quem escreve o nome de algo recebe esse algo primeiro; quem escreve uma frase recebe os caminhos de pedido primeiro e as telas relacionadas depois.

Não há identidade de usuário no aplicativo, então não há comando escondido por permissão. O contexto que existe é o projeto: um pedido sem projeto aberto pede a pasta antes de seguir.

## 5. Nomes: o apresentado, o técnico e a descrição

Cada item de `navigation.tsx` separa três coisas: `label` (o que a pessoa lê), `technical` (o nome técnico, que só a busca usa) e `description` (o que se faz ali, mostrado na busca). O glossário (`Term`, `glossary.ts`) continua explicando os termos técnicos onde eles aparecem no texto.

| Apresentado | Técnico | Onde o técnico continua visível |
| --- | --- | --- |
| Padrões do projeto | Harness | descrição da tela, seções internas, glossário |
| Conhecimento | RAG, índice | glossário |
| Infraestrutura | IaC | textos da tela |
| Serviços | Integrações | título da tela |
| Modelos de IA | Minha IA, provedores | Configurações |

Nenhum nome interno, contrato ou método do motor mudou.

## 6. Simples e avançado

Não há dois modos. O que existe é ordem: o caminho simples vem primeiro (pergunta, sugestões, primeiros passos) e as ferramentas de quem conhece o produto continuam a um atalho de distância (Ctrl K para qualquer tela, Ctrl J e Ctrl ` para o terminal, Ctrl B, Ctrl + ponto, o painel da área com todas as telas). Um seletor de "modo simples" foi avaliado e descartado nesta rodada: esconderia telas sem que nenhuma permissão o justificasse.

## 7. Componentes

A biblioteca `@mariliz/ui-kit` passou de 0.6.0 para **0.7.0**, que recebeu o que o Spock Studio mantinha por conta própria e usava em dezenas de telas:

| Componente | Uso |
| --- | --- |
| `Tooltip` | dica que descreve sem renomear |
| `Radio` | tipo de atividade, e os grupos de opção que já existiam |
| `StatusChip` | estado de execuções e de componentes, em 45 arquivos |
| `CountBadge` | avisos de contagem na barra, no topo e no painel |
| `TabBar` | abas do painel de atividade; ganhou `aria-label` e ficou legível no tema escuro |
| `LoadingBar` | progresso dos primeiros passos |

Os átomos locais equivalentes foram removidos; as telas continuam importando de `@/shared/components`, que os repassa. Um teste (`ui-kit-atoms.test.tsx`) guarda o comportamento de que o aplicativo depende. Os tons de atenção e de informação do tema passaram a vir dos mesmos tokens das demais cores.

Componentes novos que ficaram no aplicativo, por serem dele: `ActivityBar`, `AreaPanel`, `ActivityPanel`, `ActivityList`, `RequestComposer`, `SetupChecklist`, `RecentActivity`.

## 8. Acessibilidade

- Áreas e telas são links com o estado atual anunciado (`aria-current`), marcado também por forma (fundo e filete), não só por cor.
- Toda área tem o nome à vista; quando a barra fica só com ícones, o nome segue como nome acessível e aparece como dica.
- Os painéis que abrem e fecham dizem o estado (`aria-expanded`) e o que controlam. Abrir o painel de atividade leva o foco para ele; fechá-lo devolve o foco ao botão. O painel que reabre sozinho ao iniciar não puxa o foco.
- A entrada de pedidos tem rótulo, descreve o que vai acontecer (`aria-describedby`) e anuncia a troca do tipo sugerido sem interromper.
- A paleta é um `combobox` com `listbox` agrupado; as setas seguem a ordem da tela.
- As animações de entrada dos painéis respeitam a preferência de movimento reduzido (regra global do `_reset.scss`).
- A 480 px de largura (o equivalente a 300% de zoom numa janela de 1440 px) não há rolagem horizontal.

## 9. Validação

Feita no aplicativo real (janela Tauri, motor .NET e um projeto de verdade), dirigida pelo Playwright ligado à janela pelo protocolo de depuração, com o axe-core injetado na página. As jornadas só leem: nenhuma inicia execução, grava arquivo ou muda conexão.

| Verificação | Resultado |
| --- | --- |
| Início: pergunta, entrada de pedidos, sugestões | passou |
| Teclado: ordem do foco (atalho, marca, áreas) e foco visível | passou |
| Frase no Início vira pedido de engenharia com o objetivo preenchido | passou |
| Pergunta no Início sugere o Assistente e oferece telas relacionadas | passou |
| Ctrl K: "harness" leva a Padrões do projeto | passou |
| Ctrl K: frase oferece os caminhos e leva a pergunta ao Assistente, preenchida | passou |
| Ctrl + ponto: tarefas, feed e detalhe de uma execução sem sair da tela | passou |
| Cada área leva à tela dela e lista as telas certas (5 áreas) | passou |
| As 24 telas existentes abrem no novo shell, sem tela de erro | passou |
| Ctrl B recolhe e expande o painel | passou |
| 800×600: painel recolhido, abre por cima, fecha com Esc, sem rolagem horizontal | passou |
| 480×800: barra só de ícones com nome acessível, sem rolagem horizontal | passou |
| axe (WCAG 2.2 AA, com contraste) em 11 estados de tela | 0 violações |
| Console sem erros durante as jornadas | passou |

Total: 29 de 29. Além disso, `pnpm check` (tipos, lint, formatação e 984 testes em 105 arquivos) passa.

A validação achou e corrigiu três defeitos que já existiam: dois ícones da biblioteca com atributos SVG que o React recusava (aviso no console a cada renderização), as abas da biblioteca ilegíveis no tema escuro e uma chave repetida na lista de ferramentas da Visão geral quando dois módulos declaram a mesma ferramenta.

**O que não foi verificado:**

- Leitor de tela de verdade (NVDA ou Narrador). O axe e os testes cobrem nomes, papéis e estados, não a experiência de ouvir a tela.
- Um computador sem nenhum projeto recente. O estado "primeiro projeto" está coberto pelos testes de componente, não foi visto no aplicativo real.
- Uma execução esperando aprovação de verdade. A aba Tarefas foi vista vazia no aplicativo; com tarefa, só nos testes.
- macOS e Linux.
- As jornadas não estão no repositório nem no CI: rodaram a partir de um roteiro fora dele (seção 10).

## 10. O que ficou para depois

Incrementos:

1. **Entrada de pedidos**: seletores de modelo e de agente quando fizerem diferença; "Pedir a um agente de IA" como quarto tipo.
2. **Feed por projeto**: a linha do tempo do projeto aberto (`timeline.read`) como terceira aba do painel, com decisões e sincronizações além das execuções.
3. **Painel redimensionável**, como já é o inferior.
4. **Nomes dentro das telas**: "Integrações" e "Conexões cloud" ainda são os títulos das telas que o menu chama de "Serviços" e "Nuvem".
5. **`CheckboxOption`**: o par do `Radio` com rótulo rico ainda é local; cabe no `Checkbox` da biblioteca quando o rótulo dele aceitar conteúdo.
6. **Fluxos internos** de Engenharia e de Gestão (filtros de Execuções, que ocupam três linhas inteiras; a tela de Configurar) não foram redesenhados.

Decisões do dono:

| # | Decisão | Observação |
| --- | --- | --- |
| D1 | Tema claro | Avaliado e não feito. Os tokens semânticos permitem, mas é uma segunda paleta inteira a validar por contraste e uma decisão anterior fixou o tema escuro único. |
| D2 | Jornadas com Playwright no repositório e no CI | Exige uma dependência de desenvolvimento nova e minutos de CI. O roteiro existe e pode entrar. |
| D3 | Área "Projetos" e contexto de organização na barra | Hoje o projeto se troca pelo seletor do topo e o Portfólio cobre todos. O aplicativo não tem o conceito de organização nem de usuário. |
| D4 | Painel de atividade aberto por padrão no Início | Hoje começa fechado, e o Início mostra o resumo. |
