# Spock Studio: proposta de evolução de produto

**Data:** 2026-11-12. Documento 4 da análise de referência. Base: `KAIVOR_PRODUCT_ANALYSIS.md`,
`MARILIZ_CURRENT_STATE.md` e `KAIVOR_SPOCK_COMPARISON.md`.

Este documento é proposta. Nada aqui foi implementado, e nada deve ser implementado sem decisão do dono do produto. A
ordem de construção está em `SPOCK_IMPLEMENTATION_ROADMAP.md`; a forma visual, em `SPOCK_DESIGN_DIRECTION.md`.

## 1. Tese

O Kaivor e o Spock resolvem problemas diferentes. O Kaivor executa trabalho a partir de uma conversa. O Spock governa a
engenharia assistida por IA de um projeto e mostra a quem gere o que está acontecendo, com dados reais.

O que o Spock tem de melhor (motor que decide, plano com diff, gestão calculada, resposta verificada) já está pronto.
O que falta é o que o Kaivor faz bem: **dar forma de produto a isso**. Contexto sempre visível, um lugar para ver o que
aconteceu, padrões de tela consistentes, um hub que explica o que cada conexão alcança.

Por isso a evolução proposta tem três movimentos, nesta ordem:

1. **Tornar visível o que já existe.** Shell com contexto, central de execuções, catálogo do Harness, histórico.
2. **Fechar as pontas soltas do que já foi construído.** Validação real, chave no cofre, política de contexto com
   consumidor, sincronização no mesmo modelo de execução.
3. **Ligar os dois ambientes.** Execução ligada à entrega, decisões com evidência técnica, conhecimento compartilhado.

Não há um quarto movimento de "ampliar para onde o Kaivor está". Construtor de automações, editor visual, cliente de
banco, cliente HTTP e conversa livre ficam fora (seção 9).

## 2. Princípios de produto para o próximo ciclo

Extraídos do que funciona no Kaivor e do que já é regra no Spock.

| # | Princípio | Origem |
| --- | --- | --- |
| 1 | O projeto ativo e o ambiente estão sempre visíveis. | Kaivor (contexto no campo de mensagem); lacuna do Spock |
| 2 | Conectar não é autorizar. Cada conexão diz o que alcança, em qual projeto e se alimenta os agentes. | Kaivor (regra de produto); Spock (modelo de domínio) |
| 3 | Toda operação tem uma execução, e toda execução pode ser reaberta. | Kaivor (central); Spock (diários) |
| 4 | Inspecionar nunca reexecuta. | Kaivor |
| 5 | A IA propõe; o motor decide; a pessoa aprova o plano exato. | Spock (já é regra) |
| 6 | Nunca afirmar sucesso que não foi observado. Operação interrompida tem estado próprio. | Kaivor ("resultado incerto"); Spock ("sem dado") |
| 7 | Linguagem de tarefa na frente; nome técnico dentro de "Detalhes técnicos". | Kaivor |
| 8 | Não usar IA onde há regra. Não usar LangGraph onde não há estado, roteamento ou pausa humana. | Spock (já é regra); Kaivor (automação sem IA) |
| 9 | Toda resposta de IA distingue fato, cálculo, inferência e recomendação, com evidência. | Spock (diferencial) |
| 10 | Um só modelo para cada conceito. Não criar um segundo motor, um segundo cadastro ou um segundo vocabulário. | Lição do Kaivor (quatro motores, três formatos de concessão) |

## 3. Navegação e organização dos ambientes

### 3.1 Problema

O projeto ativo não aparece no shell. Engenharia e Gestão são rótulos de grupo. O topo repete o estado do motor.
Pedido e Execução não têm lugar no menu.

### 3.2 Proposta

Um shell com três elementos fixos e a navegação do ambiente escolhido.

```
+----------------------------------------------------------------------------+
| [marca]  Projeto: api-pagamentos v   | Engenharia | Gestão |      [buscar] |
+-----------+----------------------------------------------------------------+
| Visão     |  Título da tela                         ação primária          |
| geral     |  trilha / contexto                                             |
| ...       |                                                                |
| (itens do |  conteúdo                                   painel de detalhe  |
| ambiente) |                                                                |
|           |                                                                |
| --------- |                                                                |
| Plataforma|                                                                |
| Motor: ok |                                                                |
+-----------+----------------------------------------------------------------+
```

- **Seletor de projeto** no topo, com recentes e "abrir pasta". A seleção sobrevive a reinício. "Todos os projetos"
  leva ao portfólio.
- **Alternador de ambiente** (Engenharia, Gestão) ao lado do seletor. Trocar de ambiente mantém o projeto.
- **Navegação lateral do ambiente**, com Plataforma fixa no rodapé.
- **Estado do motor** em um só lugar (rodapé do rail).

| Engineering Workspace | Management Workspace | Plataforma (comum) |
| --- | --- | --- |
| Visão geral | Visão geral | Integrações |
| Harness (estado, revisões, catálogo) | Portfólio | Modelos de IA |
| Configurar (jornada) | Acompanhamento (fluxo, previsão, riscos) | Execuções |
| Pedidos de engenharia | Marcos e bloqueios | Preferências |
| Conhecimento | Decisões | Diagnóstico |
| Execuções do projeto | Assistente | Sobre |
| | Relatórios | |

Notas:

- **Execuções** aparece nos dois lugares por desenho: dentro de Engenharia filtrada pelo projeto, e em Plataforma com
  tudo. É a mesma tela com filtro inicial diferente, não duas telas.
- **Visão geral** substitui o Início como página de entrada. Em Engenharia mostra o estado do Harness, a última
  execução, o que espera aprovação e o próximo passo. Em Gestão mostra os sinais do projeto, a previsão e os riscos
  abertos. O Início atual vira o estado "nenhum projeto aberto".
- A tela **Projeto** de hoje, com seis seções empilhadas, é repartida: perfil e inventário ficam na Visão geral de
  Engenharia; Harness aplicado, revisões e catálogo ficam em Harness; conhecimento ganha tela própria.
- As rotas atuais continuam válidas; as novas são acrescentadas.

### 3.3 O que os ambientes compartilham

O mesmo `projectId`, as mesmas conexões, a mesma central de execuções, o mesmo histórico de decisões e o mesmo
conhecimento. O que muda é a navegação, o vocabulário e os indicadores. Sem identidade de usuário, o ambiente é uma
preferência de visão, não uma permissão: isso precisa ser dito na tela, para não sugerir um controle que não existe.

## 4. Engineering Workspace

| Área | Hoje | Evolução proposta | Prior. |
| --- | --- | --- | --- |
| Organização de projetos | Pasta autorizada; recentes no host | Seletor no shell; seleção persistida; nome de exibição editável guardado pelo motor | P0 |
| Perfil tecnológico | Seção da tela Projeto | Fica na Visão geral, com o histórico de mudanças recolhido | P1 |
| Harness e configurações | Estado, dispensa, revisões, jornada | Tela Harness com três abas: Estado, Catálogo, Revisões. A jornada continua sendo o único caminho de mudança | P1 |
| Agentes | Três embutidos, sem tela | Ficha por agente no catálogo (seção 4.1) | P1 |
| Skills e Rules | Aplicadas pelo plano | Fichas no catálogo; regras do projeto mostradas ao lado das do produto | P1 |
| Hooks | Um hook de política | Ficha "O que os agentes podem fazer neste projeto", em linguagem simples, gerada a partir da política | P2 |
| Ferramentas (MCP) | Inventário do `.mcp.json` | Inventário visível no catálogo, com alerta de segredo literal. Depois, gerenciar pelo plano | P1 (ver) / P2 (gerenciar) |
| Integrações | Vínculos por projeto | Resumo das conexões do projeto na Visão geral, com atalho para o hub | P1 |
| Conhecimento contextual | Seção de leitura | Tela própria; mostra também as fontes sincronizadas autorizadas (seção 6) | P1 |
| Workflows | Um grafo | Lista de pedidos de engenharia com estado; segundo workflow só quando houver investigação em etapas | P1 |
| Execuções | Sem lista | Central de execuções (seção 7) | P0 |
| Evidências | Na execução | Resumo de entrega no topo de cada execução | P1 |
| Histórico técnico | Revisões do Harness, histórico do perfil | Linha do tempo do projeto que junta revisões, mudanças de perfil, execuções e decisões | P2 |

### 4.1 Catálogo do Harness

O problema: a pessoa instala agentes, uma skill, regras e um hook no projeto e só os vê como linhas com um selo de
integridade. Ela não sabe o que cada um faz.

A proposta: uma ficha por componente, montada pelo motor a partir do próprio ativo.

| Campo da ficha | Exemplo para um agente |
| --- | --- |
| O que faz | "Revisa as mudanças antes do commit e aponta problemas." |
| Quando age | "Quando você pede uma revisão ou ao final de uma tarefa." |
| O que lê | "Arquivos do projeto e o diff atual." |
| O que pode alterar | "Nada. Só escreve o parecer." |
| De onde veio | "Catálogo Spock" ou "Proposta do Harness Engineer, aprovada na revisão 4" |
| Estado no projeto | Íntegro, mantido, modificado, ausente, dispensado |
| Ações | Dispensar ou Voltar a aplicar (abrem o Configurar, como hoje) |

Decisões embutidas:

- **Sem editor livre de agente.** Personalização continua pelo `mariliz.yaml` e pela proposta, aplicada pelo plano. O
  Kaivor tem editor e não tem versionamento nem reversão; o Spock mantém o que tem de melhor.
- **A ficha é um dado do motor**, com fixture de contrato, não um texto escrito na interface.
- **Vale para todos os tipos** (agente, skill, regra, hook, MCP), com os campos que se aplicam.

### 4.2 Ciclo de engenharia

O ciclo pedido (Discovery, Analysis, Specification, Planning, Execution, Validation, Review, Delivery, Learning) hoje
se divide assim:

| Fase | Onde acontece | O que o Spock mostra |
| --- | --- | --- |
| Discovery, Analysis | Motor (`project.discover`, perfil) e workflow (`analyze_request`, `retrieve`) | Sim |
| Specification | No Claude Code, com a estrutura de especificação instalada | Não |
| Planning | Workflow (`plan`, `validate_plan`, `architecture`) | Sim |
| Execution | No Claude Code | Não |
| Validation | Evals do Harness; `evaluate` no workflow | Sim, para o Harness |
| Review | Agente revisor, no Claude Code | Não |
| Delivery | Pull request no GitHub | Só como item sincronizado |
| Learning | Não existe | Não |

A lacuna não é de execução: é de **visibilidade**. A proposta, em duas etapas:

1. **Ler o que o trabalho feito fora deixa no projeto.** A estrutura de especificação grava specs, tarefas e estado em
   arquivos do projeto. O motor pode ler isso (só leitura, como já lê o `.mcp.json`) e mostrar em que fase cada pedido
   está. Não exige rodar agente dentro do produto.
2. **Ligar o pedido à entrega.** Quando a pessoa informa a issue ou o pull request de um pedido, a execução passa a
   aparecer também em Gestão (seção 8).

Trazer a execução para dentro (decisão D5 do ADR) continua em aberto e não é pré-requisito de nada aqui.

## 5. Management Workspace

O Kaivor não oferece gestão de projetos. Aqui a referência serve só para padrões de apresentação. As funcionalidades
vêm da visão do próprio Spock e da ordem já aprovada.

| Área | Hoje | Evolução proposta | Prior. |
| --- | --- | --- | --- |
| Visão de portfólio | Uma linha por projeto, não navegável | Linhas abrem o projeto; ordenar e filtrar por sinal; comparação lado a lado de previsão e riscos | P0 |
| Gestão de projetos | Só leitura do que foi sincronizado | Continua sem criar tarefas. Jira e GitHub seguem como fonte oficial | Mantido |
| Indicadores | WIP, vazão, lead time, prazo vencido | Cycle time (Jira pelo histórico de status; GitHub exige a API de eventos); previsibilidade | P1 |
| Marcos | Só o prazo do item | Marcos lidos do provedor (milestone, versão), com itens abertos e previsão por marco | P1 |
| Bloqueios | Não calculado | Itens bloqueados lidos dos vínculos do Jira e de marcadores acordados; tempo bloqueado | P1 |
| Riscos | Cinco regras, recalculadas a cada leitura | Histórico por risco: quando surgiu, como evoluiu, quando fechou | P1 |
| Dependências | Não calculado | Vínculos entre itens lidos do provedor, só como lista | P2 |
| Previsão de entregas | Monte Carlo com confiança | Mesma conta; faixa de datas num gráfico; previsão por marco | P1 |
| Alterações de escopo | Regra `scope-growth` | Itens que entraram e saíram do marco no período, com data | P2 |
| Histórico de decisões | Não existe em Gestão | Registro de decisão gerencial com as evidências do momento (seção 5.1) | P1 |
| Relatórios | Não existe | Relatório do período montado só com fatos do motor, exportável (seção 5.2) | P1 |
| Assistente gerencial | Uma pergunta, sem histórico | Histórico de perguntas; perguntas sobre o portfólio; investigação em etapas com LangGraph quando cruzar com engenharia | P1 |

Regra que continua valendo: nenhum indicador por pessoa, nenhum número sem fonte, "sem dado" com motivo quando não há
base. Cada indicador novo entra com fórmula escrita e teste conferido à mão, como os atuais.

### 5.1 Decisões com evidência

Uma decisão gerencial ("adiar o marco em duas semanas") é registrada com:

- o texto da decisão e a data;
- os fatos do motor naquele momento (previsão, riscos, indicadores), guardados como estavam;
- quando houver, as execuções de engenharia relacionadas;
- quem registrou, quando existir identidade. Até lá, o campo fica vazio e a tela diz por quê.

Fica ao lado dos registros de decisão de engenharia que o workflow já grava em `.mariliz/decisions/`. Os dois tipos
aparecem na mesma linha do tempo do projeto.

### 5.2 Relatórios

Um relatório é uma fotografia do período: situação, entregas, previsão, riscos, bloqueios, decisões. É montado pelo
motor a partir dos mesmos fatos do assistente. Se houver texto de IA, ele passa pelo mesmo verificador e vem marcado.
Exportável em um formato que se possa enviar. Sem número que não esteja nos fatos.

## 6. Integration Hub

### 6.1 O que muda

| Tema | Hoje | Evolução proposta | Prior. |
| --- | --- | --- | --- |
| Validação real | GitHub e Jira nunca validados com dado real | Validar os dois com credencial de teste antes de qualquer conector novo | P0 |
| Provedores de IA | Chave em texto puro, fora do hub | "Modelo de IA" vira um tipo de conexão: credencial no cofre, teste, estado, troca de credencial | P0 |
| Mais de um provedor | Só OpenAI, um modelo | Segundo provedor; escolha de modelo por finalidade (assistente, juiz, workflow); endpoint local compatível | P1, P2 para o local |
| O que cada conexão alcança | Espalhado em vínculo, recorte e política | Um resumo por conexão: em quais projetos, quais dados, se alimenta os agentes | P1 |
| Política de contextualização | Só o assistente gerencial consulta | Dado sincronizado autorizado entra no índice de conhecimento, marcado como fonte externa | P1 |
| Sincronização | Sob demanda; histórico próprio; a interface relê a cada 1,5 s | Vira execução no mesmo modelo, com eventos; agendável com o aplicativo aberto | P1 (modelo), P2 (agenda) |
| MCP Servers | Inventário | Catálogo do Harness mostra; depois, gerenciar pelo plano | P1, P2 |
| Cofre | Só Windows | Linux e macOS | P2 |
| GitHub | Issues e pull requests | Marcos, eventos de issue (para cycle time), revisões | P1 |
| Jira | Issues com histórico de status | Versões, vínculos de bloqueio, sprints | P1 |
| Azure DevOps, GitLab | Não existem | Só depois dos dois primeiros validados, e por necessidade real | P2 |
| Bancos, APIs HTTP, arquivos | Não existem | Fora deste ciclo | P3 |
| Escrita em provedor | Não existe | Fora deste ciclo. Se entrar: operação nomeada, aprovação por ação, estado "incerto" | P3 |

### 6.2 Como a tela explica a separação

A separação já existe no domínio. Falta mostrá-la. Para cada conexão, quatro perguntas respondidas em linguagem simples:

| Conceito no domínio | Pergunta na tela |
| --- | --- |
| Connection | "Com qual conta o Spock entra neste serviço, e ela está funcionando?" |
| Project Integration | "Em quais projetos esta conexão é usada, e com qual recurso?" |
| Data Scope | "O que o Spock lê de lá?" |
| Contextualization Policy | "Os agentes de IA podem usar esses dados como contexto?" |

A última pergunta nasce em "não" e pede uma decisão explícita, com a explicação do que passa a ser enviado ao provedor
de IA quando a resposta é "sim".

### 6.3 Regra de segurança

Uma conexão ativa não concede acesso a agentes. O caminho de um dado externo até um agente passa por três portões, e
os três já existem: o recorte (o que é lido), a política de contextualização (se pode virar contexto) e o proxy de
modelo (o que sai para o provedor, com redação de segredos). A evolução é ligar o segundo portão ao índice, sem criar
um quarto mecanismo.

## 7. Shared Intelligence Core

| Tema | Hoje | Evolução proposta | Prior. |
| --- | --- | --- | --- |
| Execuções | Diários por tipo; sem tela de lista | **Central de execuções**: projeção dos diários, mais sincronizações e respostas do assistente; busca, filtro por tipo, estado, projeto e período; detalhe em painel lateral | P0 |
| Histórico | Por área | Linha do tempo do projeto, alimentada pela central | P2 |
| Auditoria | Repartida | Trilha única por projeto, que referencia diário e arquivo de auditoria | P1 |
| Evidências | Na execução | Resumo de entrega no topo: pedido, o que foi feito, o que foi validado, o que ficou de fora | P1 |
| Contexto compartilhado | Só o projeto | Execução ligada ao item de trabalho; decisão ligada a execuções | P1 |
| Conhecimento contextual | Só arquivos do repositório | Mais as fontes sincronizadas autorizadas e os registros de decisão | P1 |
| Memória | Checkpoint da execução | Registros de decisão como memória do projeto, indexados. Sem memória aprendida | P1 |
| RAG | Uma busca por execução | Laço agentic (reformular, buscar de novo) só no workflow de investigação; embeddings seguem adiados | P2 |
| Agentes especializados | Planning, Architecture, Judge | Agente de investigação de entrega, no segundo workflow | P1 |
| Orquestração | Um grafo | Segundo grafo para investigação em etapas; nada de LangGraph para consulta simples | P1 |
| Evals | Recall, baselines, juiz | Dataset com perguntas reais de gestão; verificação do assistente como eval contínua | P2 |
| Uso de modelo | Tokens por execução | Uso por execução e por projeto, com o que é estimado marcado | P2 |
| Diagnóstico | stderr | Modo de diagnóstico ligável, com log redigido e exportável | P2 |
| Operação interrompida | Workflow vira falha ao subir; aplicação do Harness não tem recuperação | Estado explícito "interrompida, confira", com o que foi e o que não foi aplicado | P2 |

### 7.1 A central de execuções

É a peça que mais vale construir, e o motor já tem quase tudo. Desenho:

- **Projeção, não armazenamento novo.** Lê os diários (`harness.runs`), o histórico de sincronização e a auditoria do
  assistente.
- **Uma linha por execução:** tipo, projeto, o que foi pedido, estado, início, duração, resultado em uma frase.
- **Filtros:** tipo (configuração, validação, reversão, pedido de engenharia, sincronização, pergunta), estado,
  projeto, período.
- **Detalhe:** resumo de entrega, etapas, evidências, aprovações, uso de modelo, arquivos, e "Próximo passo" quando
  falhou.
- **Ações explícitas e separadas da leitura:** reverter, validar de novo, abrir o item de trabalho.
- **Vazio diferente de filtro sem resultado.**

### 7.2 Onde a IA apoia cada ambiente

| Ambiente | Apoio | Como | Existe? |
| --- | --- | --- | --- |
| Engenharia | Análise de código e arquitetura | Workflow com recuperação e agente de arquitetura | Sim |
| Engenharia | Descoberta técnica | Assistente de engenharia: pergunta respondida com trechos citados | Não (P2) |
| Engenharia | Especificações e planejamento técnico | No Claude Code, com o Harness; o Spock passa a mostrar o estado | Parcial |
| Engenharia | Configuração do Harness | Proposta do Harness Engineer, aplicada pelo plano | Sim |
| Engenharia | Qualidade, revisão, validação | Evals determinísticas e juiz conferido | Sim |
| Gestão | Andamento, indicadores, desvios | Assistente com fatos e verificação | Sim, não validado com modelo real |
| Gestão | Riscos e bloqueios | Regras; depois, investigação em etapas | Parcial |
| Gestão | Previsão | Monte Carlo no motor; a IA só explica | Sim |
| Gestão | Relatórios | Fatos do motor, texto verificado | Não (P1) |
| Gestão | Recomendações com evidência | Seção "Recomendação" do assistente, com fato citado | Sim |

A regra de apresentação continua a do assistente gerencial e passa a valer para todo apoio de IA: observado,
calculado, inferido, recomendado, indisponível, e o que foi descartado na verificação.

## 8. Ligação entre os dois ambientes

É o que nenhum dos dois produtos tem hoje e o que mais diferencia o Spock.

| Ligação | O que permite |
| --- | --- |
| Execução de engenharia ligada a um item de trabalho | O gestor vê que a issue atrasada teve três pedidos de engenharia, um com ressalva |
| Revisão do Harness na linha do tempo do projeto | Explica uma mudança de ritmo depois de mudar a configuração dos agentes |
| Decisão gerencial com fatos e execuções | Rastreabilidade de por que o prazo mudou |
| Dado sincronizado no conhecimento dos agentes | O agente de planejamento conhece a issue e os critérios de aceite |
| Investigação em etapas | "Por que a vazão caiu?" cruza riscos, itens parados e execuções com falha |

Cada ligação respeita a política de contextualização e as permissões que existirem. Enquanto não houver identidade, a
regra é a do projeto: o que um ambiente vê é o que o outro também pode ver, e a tela não finge o contrário.

## 9. O que fica de fora, e por quê

| Fora | Motivo |
| --- | --- |
| Construtor de automações | O Spock não é plataforma de automação. No Kaivor são quatro motores e a interface não alcança o que o motor sabe fazer. |
| Editor visual de fluxo | O Spock tem um grafo fixo de dez nós. No Kaivor o canvas custou uma spec de 44 KB e segue com pendências de teste. A linha do tempo já responde "onde está" e "o que falhou". |
| Database Studio e HTTP Client | Competem com ferramentas especializadas. O Database Studio do Kaivor é um produto inteiro (114 specs). |
| Conversa livre como centro | O centro do Spock é o projeto. O assistente continua sendo pergunta com evidência. |
| Memória aprendida | Custo alto, valor incerto, risco de guardar dado indevido. |
| Catálogo amplo de conectores | Dois conectores comprovados antes de qualquer outro. |
| Aplicativo web | Quebra o local-first. |
| Editor livre de agente | Sem versionamento e reversão, como no Kaivor, perde-se o que o Spock tem de melhor. |
| Custo em dinheiro | O motor não conhece o preço; mostrar seria estimativa apresentada como fato. |
| Indicador por pessoa | Decisão já tomada. |

## 10. Dependências entre funcionalidades

```
Esqueleto de página e painel lateral ----+--> Shell com contexto ----+--> Visão geral por ambiente
                                         |                           +--> Portfólio navegável
                                         +--> Central de execuções --+--> Resumo de entrega
                                         |                           +--> Linha do tempo do projeto
                                         +--> Catálogo do Harness
Sincronização como execução -------------> Central de execuções
Validação real (GitHub, Jira) -----------+--> Cycle time, marcos, bloqueios --> Relatórios
                                         +--> Política de contexto consumida --> Investigação em etapas
Chave de modelo no cofre ----------------> Provedor de IA como conexão --> Segundo provedor
Validação com modelo real ---------------+--> Histórico de perguntas
                                         +--> Assistente de engenharia
Execução ligada ao item -----------------> Decisões com evidência técnica --> Investigação em etapas
```

Três nós sustentam quase tudo: os componentes de página, a validação com dado real e a central de execuções.

## 11. Riscos e compromissos

| Risco | Efeito | Como reduzir |
| --- | --- | --- |
| Ampliar antes de validar | Repetir o Kaivor: muito implementado, pouco comprovado | Validação real como primeira fase; nada novo sobre conector não comprovado |
| Dois ambientes virarem dois produtos | Navegação, componentes e conceitos duplicados | Um shell, uma central, um histórico, um conjunto de componentes |
| Catálogo do Harness virar editor | Perda do ciclo plano, diff, aprovação, reversão | Ficha só de leitura; mudança sempre pela jornada |
| Gestão com dado defasado | Decisão sobre número velho | Data da leitura sempre visível; sinal de dado defasado; agenda de sincronização |
| Dado externo no contexto dos agentes | Vazamento para o provedor de IA | Política nasce fechada; a tela mostra o que será enviado; redação de segredos no proxy |
| Ambiente sem identidade parecer permissão | Falsa sensação de controle | Dizer na tela que é uma visão, não um acesso |
| Reorganizar telas quebrar testes de shell e rotas | Retrabalho | Rotas atuais mantidas; testes de shell atualizados junto, não removidos |
| Componentes novos fora do ui-kit | Mais duplicação | Compor no aplicativo primeiro e promover ao ui-kit o que se provar; registrar o que foi promovido |
| Segundo workflow sem necessidade | LangGraph onde bastava uma chamada | Só quando houver investigação com mais de uma etapa e decisão no meio |
| Ler arquivos da estrutura de especificação externa | Acoplamento a formato de terceiro | Só leitura, com versão conferida, e falha silenciosa para "não foi possível ler" |

## 12. Priorização resumida

| Prior. | Itens |
| --- | --- |
| P0 | Validação real de GitHub, Jira e modelo; chave de modelo no cofre e provedor como conexão; componentes de página e painel lateral; shell com projeto e ambientes; central de execuções; portfólio navegável; retorno de ação e confirmação destrutiva padronizados |
| P1 | Visão geral por ambiente; catálogo do Harness; conhecimento com fontes sincronizadas; sincronização como execução; cycle time, marcos, bloqueios, histórico de riscos; decisões com evidência; relatórios; histórico de perguntas; execução ligada à entrega; resumo de entrega; trilha de auditoria única; segundo provedor de IA; gráficos simples; camada de texto para quem não é técnico |
| P2 | Paleta de comandos; MCP gerenciado pelo plano; servidor MCP do Spock; sincronização agendada e rotinas de plataforma; assistente de engenharia; uso de modelo agregado; diagnóstico; estado de operação interrompida; cofre em Linux e macOS; segundo ambiente de IA; linha do tempo do projeto; leitura do ciclo de especificação |
| P3 | Mapa de etapas somente leitura; escrita em provedores; bancos e APIs como conexão; identidade e papéis (depende de decisão); tema claro |
