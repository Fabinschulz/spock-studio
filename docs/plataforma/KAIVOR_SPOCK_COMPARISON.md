# Kaivor e Spock Studio: matriz comparativa

**Data:** 2026-11-12. Documento 3 da análise de referência. Evidências do Kaivor em `KAIVOR_PRODUCT_ANALYSIS.md`;
evidências do Spock em `MARILIZ_CURRENT_STATE.md`.

## 1. Como ler

- **Complexidade:** baixa, média ou alta, estimada para a arquitetura atual do Spock (motor .NET decide, React
  apresenta, Python orquestra, Tauri hospeda).
- **Prioridade:** P0 essencial para consolidar o produto; P1 alto valor; P2 evolução importante, não prioritária; P3
  melhoria futura ou experimental.
- **Recomendação:** *Incorporar* (trazer o conceito quase como está), *Adaptar* (trazer a ideia, com outra forma),
  *Manter como referência* (não construir agora; consultar quando o tema chegar, ou o Spock já resolve), *Descartar*.

Regra aplicada em cada linha: uma funcionalidade só entra se resolver um problema do Spock registrado no diagnóstico.
Existir no Kaivor não é justificativa.

## 2. Diferença de natureza entre os dois produtos

| | Kaivor | Spock Studio |
| --- | --- | --- |
| Promessa | "Descreva o objetivo, eu executo com suas conexões." | "Configure, governe e acompanhe a engenharia assistida por IA dos seus projetos." |
| Quem executa o trabalho de IA | O próprio produto (bot, agentes, CLIs que ele orquestra) | O ambiente de IA da pessoa (Claude Code), com o Harness que o Spock instala e valida |
| Centro da interface | Conversa | Projeto |
| Público | Uma pessoa operando ferramentas | Engenharia e Gestão sobre o mesmo projeto |
| Amplitude | Muito larga (48 conectores, banco, HTTP, canvas, voz, web) | Estreita e profunda (Harness, gestão determinística) |
| Gestão de projetos | Não tem | Tem portfólio, indicadores, previsão e riscos calculados |
| Rigor de evidência | Alto no desenho, validação real pendente | Alto no desenho e nos testes, validação real também pendente |

Consequência para a matriz: o que o Kaivor tem de mais aproveitável são **padrões de experiência e de segurança**, não
áreas de produto. Trazer as áreas (canvas, banco, HTTP, bot) transformaria o Spock em outro produto.

## 3. Matriz

### 3.1 Produto e navegação

| Funcionalidade | Kaivor | Spock atual | Oportunidade | Benefício | Complex. | Prior. | Recomendação |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Contexto visível no shell | Projeto, branch, modelo e nível de acesso sempre à vista | O projeto ativo não aparece em nenhum lugar do shell | Seletor de projeto no topo do shell, com recentes; seleção persistida | Acaba com o "volte ao Início para trocar de projeto" e com a dúvida sobre de qual projeto são os números | Baixa | P0 | Incorporar |
| Navegação em níveis | Cinco itens fixos, ferramentas fixáveis, busca | Um menu com quatro grupos | Dois ambientes com navegação própria e uma área de Plataforma comum | Cada perfil vê só o que usa; os ambientes ganham identidade | Média | P0 | Adaptar |
| Página de entrada por contexto | Conversa vazia com três caminhos | Início é só um lançador | Uma visão geral por ambiente, com o estado do projeto e o próximo passo | Primeiro valor sem navegar; orienta quem chega | Média | P1 | Adaptar |
| Estado vivo na navegação | Contador de execuções ativas e lista "Em execução" | Estado do motor no rodapé, repetido no topo | Indicador de execução em andamento e de aprovação pendente no menu | A pessoa sabe que algo espera por ela sem abrir a tela | Baixa | P1 | Incorporar |
| Busca global e paleta de comandos | Ctrl/Cmd+K cobre itens e ferramentas | Não existe | Paleta para ir a telas, projetos e execuções | Reduz cliques quando houver mais telas e projetos | Média | P2 | Adaptar |
| Ferramentas fixáveis pelo usuário | Fixar e desafixar na barra | Não existe | Nenhuma por ora: o Spock tem poucas telas | Sem ganho com dez rotas | Baixa | P3 | Manter como referência |
| Configurações como modo do shell | Navegação própria de seções, trilha e busca | Uma tela com a chave de modelo | Área de Plataforma com seções (Integrações, Modelos, Preferências, Diagnóstico, Sobre) | Dá lugar ao que hoje não tem onde morar | Média | P1 | Adaptar |
| Painéis redimensionáveis | Divisores por ponteiro e teclado, restaurar layout | Só o painel de contexto da jornada, fixo | Painel de detalhe redimensionável em telas de lista e detalhe | Usa a largura da janela, hoje desperdiçada | Média | P2 | Adaptar |
| Dois hospedeiros (desktop e web) | Aplicativo web com execução em servidor | Só desktop, local-first | Nenhuma | Quebraria o local-first | Alta | P3 | Descartar |

### 3.2 Hub de conexões

| Funcionalidade | Kaivor | Spock atual | Oportunidade | Benefício | Complex. | Prior. | Recomendação |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Conectar não é autorizar | Regra de produto, com concessões por recurso | Já modelado: Connection, Project Integration, Data Scope, Contextualization Policy | Tornar a separação visível na tela, em linguagem simples | A pessoa entende o que cada conexão alcança | Baixa | P1 | Manter como referência (o Spock já resolve; falta mostrar) |
| Validar antes de gravar | Chamada real antes de salvar | Igual: só grava se o provedor aceitar | Nenhuma | | | | Manter como referência |
| Saúde da conexão | Não há estado persistido; a falha aparece no uso | Estados Connected, Error, Disconnected, e sinais de dado defasado no portfólio | Nenhuma no modelo. Mostrar a saúde no shell e no hub | O Spock está à frente | Baixa | P1 | Manter como referência |
| Provedor de IA como conexão | Cofre de provedores, vários provedores e CLIs, modelos locais | Chave em texto puro, fora da central, um provedor, um modelo | Provedor de IA vira um tipo de conexão no hub, com credencial no cofre e teste | Fecha a dívida de segurança e abre espaço para mais provedores | Média | P0 | Adaptar |
| Vários provedores e escolha de modelo | OpenAI, Anthropic, Google, locais; modelo por agente e por conversa | Só `gpt-4o-mini` | Segundo provedor e escolha de modelo por finalidade (assistente, juiz, workflow) | Independência de fornecedor; modelo adequado a cada tarefa | Média | P1 | Adaptar |
| Modelo local embutido | Catálogo de modelos locais instaláveis | Não existe | Aceitar um endpoint local compatível como provedor, sem instalar nada | Atende quem não pode enviar dados para fora | Média | P2 | Adaptar (sem instalador próprio) |
| Catálogo amplo de conectores | 48 conectores, maturidade variável | GitHub e Jira Cloud, nunca validados com dado real | Validar os dois que existem antes de qualquer outro; depois Azure DevOps ou GitLab, conforme necessidade | Dois conectores comprovados valem mais que dez por comprovar | Média | P0 (validar) / P2 (novos) | Manter como referência |
| Classificação de impacto | Leitura, escrita, sensível; desconhecido cai em sensível | Tudo é leitura; ações do workflow exigem aprovação | Adotar o vocabulário "Leitura" e "Altera dados" nas telas de plano e aprovação e no contrato de conector | Prepara o terreno para qualquer escrita futura sem redesenho | Baixa | P2 | Incorporar |
| Escrita em Jira e GitHub com aprovação | Criar, editar, comentar, transicionar, com aprovação | Só leitura, por decisão | Nenhuma agora. Se um dia entrar: operação nomeada, aprovação e estado "incerto" | Manter Jira e GitHub como fonte oficial | Alta | P3 | Manter como referência |
| MCP como conexão do produto | Hub de MCP reutilizado em tudo | Só inventário do `.mcp.json` | Gerenciar os MCP do projeto pelo plano do Harness (diff, aprovação, reversão), não como conexão do motor | Coerente com o modelo do Spock: o agente roda no ambiente da pessoa | Média | P2 | Adaptar |
| Spock como servidor para IAs externas | Gateway local com credencial por consumidor | Não existe | Servidor MCP local do Spock expondo conhecimento do projeto e fatos de entrega ao Claude Code, sob a política de contextualização | Leva a inteligência compartilhada para onde o trabalho de engenharia acontece | Alta | P2 | Adaptar (investigar antes) |
| Bancos e APIs HTTP como conexão | Database Studio e HTTP Client com recursos salvos | Não existe | Nenhuma agora. O roteiro antigo prevê banco somente leitura | Sem demanda registrada | Alta | P3 | Manter como referência |
| Erros em três camadas | O que houve, o que fazer, detalhe técnico recolhido | Códigos estruturados e mensagens; erro longe da ação em Integrações | Padrão único de erro com próximo passo ao lado da ação | Menos ruído técnico para o gestor, mais ação | Baixa | P1 | Incorporar |
| Histórico de uso por conexão | Só "último uso" por consumidor externo | Histórico de sincronização e auditoria | Nenhuma: o Spock já tem mais | | | | Manter como referência |
| Sincronização agendada | Rotinas agendadas com aplicativo aberto; avisa quando não rodou | Só sob demanda | Agendar a sincronização enquanto o aplicativo estiver aberto, dizendo quando não rodou | Dados de gestão menos defasados | Média | P2 | Adaptar |
| Política de contextualização consumida | Não há conceito equivalente | Existe e nasce fechada; só o assistente gerencial a consulta | Dado sincronizado autorizado passa a alimentar o conhecimento dos agentes | Engenharia e Gestão compartilham contexto de verdade | Alta | P1 | Manter como referência (ideia própria do Spock) |
| Cofre em Linux e macOS | Armazenamento seguro do sistema nas três plataformas | Só Windows | Implementar os dois cofres | Destrava o hub fora do Windows | Média | P2 | Incorporar |

### 3.3 Agentes e componentes

| Funcionalidade | Kaivor | Spock atual | Oportunidade | Benefício | Complex. | Prior. | Recomendação |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Catálogo de agentes | Grade de cards com modo e runtime | Três agentes embutidos, visíveis só como linhas do Harness aplicado | Catálogo do Harness: agentes, skills, regras, hooks e MCP, cada um com o que faz, quando age, o que usa e o que pode alterar | A pessoa entende o que foi instalado no projeto e por quê | Média | P1 | Adaptar |
| Visão "o que este agente pode acessar" | Espalhada pelo editor (lacuna do Kaivor) | Não existe | Uma ficha única por componente, gerada pelo motor a partir do ativo | Transparência que o Kaivor não tem | Média | P1 | Adaptar |
| Criar e editar agente no produto | Editor completo de perfil | Não existe; os ativos são embutidos | Personalização pela declaração do projeto, aplicada pelo plano; sem editor livre nesta fase | Mantém o ciclo plano, diff, aprovação, reversão | Alta | P2 | Adaptar |
| Versionamento de agente | Não tem | Revisões do Harness numeradas, com reversão | Nenhuma: o Spock está à frente | | | | Manter como referência |
| Agente determinístico (sem IA) | Editor em cinco passos, grafo de até 30 nós | Regras, cálculos e evals já são código determinístico no motor | Nenhum construtor. O princípio (não usar IA onde há regra) já é praticado | Evita um segundo motor de fluxo | Alta | P3 | Descartar o construtor; manter o princípio |
| Níveis de autonomia | Seguro, assistido, autônomo, completo, por agente | Hook de política e aprovação por ação | Apresentar a política do hook como "o que os agentes podem fazer neste projeto", em linguagem simples | Clareza para quem não lê o arquivo do hook | Baixa | P2 | Adaptar |
| Skills como instrução anexável | Gerenciador de skills | Uma skill própria e uma estrutura externa | Entra no catálogo do Harness | | Média | P1 | Adaptar (junto do catálogo) |
| Consumo de tokens por agente | Registro por quest, etapa, agente, provedor | Tokens por execução no diário, sem visão agregada | Uso de modelo por execução e por projeto, marcando o que é estimado | Controle de consumo sem prometer custo que o motor não conhece | Baixa | P2 | Incorporar |
| Laço de agente unificado e avaliação sem rede | Um motor, quatro adaptadores, teste roteirizado | Agentes sem ferramenta; evals próprias | Nenhuma agora: o Spock não roda laço de ferramentas | Só vale se o Harness Engineer vier para dentro (decisão D5) | Alta | P3 | Manter como referência |
| Vários ambientes de IA | Codex, Claude, Cursor e outros | Só Claude Code recebe configuração | Segundo ambiente configurável (Codex ou Cursor) | Amplia quem pode usar o Harness | Alta | P2 | Adaptar |

### 3.4 Automações e orquestração

| Funcionalidade | Kaivor | Spock atual | Oportunidade | Benefício | Complex. | Prior. | Recomendação |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Construtor de automações | Etapas por capacidade, linear na interface | Não existe | Nenhum construtor genérico | O Spock não é plataforma de automação | Alta | P3 | Descartar |
| Rotinas de plataforma | Rotinas agendadas propostas pelo bot | Não existe | Poucas rotinas fixas, determinísticas e do motor: sincronizar, recalcular riscos, gerar o relatório do período | Gestão atualizada sem esforço; previsível e sem IA | Média | P2 | Adaptar |
| IA propõe, pessoa aplica | Rascunho nunca salva nem executa sozinho | Igual: proposta do Harness Engineer tratada como dado não confiável e aplicada só pelo plano | Nenhuma | | | | Manter como referência (o Spock já faz) |
| Aprovação presa ao plano exato | Revisão exata do plano; permissões conferidas de novo | `approvalId` ligado ao hash das ações; aprovação por mudança e por comando | Nenhuma | | | | Manter como referência (o Spock já faz) |
| Estado "resultado incerto" | Escrita sem confirmação nunca vira sucesso nem é repetida | Aplicação do Harness tem backup e reversão; aplicação interrompida não tem recuperação automática | Estado explícito para operação interrompida, com o que conferir | Nunca afirmar sucesso não observado | Média | P2 | Incorporar |
| Repetição segura | Leitura repete, escrita não | Sincronização repete com espera; ações não repetem | Nenhuma | | | | Manter como referência |
| Quatro motores de execução | Automações, quests, tarefas, determinístico | Um modelo de execução (`RunJournal`) para tudo, menos sincronização | Trazer a sincronização para o mesmo modelo | Uma só história para "o que aconteceu" | Média | P1 | Manter como referência (lição do que evitar) |
| Mais de um workflow LangGraph | Não se aplica | Um grafo (`engineering.request`) | Segundo workflow só quando houver investigação em etapas (cruzar entrega com engenharia) | LangGraph onde há estado, roteamento e pausa humana | Alta | P1 | Manter como referência (ideia própria do Spock) |

### 3.5 Diagramas e visualização

| Funcionalidade | Kaivor | Spock atual | Oportunidade | Benefício | Complex. | Prior. | Recomendação |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Editor visual de fluxo | Canvas próprio, um tipo de nó, sem validação | Não existe | Nenhum editor | O Spock tem um grafo fixo de dez nós; o custo do canvas no Kaivor é muito alto | Alta | P3 | Descartar |
| Rascunho de diagrama por IA | Gera fluxo simples com agentes existentes | Não se aplica | Nenhuma | | | P3 | Descartar |
| Estado da execução sobre o fluxo | Nó ativo destacado, aresta animada | Linha do tempo com etapas, segmentos e evals | Manter a linha do tempo; acrescentar um mapa de etapas somente leitura se surgir um workflow com ramificação visível | A linha do tempo já responde "onde está" e "o que falhou" | Média | P3 | Manter como referência |
| Laço com contador e motivo | "Tentativa N de M", com origem e destino | O grafo não tem laço | Se um workflow futuro tiver retorno, mostrar tentativa e motivo | | Baixa | P3 | Manter como referência |

### 3.6 Planejamento e execução de atividades

| Funcionalidade | Kaivor | Spock atual | Oportunidade | Benefício | Complex. | Prior. | Recomendação |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Pipeline de desenvolvimento por agentes | Discovery, Spec, plano, Builder, QA, Reviewer, dentro do produto | O ciclo roda no Claude Code com a estrutura de especificação que o Harness instala | Não trazer a execução para dentro. Dar visibilidade ao ciclo: ler o que a estrutura de especificação produz e mostrar em que fase cada pedido está | Visibilidade e controle sem virar executor | Alta | P2 | Adaptar |
| Manifesto estruturado por etapa | Estado, arquivos, testes, riscos, próximas ações | Contratos versionados para plano, impacto e avaliação | Nenhuma no contrato. Usar o mesmo formato para ler o resultado do trabalho feito fora | | Média | P2 | Manter como referência |
| Bloco de entrega (spec, testes, evidências) | Resumo por execução com estado Validado, Com ressalvas, Bloqueado | Resultado da jornada com validações; execução de workflow com evidências e avaliação | Resumo de entrega no topo de toda execução: o que foi pedido, o que foi feito, o que foi validado, o que ficou de fora | Responde "terminou bem?" em uma linha | Média | P1 | Adaptar |
| Painel de estado das execuções | Kanban de cinco colunas derivadas do estado | Não existe | Entra na central de execuções como agrupamento por estado | | Baixa | P1 | Adaptar (como filtro, não como quadro) |
| Ajustar direção | Nota humana no meio da execução | Aprovar ou cancelar | Nenhuma agora: o workflow é curto | | Média | P3 | Manter como referência |
| Divisão em subtarefas | Texto dentro do pacote de spec | Não existe | Nenhuma: Jira e GitHub seguem como fonte de tarefas | | | P3 | Descartar |

### 3.7 Gestão de projetos

| Funcionalidade | Kaivor | Spock atual | Oportunidade | Benefício | Complex. | Prior. | Recomendação |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Portfólio | Não tem | Tem, por regra, com sinais | Tornar as linhas navegáveis, com ordenação e filtro | O gestor abre o projeto de onde o vê | Baixa | P0 | Manter como referência (melhoria própria) |
| Indicadores de fluxo | Não tem | WIP, vazão, lead time, prazo vencido | Cycle time e bloqueios (já na ordem aprovada) | Completa o conjunto mínimo | Média | P1 | Manter como referência (ideia própria) |
| Previsão de entrega | Não tem | Monte Carlo reprodutível, com confiança e motivos | Nenhuma no cálculo; melhorar a apresentação | | Baixa | P1 | Manter como referência |
| Riscos | Lista de texto por execução | Cinco regras com evidência | Histórico de acompanhamento de cada risco | Mostra se o risco está piorando | Média | P1 | Manter como referência |
| Marcos e prazos | Não tem | Só o prazo do item | Ler versões e marcos do provedor (milestone do GitHub, versão do Jira) | Marcos reais, sem cadastro paralelo | Média | P1 | Manter como referência |
| Dependências e bloqueios | Só entre agentes | Não calculado | Ler vínculos de bloqueio do Jira e referências entre issues | Explica por que algo parou | Alta | P1 | Manter como referência |
| Histórico de decisões | Decisões dentro da memória do projeto | Registros de decisão de engenharia em `.mariliz/decisions/` | Registro de decisão gerencial com as evidências que a sustentaram, ao lado das de engenharia | Rastreabilidade do "por que mudamos o prazo" | Média | P1 | Adaptar |
| Relatórios | Exportação por seção; relatório por rascunho de IA | Não existe | Relatório do período montado só com fatos do motor, exportável | Substitui a montagem manual do status | Média | P1 | Adaptar |
| Gráficos em respostas e telas | O modelo envia dados e intenção; o renderizador controla o estilo | Tabela com barras em CSS | Componentes de gráfico simples (barra, linha, faixa de previsão) alimentados só por números do motor | Leitura rápida sem abrir mão do rigor | Média | P1 | Adaptar |

### 3.8 Suporte da IA

| Funcionalidade | Kaivor | Spock atual | Oportunidade | Benefício | Complex. | Prior. | Recomendação |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Resposta separada em fatos, cálculos, inferências e recomendações | Não tem como estrutura | Tem, com verificação de números e citações | Nenhuma: é diferencial do Spock | | | | Manter como referência |
| Mostrar o que será enviado | Painel de contexto da próxima resposta | Tem no assistente gerencial | Estender ao pedido de engenharia | Mesmo controle nos dois ambientes | Baixa | P1 | Incorporar |
| Histórico de conversas | Durável, com busca | O assistente não guarda perguntas | Histórico de perguntas e respostas por projeto, com os fatos de cada resposta | Retomar uma análise sem refazer | Média | P1 | Adaptar |
| Assistente de engenharia | Conversa com projeto e ferramentas | Não existe; o RAG só alimenta o workflow | Perguntas sobre o projeto respondidas com trechos citados do índice, no mesmo padrão do assistente gerencial | Descoberta técnica com evidência, sem dar ferramenta ao modelo | Alta | P2 | Adaptar |
| Atividade da IA como elemento recolhível | Ação atual, contagem, tempo; linha do tempo sob demanda | Linha do tempo sempre aberta | Resumo de uma linha no topo, detalhe sob demanda | Menos ruído em execuções longas | Baixa | P2 | Incorporar |
| Memória de projeto | Arquivo com arquivos, testes, riscos, decisões | Decisões em `.mariliz/decisions/`; índice de conhecimento | Tratar os registros de decisão como memória do projeto e indexá-los | Decisões passadas entram no contexto dos agentes | Baixa | P1 | Adaptar |
| Memória aprendida e vista "Cérebro" | Três tipos de memória e uma vista 3D | Não existe | Nenhuma | Alto custo, valor incerto, risco de memorizar dado indevido | Alta | P3 | Descartar |
| Conversa livre como centro do produto | Bot | Não existe | Nenhuma | O centro do Spock é o projeto | Alta | P3 | Descartar |
| Modo automático do bot | Trabalha sem perguntar | Não se aplica | Nenhuma | | | P3 | Descartar |
| Voz, documentos, widgets | Existem | Não existem | Nenhuma | Fora do propósito | Alta | P3 | Descartar |

### 3.9 Execuções e observabilidade

| Funcionalidade | Kaivor | Spock atual | Oportunidade | Benefício | Complex. | Prior. | Recomendação |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Central de execuções | Projeção única, com busca, filtros e paginação | Diários completos, sem tela de lista | Central como projeção dos diários que já existem, mais sincronizações e respostas do assistente | "O que aconteceu neste projeto" em um lugar | Média | P0 | Incorporar |
| Detalhe em painel lateral com próximo passo | Estado, etapa com falha, o que fazer, entrada e resultado | Tela de execução sem volta nem contexto | Detalhe reabrível a partir da lista, com resumo e próximo passo | Investigar uma falha sem reproduzir | Média | P0 | Incorporar |
| Vazio diferente de filtro sem resultado | Exigido em spec | Estados vazios bem escritos, sem filtros | Adotar na central e no portfólio | | Baixa | P1 | Incorporar |
| Inspecionar nunca reexecuta | Regra explícita | Reverter e validar de novo são ações explícitas | Manter a regra ao criar a central | | Baixa | P0 | Incorporar |
| Execução ligada à entrega | Não tem | Não tem (está na ordem aprovada) | Ligar a execução ao item de trabalho quando a pessoa informar | Liga engenharia e gestão | Média | P1 | Manter como referência (ideia própria) |
| Trilha de auditoria única | Histórico por motor | Auditoria repartida entre arquivo e diários | Uma trilha por projeto que referencia as duas fontes | Uma resposta para "quem aprovou o quê" | Média | P1 | Manter como referência |
| Modo de diagnóstico | Captura de log com redação de segredos, painel com filtros | Só stderr | Diagnóstico ligável, com log redigido e exportável | Suporte sem pedir captura de tela | Média | P2 | Incorporar |
| Custo em dinheiro | Não tem | Sempre zero, por decisão | Nenhuma: continuar mostrando tokens | Não inventar número | | P3 | Manter como referência |

### 3.10 Ferramentas de desenvolvimento e dados

| Funcionalidade | Kaivor | Spock atual | Oportunidade | Benefício | Complex. | Prior. | Recomendação |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Database Studio | Cliente completo para seis motores | Não existe | Nenhuma | Compete com ferramentas especializadas; no Kaivor custou 114 specs | Alta | P3 | Descartar |
| HTTP Client | Coleções, ambientes, autenticação | Não existe | Nenhuma | Idem | Alta | P3 | Descartar |
| Explorador de arquivos, Git, terminal | Dentro do produto | Não existe | Abrir o projeto, um arquivo ou o diff no editor da pessoa | Atalho útil sem virar IDE | Baixa | P2 | Adaptar |
| Seguro por padrão em dados | Somente leitura por padrão, classificação no processo principal | Só GET, só HTTPS, escopo gravável restrito | Nenhuma: o Spock já é mais restrito | | | | Manter como referência |
| Recurso salvo como fronteira | Agente escolhe identificador aprovado, nunca URL ou SQL | O recorte monta a chamada; o conector não segue endereço do provedor | Nenhuma: mesmo princípio | | | | Manter como referência |

### 3.11 UX/UI

| Funcionalidade | Kaivor | Spock atual | Oportunidade | Benefício | Complex. | Prior. | Recomendação |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Painel lateral em vez de modal | Regra do produto, um componente empilhável | Um modal; painel de contexto próprio; `Drawer` do ui-kit sem uso | Painel lateral como padrão para detalhe, edição e confirmação | Mantém o contexto da lista visível | Média | P0 | Incorporar |
| Esqueleto de página compartilhado | Cabeçalho de contexto, lista pesquisável, bancada | Copiado em quatro módulos | Componentes de página, seção, linha, vazio, carregando e erro | Telas novas saem consistentes e mais baratas | Média | P0 | Incorporar |
| Feedback ao lado da ação | Regiões de estado e alerta junto do controle | Sucesso sem confirmação; erro longe da linha | Padrão de retorno por ação, inclusive sucesso | A pessoa sabe se funcionou | Baixa | P0 | Incorporar |
| Botão desabilitado explica o motivo | Regra escrita | Parcial | Adotar como regra | | Baixa | P1 | Incorporar |
| Confirmação destrutiva | Painel com alvo e o que não será afetado | Ora modal, ora inline, ora ausente | Um só padrão, com o que não será afetado | Evita perda por engano | Baixa | P0 | Incorporar |
| Detalhes técnicos recolhidos | Padrão do produto | `details` em onze arquivos, cada um com um estilo; termos técnicos em telas de gestão | Um componente de divulgação e duas camadas de texto: linguagem de tarefa na frente, nome técnico dentro | Gestão sem jargão; engenharia sem perder precisão | Média | P1 | Incorporar |
| Rascunho isolado e descarte confiável | Edição só vale ao salvar | A jornada perde o estado ao sair da rota | Preservar o estado da jornada por projeto | Não perder trabalho ao navegar | Média | P1 | Incorporar |
| Uso da largura | Coluna limitada na conversa; painéis nas ferramentas | Coluna de 46rem em telas de relatório | Layout de lista e detalhe e grade de indicadores acima de certa largura | Aproveita a tela | Média | P1 | Adaptar |
| Uma família de ícones | Norma de uma família, descumprida | Quatro fontes de ícone | Só os ícones do ui-kit | Consistência | Baixa | P1 | Incorporar (a norma) |
| Temas claro e escuro | Paridade obrigatória | Escuro único, por decisão | Nenhuma | | | P3 | Manter como referência |
| Sem acento cromático | Neutros, cor só em estado | Acento teal `#33C1BA` | Nenhuma: o teal é identidade | | | | Descartar |
| Papel de parede e vidro | Cena estática atrás da conversa | Não existe | Nenhuma | Decoração sem função no Spock | | P3 | Descartar |
| Honestidade de medição | Marca estimativa, admite incerteza | "Sem dado" com motivo; confiança com fatores | Nenhuma: mesma postura | | | | Manter como referência |

## 4. O que o Spock já faz melhor

1. **Gestão de projetos calculada.** Portfólio, indicadores, previsão e riscos por regra, com fórmula explícita. O
   Kaivor não tem nada equivalente.
2. **Resposta de IA verificada.** O assistente separa observado, calculado, inferido e recomendado, e o motor descarta
   afirmação sem fato citado ou com número fora dos fatos. O Kaivor instrui o modelo a não inventar; o Spock confere.
3. **Estado de conexão persistido** e sinal de dado defasado. No Kaivor a falha só aparece no uso.
4. **Um só modelo de execução** com diário, eventos e reversão. O Kaivor tem quatro motores.
5. **Versionamento e reversão do que é instalado no projeto** (revisões do Harness). O Kaivor não versiona agentes.
6. **Plano com diff e aprovação por mudança.** No Kaivor o plano de uma tarefa só pode ser aprovado ou parado.
7. **Contratos versionados com fixtures** conferidas em mais de uma linguagem, e allowlist de métodos em três camadas.
   No Kaivor as garantias ficam em arquivos de dez a dezoito mil linhas.
8. **Identidade visual única e testada.** Um tema, um acento, contraste conferido por teste. O Kaivor convive com três
   gerações visuais.
9. **Separação de conexão, vínculo, recorte e política de contexto** já no domínio.
10. **Foco.** O Spock faz poucas coisas e as prova por teste. A amplitude do Kaivor aparece como lista de pendências.

## 5. O que falta ao Spock e o Kaivor mostra como resolver

1. Contexto de projeto no shell e navegação por ambiente.
2. Central de execuções com detalhe reabrível.
3. Padrões de interface compartilhados: painel lateral, página, seção, estados, retorno de ação.
4. Provedor de IA dentro do hub e do cofre.
5. Catálogo legível do que o Harness instala.
6. Camada de texto para quem não é técnico.
7. Histórico (de perguntas, de riscos, de decisões).
8. Diagnóstico exportável.

## 6. O que não trazer

Construtor de automações, editor visual de fluxo, Database Studio, HTTP Client, conversa livre como centro do produto,
memória aprendida, voz, documentos, aplicativo web e catálogo amplo de conectores. Cada um desses é uma área inteira de
produto no Kaivor, com custo alto e pendências abertas, e nenhum resolve um problema registrado no diagnóstico do Spock.
