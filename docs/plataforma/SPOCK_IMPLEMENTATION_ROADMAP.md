# Spock Studio: roadmap de implementação

**Data:** 2026-11-12. Documento 6 da análise de referência. Base: `SPOCK_PRODUCT_EVOLUTION.md` (o quê) e
`SPOCK_DESIGN_DIRECTION.md` (como se vê).

Este roadmap é uma proposta de ordem. Não é autorização para implementar: cada fase começa com a aprovação do dono do
produto, como nas fases anteriores da plataforma.

## 1. Como este roadmap se encaixa no que já existe

- As fases 1 a 6 de `evolucao-engineering-management.md` estão entregues ou em andamento. Este roadmap não as refaz.
- A ordem aprovada em 2026-11-11 é respeitada: (1) revisões do Harness, feita; (2) execuções ligadas à entrega; (3)
  cycle time e bloqueios; (4) decisões gerenciais com evidência técnica; (5) agentes técnicos; (6) histórico
  consolidado e investigação com LangGraph e RAG. A tabela da seção 3 mostra onde cada uma cai.
- O que a análise acrescenta são duas fases antes (validação e fundações de interface) e uma de hub. Elas entram
  porque as entregas aprovadas dependem delas: uma central de execuções sem padrão de lista e detalhe repetiria o
  problema da tela Projeto, e indicadores novos sobre conectores nunca validados seriam número sem base.
- As fases têm letras (A a G) para não colidir com a numeração das fases da plataforma.

Regras que valem para todas as fases:

1. Arquitetura preservada: o motor .NET decide e persiste; React apresenta; Python orquestra só onde há estado,
   roteamento ou pausa humana; Tauri hospeda. A interface não fala com o Python.
2. Cada método novo entra nos três lugares (interface, host, motor), com fixture de contrato.
3. Cada indicador novo entra com fórmula escrita e teste conferido à mão.
4. Nada de dado simulado. O que não tem base aparece como "sem dado", com o motivo.
5. Nenhuma dependência nova sem aprovação.
6. Sem refatoração global. As mudanças de interface são tela a tela.
7. Suítes ao fim de cada fase: `dotnet test`, `pnpm --dir desktop/ui check`, build da interface, `cargo test`.

## 2. Visão geral

| Fase | Nome | Valor principal | Tamanho | Depende de |
| --- | --- | --- | --- | --- |
| A | Validar o que existe | Confiança nos dados e segredo protegido | Pequeno | Credenciais de teste (decisão do dono) |
| B | Fundações de interface e shell | Projeto e ambiente sempre visíveis; telas consistentes | Médio | Nada |
| C | Central de execuções ligada à entrega | Um lugar para ver o que aconteceu | Médio | B |
| D | Gestão: tempo, bloqueios, marcos, decisões | Gestão explica por que, não só quanto | Médio a grande | A, B |
| E | Catálogo do Harness | Entender o que está instalado no projeto | Médio | B |
| F | Hub de integrações, nível 2 | Mais de um provedor de IA; dado externo como contexto | Médio a grande | A |
| G | Inteligência compartilhada | Engenharia e Gestão cruzadas, com evidência | Grande | C, D, F |
| Depois | Itens P2 e P3 | Ver seção 5 | | |

```
A ----+--------------------> D ----+
      |                            |
      +--------------------> F ----+--> G
B ----+--> C ----------------------+
      +--> E
```

A e B podem andar em paralelo. C e E dependem só de B. D pede A e B. G fecha o ciclo.

## 3. Fases

### Fase A: validar o que existe

**Por que primeiro.** A lição mais clara do Kaivor é o custo de acumular funcionalidade "implementada, validação real
pendente". O Spock tem hoje três pontos assim: sincronização real do GitHub, todo o conector do Jira e o assistente
gerencial com modelo real. Além disso, a chave de modelo está em texto puro.

| Entrega | Camada | Observação |
| --- | --- | --- |
| Sincronização real de um repositório GitHub de teste | Motor | Conferir paginação, limite de chamadas e ponto de retomada com dado real |
| Sincronização real de um projeto Jira de teste | Motor | O conector foi escrito só com dublês; tratar como não comprovado até aqui |
| Assistente gerencial com modelo real | Motor | Conferir se o verificador descarta o que deve e não descarta demais |
| Chave de modelo no cofre | Motor, interface | Migrar `model.json` para o cofre que as conexões já usam; ler o arquivo antigo uma vez e apagá-lo |
| Registro do que foi validado e do que não foi | Docs | Atualizar a seção de validação do documento da plataforma |

- **Depende do dono:** credencial de teste do GitHub e do Jira; chave de modelo; decisão 3 da seção 13 do documento
  da plataforma (migrar a chave).
- **Risco:** o conector do Jira pode exigir correção ao encontrar a API real. É melhor descobrir agora.
- **Pronto quando:** as três validações estiverem escritas com o resultado, inclusive o que falhou, e nenhum segredo
  estiver em texto puro.

### Fase B: fundações de interface e shell

**Por que agora.** Toda tela nova das fases seguintes precisa de página, seção, linha, estados e painel de detalhe.
Sem isso, cada uma copiaria o SCSS de novo. E o problema de experiência de maior impacto (projeto invisível) é barato.

| Entrega | Camada | Observação |
| --- | --- | --- |
| Componentes de página, seção, linha, vazio, carregando, erro, retorno de ação, divulgação | Interface | Em `shared/components/ui/`, sobre o ui-kit |
| Painel de detalhe e confirmação sobre o `Drawer` do ui-kit | Interface | Conferir foco, Esc e tema escuro do `Drawer` antes de adotar |
| Seletor de projeto no shell; seleção persistida | Interface, host | O host já guarda os recentes; falta guardar o último aberto |
| Alternador de ambiente e navegação por ambiente | Interface | Rotas atuais mantidas |
| Estado do motor em um só lugar; faixa superior com projeto e ambiente | Interface | Atualizar `app-shell.test.tsx` |
| Visão geral por ambiente | Interface | Só com métodos que já existem (`engineering.*`, `management.*`, `harness.runs`) |
| Portfólio navegável | Interface | Abrir o projeto pela linha |
| Migração das telas existentes para os componentes novos | Interface | Uma tela por vez; remover o padrão antigo ao terminar cada uma |
| Português do Brasil em tudo; um ícone por item; uma fonte de ícones | Interface | |
| Estado da jornada preservado por projeto | Interface | Não perder o plano ao sair da rota |

- **Não precisa de método novo no motor**, com uma exceção pequena: nome de exibição do projeto, se for editável.
- **Risco:** reorganizar o shell quebra testes de shell e de rotas. Eles são atualizados junto, não removidos.
- **Pronto quando:** nenhuma tela usa as classes copiadas; a lista de conferência por tela (seção 10 de
  `SPOCK_DESIGN_DIRECTION.md`) passa em todas; capturas novas em 760 e 1440 px.

**Estado da fase B (2026-11-12).** Primeira entrega feita na branch `feat/fase-b-shell-contexto`, sem commit até o
dono pedir. Só interface; motor, host, contratos e protocolo não mudaram.

| Feito | Observação |
| --- | --- |
| Seletor de projeto no topo do shell | Painel de disclosure com recentes, "Abrir outra pasta" e "Todos os projetos". Reaproveita o botão de recente que já existia, em vez de criar uma terceira lista |
| Último projeto reaberto ao abrir o aplicativo | Usa o primeiro dos recentes do host (já ordenados). Falha em silêncio; uma escolha da pessoa vence a restauração |
| Alternador Engenharia e Gestão | Links, com o ambiente atual marcado por `aria-current` e por forma. Nas telas comuns (Início, Integrações, Configurações) vale o último ambiente, guardado no armazenamento local |
| Menu só do ambiente atual, mais Plataforma | Projeto ganhou ícone próprio (não repete o de Configurar) |
| Estado do motor em um lugar só | Saiu do topo; a versão foi para o rodapé do menu; a tagline saiu |
| Portfólio navegável | A linha do projeto aberto leva ao acompanhamento; as outras abrem o projeto quando casam com um recente |
| Testes | 298 na interface (eram 255 antes da fase), mais tipos, lint, formato e build limpos. Prévia visual conferida em 1280 e 760 px com host simulado; nada foi conferido no Tauri empacotado |

Limite conhecido: o host guarda os recentes por caminho e não conhece o `projectId` do motor. Por isso só o projeto
aberto agora casa por id; os demais casam por nome e caminho abreviado, e só quando um único recente combina. Um
projeto fora dos seis recentes continua abrindo só pela tela inicial (a tela diz isso). Resolver de vez pede o host
guardar o `projectId` junto do recente, o que é mudança em Rust e fica como decisão separada.

**Segunda entrega da fase B (2026-11-12).** Também só interface, na mesma branch.

| Feito | Observação |
| --- | --- |
| Componentes de página compartilhados | `Page`, `PageSection`, `LoadingState`, `ErrorState` (com "Tentar de novo" quando a leitura pode ser refeita), `EmptyState`, `NoProjectState` e `ResourceView`, em `shared/components/ui/molecules`, sobre o ui-kit. 13 testes, inclusive de acessibilidade |
| Telas migradas | Portfólio, Acompanhamento, Assistente, Integrações, Projeto e Configurações, com as nove seções de Gestão, Integrações e Projeto. Acabaram as classes `.page`, `.title`, `.lead`, `.section` e `.sectionHead` copiadas em quatro módulos de estilo, e os cerca de vinte parágrafos de "lendo" e de erro escritos à mão nessas telas |
| Jornada Configurar preservada ao sair | Guardada por projeto, só na memória, e retomada ao voltar (`JourneyStoreProvider`, `restorableJourney`). Não se retoma uma execução em andamento (o acompanhamento morre com a tela) nem uma leitura ou plano "carregando" (voltam a "ainda não pedido"). O plano velho é seguro: o motor replaneja ao aplicar e recusa com `STALE_PLAN` se o projeto mudou. Concluir ou cancelar limpa. Um pedido novo vindo da tela Projeto começa do zero |
| Português do Brasil | "Guardar chave", "Guardar credencial", "Guardar a chave em Configurações" e "A carregar" viraram "Salvar" e "Carregando" |
| Testes | 326 na interface, mais tipos, lint, formato e build limpos. Prévia visual conferida (Acompanhamento, Integrações, jornada saindo para Gestão e voltando com o plano de 13 alterações e sem novo `harness.plan`) |

Decisão visível ao dono: a página de Configurações passou da largura de 36rem para os 46rem das demais, por uniformidade.

**Terceira entrega da fase B (2026-11-12).** Também só interface, na mesma branch.

| Feito | Observação |
| --- | --- |
| Tela Projeto repartida em três | `/projeto` é a **Visão geral** (perfil tecnológico, o que já existe, execuções e atalhos), `/projeto/harness` traz o Harness aplicado e as revisões, `/projeto/conhecimento` traz o conhecimento. A tela deixa de ser um relatório de seis seções empilhadas. Decisão 5 da seção 6 |
| Menu de Engenharia | Visão geral, Harness, Conhecimento, Configurar, Pedido de engenharia e Catálogo (em breve), cada um com ícone próprio. O Pedido de engenharia, que não tinha lugar no menu, passou a ter. "Visão geral" só fica marcada em `/projeto`, não nas telas abaixo dele |
| Remover conexão pede confirmação | No mesmo padrão de "Desconectar": diz que a conexão sai da lista, que a credencial é apagada deste computador e que usá-la de novo exige conectar com credencial nova (é o que o motor faz em `ConnectionService.Remove`). Desconectar e remover passaram a ter teste, que antes não existia |
| Testes | 335 na interface, mais tipos, lint, formato e build limpos. Prévia visual conferida nas três telas e no menu |

Gestão não ganhou uma "Visão geral" separada: o Acompanhamento já é a visão geral do projeto nesse ambiente, e uma tela nova só repetiria o conteúdo. O alternador de ambiente continua levando a ele.

**Quarta entrega da fase B (2026-11-12).** Também só interface, na mesma branch.

| Feito | Observação |
| --- | --- |
| Execução do workflow e Pedido de engenharia na `Page` | A Execução trocou o `<h1>` e as nove seções avulsas por `Page` e `PageSection`, e o Pedido de engenharia passou a usar `Page` (com a trilha que já tinha). O Pedido passou de 36rem para os 46rem das demais |
| `Disclosure` | Um componente para o "ver detalhes": o `<details>` do navegador com o mesmo visual (triângulo, foco, alvo mínimo, cor de ação). Substitui sete `<details>` de texto (assistente, resposta do assistente, previsão, conhecimento, perfil e revisões) e passou a ser a base do `EvidenceDisclosure` |
| Confirmação de sucesso das ações de conexão | `SuccessState` e uma mensagem opcional em `useAction`, escrita a partir do resultado: conectar, atualizar credencial, desconectar, remover, vincular e desvincular. **Testar** só confirma quando o motor devolve a conexão como "conectada": `connection.test` não falha quando o provedor recusa, só marca a conexão com erro, e um "deu certo" ali seria falso. A mensagem some quando a próxima ação começa |
| Testes | 343 na interface, mais tipos, lint, formato e build limpos. Prévia visual da mensagem de sucesso, do `Disclosure` aberto e fechado e do Pedido de engenharia |

Limites desta entrega: a mensagem de sucesso aparece no fim da seção, como os erros, e não dentro da linha da conexão que foi testada. Os quatro `<details>` de linha de lista da jornada Configurar (decisões, validação, arquivos, execução) têm sumário com layout próprio e ficaram como estão. O teste de teclado do `Disclosure` só prova o foco: o jsdom não ativa um `<summary>` com Enter, os navegadores ativam.

**Quinta entrega da fase B (2026-11-12).** Também só interface, na mesma branch. Fecha itens de navegação e foco da lista de problemas.

| Feito | Observação |
| --- | --- |
| Rolagem ao trocar de tela e de etapa | Era um defeito: o shell tem a altura da janela, quem rola é o `<main>`, e o hook rolava o documento, que nunca rola. Medido no navegador: com o `<main>` rolado em 400 px e o documento em 0, trocar de tela agora leva o `<main>` a 0. A área que rola vem de um contexto (`ContentAreaProvider`), e o shell passa o seu `<main>` |
| Foco ao trocar de tela e de etapa | O foco vai para o `<main>` depois de trocar de tela ou de etapa, e não na primeira renderização. Antes ficava num botão ou item de menu que já não representava o que estava na tela. O teste compara com a chave anterior, e não com "é a primeira vez", para valer também quando o React remonta o efeito |
| Stepper da jornada navegável | As etapas concluídas viram botões ("Voltar para a etapa X"); a atual e as futuras nunca são controles. Só antes de aplicar (`canReopenEarlierSteps`): durante e depois da aplicação o plano já foi usado, e reabri-lo levaria a aprovar de novo um plano que o motor recusaria com `STALE_PLAN` |
| Esc no painel de contexto | Abrir o painel pelo botão leva o foco a ele, e o Esc o fecha devolvendo o foco ao botão. O painel que já nasce aberto em janela larga não toma o foco. Sem levar o foco ao abrir, o Esc não teria a quem se dirigir, porque o painel só tem texto: foi uma falha da minha primeira versão, achada ao revisar e testada com a tecla real |
| Testes | 365 na interface, mais tipos, lint, formato e build limpos. Conferido no navegador: rolagem e foco ao trocar de tela, voltar pelo stepper e abrir e fechar o painel com Esc |

Limite: o teste do hook simula `scrollTo` (o jsdom não o implementa em elementos); a rolagem real foi medida no navegador, não no teste.

**Sexta entrega da fase B (2026-11-12).** Também só interface, na mesma branch.

| Feito | Observação |
| --- | --- |
| `ConfirmAction` | A confirmação de uma ação que apaga algo, num componente só: grupo com o nome do que se confirma, a frase do que acontece, botão de perigo e "Manter". Substitui as três cópias (desconectar, remover e desvincular). O foco vai para a opção segura ao aparecer e o Esc desiste |
| Foco que não cai no vazio | Ao clicar em "Remover" ou "Desconectar", o botão clicado saía da tela e o foco ia para o nada, o que desorienta quem usa teclado. Agora o foco vai para "Manter", e Enter ali nunca apaga nada |
| Mensagem de sucesso na linha | `useAction` passa a dizer a que linha a mensagem pertence (`target`). Testar, atualizar credencial e desconectar mostram o sucesso na linha da conexão que agiu. Remover (a linha some) e conectar (não é de uma linha) mostram na seção |
| Testes | 375 na interface, mais tipos, lint, formato e build limpos. Cobrem o foco e o Esc da confirmação, e a mensagem na linha certa e não na de outra conexão |

Limite: esta entrega foi conferida por testes, não por prévia visual.

Ainda falta nesta fase:

| Pendente | Observação |
| --- | --- |
| Painel de detalhe e confirmação sobre o `Drawer` do ui-kit | O `Drawer` já traz título, Esc, clique fora e botão fechar. Entra junto com a central de execuções (fase C), que é a primeira tela a precisar dele, para não ficar componente sem uso |
| Início e etapas da jornada Configurar | Têm desenho e moldura próprios (lançador e jornada); migrar para `Page` não ganha consistência e arrisca o que é a parte mais acabada do produto |
| Nome de exibição do projeto editável | Pede método novo no motor |

### Fase C: central de execuções ligada à entrega

Entrega 2 da ordem aprovada.

| Entrega | Camada | Observação |
| --- | --- | --- |
| Método de listagem de execuções com filtro e paginação | Motor | Projeção sobre os diários; sem armazenamento novo |
| Sincronização no mesmo modelo de execução, com eventos | Motor | Acaba com a releitura a cada 1,5 s |
| Respostas do assistente como execução | Motor | Já estão na auditoria |
| Tela Execuções: lista, filtros, detalhe em painel | Interface | Em Plataforma (tudo) e em Engenharia (do projeto) |
| Resumo de entrega no topo da execução | Motor, interface | Pedido, feito, validado, ficou de fora |
| Execução ligada a um item de trabalho | Motor, interface | A pessoa informa a issue ou o pull request; a ligação aparece em Gestão |
| Indicador de execução em andamento e de aprovação pendente no menu | Interface | |
| `run.snapshot` conferindo o projeto | Motor | Lacuna registrada |

- **Regra:** abrir o detalhe nunca dispara nada. Reverter e validar de novo continuam ações explícitas.
- **Risco:** o volume de diários em projetos antigos. A listagem pagina no motor e lê só o cabeçalho de cada diário.
- **Pronto quando:** qualquer execução passada pode ser reaberta pela interface, e uma falha mostra o próximo passo.

### Fase D: gestão que explica

Entregas 3 e 4 da ordem aprovada, mais marcos e relatórios.

| Entrega | Camada | Observação |
| --- | --- | --- |
| Cycle time | Motor | Jira pelo histórico de status; GitHub pela API de eventos da issue. Sem base, "sem dado" |
| Bloqueios | Motor | Vínculos de bloqueio do Jira; marcador acordado no GitHub |
| Marcos | Motor | Milestone do GitHub, versão do Jira; previsão por marco |
| Histórico de cada risco | Motor | Guardar a leitura, em vez de só recalcular |
| Registro de decisão gerencial com evidências | Motor, interface | Com os fatos do momento e as execuções relacionadas |
| Relatório do período | Motor, interface | Só fatos do motor; texto de IA passa pelo verificador |
| Indicador, gráfico de barras e faixa de previsão | Interface | Componentes simples, com valores escritos |
| Histórico de perguntas do assistente | Motor, interface | |

- **Depende de:** fase A (conectores validados) e fase B (componentes).
- **Risco:** campos de bloqueio e sprint variam por site do Jira. Começar pelo que é padrão e dizer o que não foi lido.
- **Pronto quando:** cada indicador novo tem fórmula, teste conferido à mão e fixture; a tela mostra de onde veio cada
  número.

### Fase E: catálogo do Harness

Entrega 5 da ordem aprovada.

| Entrega | Camada | Observação |
| --- | --- | --- |
| Ficha por componente (o que faz, quando age, o que lê, o que pode alterar, de onde veio) | Motor | Dado do motor, com fixture; os textos moram junto do ativo |
| Tela Harness com Estado, Catálogo e Revisões | Interface | Reparte a tela Projeto atual |
| Inventário de MCP no catálogo, com alerta de segredo literal | Motor, interface | O leitor já existe |
| Política dos agentes em linguagem simples | Motor, interface | A partir do hook |
| Tela Conhecimento separada | Interface | Com o que já existe em `engineering.knowledge` |

- **Decisão embutida:** sem editor livre. Mudança continua pela jornada Configurar.
- **Cuidado:** alterar o texto dos ativos em `Assets/` muda o hash do Harness aplicado. Os textos da ficha devem ficar
  fora do arquivo que é gravado no projeto.
- **Pronto quando:** uma pessoa que nunca abriu `.claude/` consegue dizer o que cada componente faz no projeto dela.

### Fase F: hub de integrações, nível 2

| Entrega | Camada | Observação |
| --- | --- | --- |
| Provedor de IA como tipo de conexão | Motor, interface | Credencial no cofre (fase A), teste, estado, troca |
| Segundo provedor de IA | Motor | O endpoint continua em lista permitida; a chave continua no motor |
| Modelo por finalidade | Motor, interface | Assistente, juiz, workflow |
| Resumo por conexão: projetos, dados, contexto | Interface | As quatro perguntas da seção 6.2 de `SPOCK_PRODUCT_EVOLUTION.md` |
| Política de contextualização consumida pelo índice | Motor, Python | Dado sincronizado autorizado entra como fonte externa; a tela mostra o que passa a ser enviado |
| Vocabulário de impacto no contrato de conector | Motor | "Leitura" e "Altera dados", mesmo sem escrita |
| Cofre em Linux e macOS | Motor | Decisão 2 da seção 13 |

- **Depende de:** fase A.
- **Risco:** dado externo no contexto dos agentes é o ponto mais sensível do roadmap. A política nasce fechada; a
  mudança é por conexão e por projeto; o proxy continua redigindo segredos; a auditoria registra.
- **Pronto quando:** um teste prova que dado de uma fonte não autorizada nunca chega ao índice nem ao provedor.

### Fase G: inteligência compartilhada

Entrega 6 da ordem aprovada e fase 7 do documento da plataforma.

| Entrega | Camada | Observação |
| --- | --- | --- |
| Linha do tempo do projeto | Motor, interface | Revisões, mudanças de perfil, execuções, decisões, riscos |
| Trilha de auditoria única por projeto | Motor | Referencia diário e arquivo de auditoria |
| Workflow de investigação em LangGraph | Python, motor | Só aqui há etapas e decisão no meio: cruzar riscos, itens parados e execuções |
| Laço agentic do RAG dentro desse workflow | Python | Reformular e buscar de novo, com teto |
| Registros de decisão indexados como memória do projeto | Motor, Python | |
| Evals com perguntas reais de gestão | Python | |
| Assistente sobre o portfólio | Motor | Respeitando a política de cada projeto |

- **Depende de:** C (execuções ligadas), D (decisões e bloqueios), F (política consumida), e validação com modelo real.
- **Risco:** é a fase com mais IA. Vale a mesma regra do assistente: fatos do motor, resposta verificada, o que foi
  descartado aparece.
- **Pronto quando:** uma pergunta como "por que a vazão caiu?" devolve uma resposta em que cada afirmação cita um fato,
  e o caminho da investigação pode ser reaberto na central.

## 4. Como a ordem aprovada aparece aqui

| Ordem aprovada em 2026-11-11 | Fase |
| --- | --- |
| 1. Revisões do Harness | Feita (branch `feat/plataforma-fase-6-versoes`) |
| 2. Execuções ligadas à entrega | C |
| 3. Cycle time e bloqueios | D |
| 4. Decisões gerenciais com evidência técnica | D |
| 5. Agentes técnicos com prioridades e objetivos | E |
| 6. Histórico consolidado e investigação com LangGraph e RAG | G |

**Decidido em 2026-11-12:** as fases A e B entram antes da ordem aprovada. A sequência passa a ser A e B (em
paralelo, quando possível), depois C, D, E, F e G. O motivo: sem B, a central de execuções nasceria sem componentes
compartilhados; sem A, os indicadores de tempo nasceriam sobre conectores não validados.

A decisão é sobre a ordem. O início de cada fase continua pedindo aprovação própria, e a fase A só começa com as
credenciais de teste (decisões 1 e 2 da seção 6).

## 5. Depois deste ciclo

Itens que não entram nas fases A a G. Ficam registrados para não se perderem e para não virarem tarefa por inércia.

| Item | Prior. | Quando reconsiderar |
| --- | --- | --- |
| Paleta de comandos | P2 | Quando houver mais de cinco projetos em uso ou mais de quinze telas |
| Painéis redimensionáveis | P2 | Depois do painel de detalhe estar em uso |
| Sincronização agendada e rotinas de plataforma | P2 | Depois da fase C (sincronização como execução) |
| MCP gerenciado pelo plano do Harness | P2 | Depois da fase E |
| Servidor MCP do Spock para o Claude Code | P2 | Depois da fase F; pede investigação própria de segurança |
| Assistente de engenharia | P2 | Depois da validação com modelo real e da fase G |
| Uso de modelo agregado | P2 | Junto da fase F, se couber |
| Modo de diagnóstico | P2 | Quando houver o primeiro usuário fora do time |
| Estado de operação interrompida | P2 | Junto da fase C, se couber |
| Segundo ambiente de IA (Codex ou Cursor) | P2 | Por demanda real |
| Leitura do ciclo de especificação feito no Claude Code | P2 | Depois da fase E |
| Azure DevOps ou GitLab | P2 | Depois de GitHub e Jira comprovados, por necessidade |
| Mapa de etapas somente leitura | P3 | Se um workflow com ramificação visível existir |
| Escrita em provedores | P3 | Só com decisão de produto; exige operação nomeada, aprovação e estado "incerto" |
| Bancos e APIs como conexão | P3 | Por demanda real |
| Identidade, papéis e organização | P3 | Depende da decisão 4 da seção 13 |
| Tema claro | P3 | Sem demanda |

Não entram em nenhum horizonte: construtor de automações, editor visual de fluxo, Database Studio, HTTP Client,
conversa livre como centro, memória aprendida, aplicativo web.

## 6. Decisões que precisam do dono do produto

| # | Decisão | Bloqueia |
| --- | --- | --- |
| 1 | Credencial de teste para GitHub e Jira | Fase A, e por ela D e F |
| 2 | Chave de modelo real para validar o assistente | Fase A, e por ela G |
| 3 | Migrar a chave de modelo para o cofre | Fase A e F |
| 4 | Aprovar o shell com seletor de projeto e alternador de ambiente | Fase B |
| 5 | A tela Projeto pode ser repartida em Visão geral, Harness e Conhecimento | Fases B e E |
| 6 | As fases A e B entram antes da ordem aprovada, ou a ordem segue como está. **Decidido em 2026-11-12: A e B antes.** | Resolvida |
| 7 | Segundo provedor de IA: qual | Fase F |
| 8 | Dado sincronizado pode alimentar o conhecimento dos agentes, com a política por conexão | Fases F e G |
| 9 | Componentes novos ficam no aplicativo, ou o ui-kit volta a receber mudanças | Fase B |
| 10 | Cofre em Linux e macOS agora ou depois | Fase F |

## 7. Riscos do roadmap

| Risco | Fase | Redução |
| --- | --- | --- |
| Conector do Jira falhar com dado real | A | Descobrir cedo; corrigir antes de D |
| Reorganização do shell quebrar testes | B | Testes atualizados na mesma mudança; rotas mantidas |
| Migração de telas ficar pela metade | B | Uma tela por vez; padrão antigo removido ao fim de cada uma |
| Central lenta com muitos diários | C | Paginação no motor; leitura só do cabeçalho |
| Campos do Jira variarem por site | D | Começar pelo padrão; dizer o que não foi lido |
| Texto de ficha mudar o hash do Harness | E | Texto fora do arquivo gravado no projeto |
| Dado externo vazar para o provedor de IA | F | Política fechada por padrão; teste de isolamento; auditoria |
| LangGraph onde bastava uma chamada | G | Só com etapas e decisão no meio; revisão antes de começar |
| Fases crescerem | Todas | Cada fase fecha com suíte verde e documento atualizado antes da próxima |

## 8. O que este roadmap não faz

- Não transforma todas as oportunidades em tarefas: 17 itens ficam para depois e 7 ficam de fora.
- Não estima prazo em dias. O tamanho é relativo (pequeno, médio, grande) porque depende de decisões da seção 6.
- Não altera nada: nenhum código, contrato, entidade ou tela foi modificado nesta análise.
