# Spock Studio — Workspace unificado

> **Estado:** oito incrementos entregues no aplicativo real. O primeiro trouxe o shell, a navegação, o Início, a entrada de pedidos, o painel de atividade, a busca com comandos e os primeiros passos; o segundo, a pergunta sobre o código como quarto caminho da entrada e o aviso do que cada caminho precisa; o terceiro, a aba do projeto no painel de atividade; o quarto, a largura ajustável desse painel; o quinto, os títulos das telas iguais aos nomes do menu; o sexto, o `Checkbox` da biblioteca no lugar do componente local e o resumo da jornada Configurar que não cobre mais o conteúdo; o sétimo, os filtros de Execuções numa linha; o oitavo, tarefas e execuções recentes na busca. As 24 telas que já existiam continuam abrindo, agora dentro do novo shell. Os próximos incrementos e as decisões pendentes estão na seção 10.
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
- **Projeto**: os doze acontecimentos mais recentes do projeto aberto, vindos da linha do tempo: execuções, decisões, riscos, sincronizações e mudanças de perfil. Sem projeto aberto, a aba diz para abrir um. A linha do tempo só é lida quando a aba é pedida, e a lista completa, com filtros, continua em Histórico.
- Cada linha abre o detalhe da execução na própria tela, e o detalhe só lê. A lista completa, com filtros, continua em Execuções.
- O aviso de quantas execuções esperam aprovação ou estão andando aparece na área Atividade da barra, no botão do topo e no item Execuções, com a frase inteira no nome acessível.
- A largura se ajusta pela divisória da borda esquerda, arrastando ou pelo teclado (setas, Home e End), entre 288 px e a metade da janela, e vale para a próxima abertura. É a mesma divisória (`Splitter`) do painel inferior.
- Abaixo de 1088 px o painel fica por cima do conteúdo, encostado à direita.

Os dados vêm de `run.list` e de `timeline.read`, que já existiam; nenhum contrato novo. A aba do projeto é um encaixe (`projectFeed`) que o shell entrega ao painel: a fatia de execuções não depende da fatia da linha do tempo.

### D — Entrada de pedidos

Existe em dois lugares com as mesmas regras (`features/ask`):

- No **Início**, um campo de texto com quatro tipos de atividade: Pedido de engenharia, Pergunta sobre o código, Pergunta sobre o andamento e Nova ideia.
- Em **qualquer tela**, a busca do topo (Ctrl K): uma frase com três ou mais palavras passa a oferecer os mesmos quatro caminhos, com o mais provável já marcado.

A entrada **só encaminha**. O texto segue no estado da navegação até a tela que trata aquele tipo de pedido, que abre com ele preenchido; nada é iniciado sem o clique nessa tela, e as opções dela (análise de impacto, uso do modelo, aprovação) continuam lá. O texto não entra na URL nem fica guardado.

| Tipo de atividade | Tela que trata | O que ela faz |
| --- | --- | --- |
| Pedido de engenharia | Pedidos | Analisa o pedido no projeto e mostra o impacto antes de mexer em arquivo |
| Pergunta sobre o código | Agentes de IA | Uma ferramenta de IA instalada no computador lê o projeto e responde, só para leitura |
| Pergunta sobre o andamento | Assistente | Responde sobre prazos, riscos e previsão, separando fato de interpretação |
| Nova ideia | Da ideia ao projeto | Monta especificação, arquitetura e backlog, uma etapa por vez |

O tipo sugerido vem de regras simples e explicáveis (`suggestAskKind`): "ideia" ou "novo projeto" sugerem Da ideia ao projeto; o vocabulário de gestão ("risco", "prazo", "andamento", "entrega") sugere o Assistente; uma pergunta sem esse vocabulário, ou um "explique" e um "como funciona", é sobre o código; o resto é pedido de engenharia. Nenhum modelo de IA decide: o aplicativo funciona sem chave configurada, e a pessoa vê a sugestão e pode trocá-la. Depois que ela escolhe um tipo, a escolha dela fica.

Como às vezes quem escreve procurava um lugar, a entrada também mostra até três telas relacionadas ao texto (`suggestCommands`): "configure as integrações" oferece Serviços.

A entrada do Início avisa quando o caminho escolhido depende de algo que falta (`askNotice`), e só então: a pergunta sobre o andamento sem modelo de IA conectado mostra o aviso com o link para Conexões › Modelos de IA; a pergunta sobre o código sem ferramenta de IA instalada diz que nenhuma foi encontrada. O pedido de engenharia e a nova ideia não avisam nada, porque funcionam sem os dois. O aviso sai do que o motor responde (`model.status`, `aitool.list`); sem resposta, não aparece.

Seletores de modelo e de agente dentro da entrada foram avaliados e ficaram de fora: o contrato de início de pedido não recebe modelo por pedido (o modelo por finalidade se define em Conexões), e a escolha da ferramenta de IA continua na tela de agentes, que mostra quais estão instaladas.

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
- **Atividade**: até cinco tarefas (o que espera aprovação ou intervenção, ou está andando) e as cinco execuções mais recentes, com o estado como dica. Escolher uma abre a execução pelo endereço dela, sem texto na URL. A lista vem de `run.list`, lido só quando a paleta abre; sem resposta do motor, o grupo não aparece e o resto da paleta segue igual. Execuções mais antigas se acham em Execuções, com filtros.
- **Pedir ao Spock Studio**: os quatro caminhos, quando o texto é uma frase.

Quem escreve o nome de algo recebe esse algo primeiro; quem escreve uma frase recebe os caminhos de pedido primeiro e as telas relacionadas depois.

Ainda não são buscados: documentos e agentes (o pedido original os lista; falta avaliar de onde viria a lista de cada um). Comandos que iniciam trabalho, como "Executar análise", também não existem: seguem o princípio da entrada de pedidos, que só encaminha para a tela onde a pessoa decide e aprova.

Não há identidade de usuário no aplicativo, então não há comando escondido por permissão. O contexto que existe é o projeto: um pedido sem projeto aberto pede a pasta antes de seguir.

## 5. Nomes: o apresentado, o técnico e a descrição

Cada item de `navigation.tsx` separa três coisas: `label` (o que a pessoa lê), `technical` (o nome técnico, que só a busca usa) e `description` (o que se faz ali, mostrado na busca). O glossário (`Term`, `glossary.ts`) continua explicando os termos técnicos onde eles aparecem no texto.

| Apresentado | Técnico | Onde o técnico continua visível |
| --- | --- | --- |
| Padrões do projeto | Harness | descrição da tela, seções internas, glossário |
| Conhecimento | RAG, índice | glossário |
| Infraestrutura | IaC | textos da tela |
| Serviços | Integrações | descrição da tela e busca |
| Nuvem | Conexões cloud | descrição da tela e busca |
| Modelos de IA | Minha IA, provedores | a mesma tela, aberta por Configurações › Minha IA |
| Catálogo | Catálogo do Harness | descrição da tela |

O título de cada tela é o nome que o menu dá a ela. A tela dos modelos de IA abre por dois menus e, em cada um, leva o nome que aquele menu usa. Quando uma tela manda a pessoa a outra, o link diz o caminho que a trilha mostra (por exemplo "Conexões › Serviços"), em vez de um nome que não aparece em lugar nenhum do menu.

Ficam como estão, de propósito: os textos que vêm do motor (como "Validação do Harness" na linha do tempo), que mudariam o motor, e "Minha IA" nos textos que explicam onde guardar a chave, porque é o nome em Configurações.

Nenhum nome interno, contrato ou método do motor mudou.

### Resumo da jornada Configurar

O resumo ao lado da jornada decidia se começava aberto pela largura da janela (1180 px ou mais), enquanto o layout decide se ele cabe ao lado pela largura da área de conteúdo (62rem). Com o menu da área e o painel de atividade ocupando espaço, ele nascia aberto por cima da página e cobria o botão "Analisar projeto". Agora os dois olham a área de conteúdo: aberto ao lado quando cabe, fechado quando cobriria o conteúdo, e de novo aberto quando o espaço volta. A escolha da pessoa vale enquanto o espaço não muda de faixa.

### Filtros de Execuções

Os três filtros (projeto, tipo e estado) ocupavam três linhas inteiras numa seção só deles, e a lista começava na metade da tela. A causa estava no `SelectField` da biblioteca, que se esticava sempre e ignorava o tamanho pequeno; corrigido na 0.9.0. Agora os filtros ficam numa linha, logo acima da lista que filtram, dentro da seção Histórico: a 1440 px, a primeira execução aparece em y = 279 em vez de por volta de y = 493.

## 6. Simples e avançado

Não há dois modos. O que existe é ordem: o caminho simples vem primeiro (pergunta, sugestões, primeiros passos) e as ferramentas de quem conhece o produto continuam a um atalho de distância (Ctrl K para qualquer tela, Ctrl J e Ctrl ` para o terminal, Ctrl B, Ctrl + ponto, o painel da área com todas as telas). Um seletor de "modo simples" foi avaliado e descartado nesta rodada: esconderia telas sem que nenhuma permissão o justificasse.

## 7. Componentes

A biblioteca `@mariliz/ui-kit` passou de 0.6.0 para **0.7.0**, depois para **0.8.0** e **0.9.0**. A 0.7.0 recebeu o que o Spock Studio mantinha por conta própria e usava em dezenas de telas:

| Componente | Uso |
| --- | --- |
| `Tooltip` | dica que descreve sem renomear |
| `Radio` | tipo de atividade, e os grupos de opção que já existiam |
| `StatusChip` | estado de execuções e de componentes, em 45 arquivos |
| `CountBadge` | avisos de contagem na barra, no topo e no painel |
| `TabBar` | abas do painel de atividade; ganhou `aria-label` e ficou legível no tema escuro |
| `LoadingBar` | progresso dos primeiros passos |
| `Checkbox` (0.8.0) | rótulo com ícone, nome e etiquetas e `labelProps`; a lista de ambientes de IA da jornada Configurar passou a usá-lo |
| `SelectField` (0.9.0) | `fullWidth={false}` e `size="small"` passaram a valer; os filtros de Execuções cabem numa linha e o período do Relatório tem a largura do que mostra |

Os átomos locais equivalentes foram removidos; as telas continuam importando de `@/shared/components`, que os repassa. Um teste (`ui-kit-atoms.test.tsx`) guarda o comportamento de que o aplicativo depende. Os tons de atenção e de informação do tema passaram a vir dos mesmos tokens das demais cores.

O `CheckboxOption` local saiu na 0.8.0: era o par do `Radio` com rótulo rico, e o `Checkbox` da biblioteca passou a aceitar o mesmo rótulo. A lista de ambientes de IA ganhou a aparência das outras caixas de marcar do aplicativo.

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
| Pergunta sobre o andamento sem modelo conectado mostra o aviso e o link para conectar | passou |
| Pergunta sobre o código segue para os agentes, preenchida, com o aviso de ferramenta ausente | passou |
| Ctrl K: "harness" leva a Padrões do projeto | passou |
| Ctrl K: frase oferece os caminhos e leva a pergunta ao Assistente, preenchida | passou |
| Ctrl K: o grupo Atividade vem depois das ações e antes das telas, lista as execuções reais com o estado, e Enter abre a execução pelo endereço dela, sem texto na URL | passou |
| axe (WCAG 2.2 AA, com contraste) com a paleta aberta a 1440 e a 900 px, sem rolagem horizontal a 900 px, Esc fecha, console sem erros | passou |
| Ctrl + ponto: tarefas, feed, acontecimentos do projeto aberto e detalhe de uma execução sem sair da tela | passou |
| Cada área leva à tela dela e lista as telas certas (5 áreas) | passou |
| As 24 telas existentes abrem no novo shell, sem tela de erro | passou |
| Ctrl B recolhe e expande o painel | passou |
| Painel de atividade: largura pelo teclado e pelo arraste, limites e largura lembrada | passou |
| O título de cada tela e aba é o nome do menu, e os links entre telas usam o caminho da trilha | passou |
| Configurar: o resumo da jornada fica ao lado quando cabe e fechado quando cobriria o conteúdo (com o painel de atividade aberto) | passou |
| Configurar: lista de ambientes de IA com o `Checkbox` da biblioteca, sem violação do axe nem erro no console | passou |
| Execuções: os três filtros numa linha a 1440 px (duas a 900 px), com 40 px de altura, e filtrar pelo teclado funciona | passou |
| 800×600: painel recolhido, abre por cima, fecha com Esc, sem rolagem horizontal | passou |
| 480×800: barra só de ícones com nome acessível, sem rolagem horizontal | passou |
| axe (WCAG 2.2 AA, com contraste) em 14 estados de tela | 0 violações |
| Console sem erros durante as jornadas | passou |

Total: 37 de 37 no roteiro, mais as duas verificações da jornada Configurar e as onze da paleta com o grupo Atividade, rodadas à parte. Além disso, `pnpm check` (tipos, lint, formatação e 1025 testes em 108 arquivos) passa.

Uma armadilha do roteiro: o modal da biblioteca entra com um fade, e o axe medido no meio dele acusa contraste que não existe (22 elementos, com opacidade de 0,11 a 0,16); assentado, dá zero. O roteiro espera a opacidade chegar a 1 antes de medir.

A validação achou e corrigiu três defeitos que já existiam: dois ícones da biblioteca com atributos SVG que o React recusava (aviso no console a cada renderização), as abas da biblioteca ilegíveis no tema escuro e uma chave repetida na lista de ferramentas da Visão geral quando dois módulos declaram a mesma ferramenta.

**O que não foi verificado:**

- Leitor de tela de verdade (NVDA ou Narrador). O axe e os testes cobrem nomes, papéis e estados, não a experiência de ouvir a tela.
- Um computador sem nenhum projeto recente. O estado "primeiro projeto" está coberto pelos testes de componente, não foi visto no aplicativo real.
- Uma execução esperando aprovação de verdade. A aba Tarefas foi vista vazia no aplicativo; com tarefa, só nos testes.
- A entrada de pedidos com modelo conectado e com ferramenta de IA instalada: no computador da validação o motor respondeu que não havia nenhum dos dois, então o aplicativo real mostrou os avisos; a ausência deles está só nos testes.
- O envio de uma pergunta a uma ferramenta de IA: a jornada confere que o texto chega preenchido e para ali.
- macOS e Linux.
- As jornadas não estão no repositório nem no CI: rodaram a partir de um roteiro fora dele (seção 10).
- O grupo Atividade da paleta com uma tarefa esperando aprovação: no computador da validação só havia execuções concluídas ou que falharam, então a ordem "tarefa antes de recente" está só nos testes. No aplicativo real foram vistas as cinco execuções mais recentes.
- A avaliação escrita, pergunta por pergunta, pelos 13 critérios do §15 do pedido original: há evidências por jornada, mas nenhuma resposta registrada para cada critério.

## 10. O que ficou para depois

Incrementos:

1. **Jornada Configurar**: o diagnóstico e a proposta estão em [`configurar.md`](configurar.md). Os incrementos C1 a C7, a DC2 e a DC5 foram entregues (#199 a #210); as sobras (T6, P4, P5, P6, PL2 e RS3) estão numa sessão própria.
2. **Fluxos internos de Gestão e de Configurações**: nunca foram redesenhados (etapa 4, itens 10 e 11 do pedido original). Cada um tem uma sessão própria, que começa por diagnóstico e proposta em `docs/redesign/gestao.md` e `docs/redesign/configuracoes.md` e espera a sua aprovação antes de implementar.
3. **Entrada de pedidos com arquivo ou informação extra** (§4.4 do pedido): os seletores de modelo e de agente foram avaliados e ficaram de fora com justificativa; anexar arquivo nem foi avaliado.
4. **Avaliação pelos 13 critérios** (§15): registrar a resposta de cada pergunta, com a evidência.

Decisões do dono:

| # | Decisão | Observação |
| --- | --- | --- |
| D1 | Tema claro | Avaliado e não feito. Os tokens semânticos permitem, mas é uma segunda paleta inteira a validar por contraste e uma decisão anterior fixou o tema escuro único. |
| D2 | Jornadas com Playwright no repositório e no CI | Exige uma dependência de desenvolvimento nova e minutos de CI. O roteiro existe e pode entrar. |
| D3 | Área "Projetos" e contexto de organização na barra | Hoje o projeto se troca pelo seletor do topo e o Portfólio cobre todos. O aplicativo não tem o conceito de organização nem de usuário. |
| D4 | Painel de atividade aberto por padrão no Início | Hoje começa fechado, e o Início mostra o resumo. |
