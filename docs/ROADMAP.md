# O que ficou pendente

> Levantamento de 2026-11-25, feito ao encerrar o redesign do workspace e a jornada Configurar. Junta num lugar só o que
> os roadmaps de cada área deixaram em aberto. O detalhe, o critério de aceite e o histórico continuam no documento de
> origem, citado em cada item. Quando um item for entregue ou decidido, atualize a origem e tire a linha daqui.

## 1. Em andamento

| Item | Situação | Onde |
| --- | --- | --- |
| Fluxos internos de Gestão (Portfólio, Acompanhamento, Assistente, Relatório) | Entregues em G-1 a G-7. Sobra a validação com dados reais, que depende de o dono vincular e sincronizar uma ferramenta (DG5): portfólio com pontos de atenção, faixa e abas do Acompanhamento com números, e o Relatório com dados | [workspace-unificado.md §10](redesign/workspace-unificado.md) |
| Fluxos internos de Configurações (Modelos de IA, Diagnóstico, Sobre) | Entregues em CF1 a CF5 (#215 a #221). Sobram: ver os estados que exigem uma chave real aceita ou recusada por um provedor (provedor conectado, com erro, finalidades, remover, trocar a chave), a inteligência local indisponível e o botão "Reiniciar o motor" na tela dos modelos sem o app ter sido visto com ele; e a cor da ajuda do `TextField`, que na biblioteca é a cor de destaque e parece link ou sucesso | [configuracoes.md](redesign/configuracoes.md) |
| Configurar: T6, P4, P5, P6, PL2 e RS3 | Sessão própria, um PR | [configurar.md](redesign/configurar.md) |
| Aba IDEs em Conexões e resíduos da etapa 2 do ciclo 2 | Um commit na branch `feat/ciclo2-etapas-1-2` do GitHub que nunca entrou no master (o PR #137 foi mergeado antes dele). Precisa de rebase; parte já foi feita por outros PRs (a reversão com `ConfirmAction` entrou no C2 do Configurar) | [SPOCK_IMPLEMENTATION_ROADMAP.md](plataforma/SPOCK_IMPLEMENTATION_ROADMAP.md) |

## 2. Bloqueado pela chave de modelo do dono

Nada rodou ainda com um modelo real. Tudo abaixo espera uma chave com limite de gasto.

- **H0-7 / W2-4**: `scripts/live-validation.py` com orçamento pequeno; relatório com fluxos, tokens, latência e falhas.
  Fontes: [hub](engineering-intelligence-hub/ROADMAP.md), [evolução](ai-engineering-platform/SPOCK_EVOLUTION_ROADMAP.md),
  [implementação](plataforma/SPOCK_IMPLEMENTATION_ROADMAP.md) (etapa 1 do ciclo 2: linha de base das evals).
- **W4-1, W4-2 e W4-3 pelo motor** (modo Chave): juiz com modelo e estabilidade do `project.design` em várias rodadas. O
  modo Sessão já está feito. Mesmo item que o **MM-5** do [desenho dos modos](modos-de-modelo/DESENHO.md).
- **C5 do hub**: homologação multi-IDE.
- **Grupos da [validação real](validacao-real.md)** ainda não rodados: modelo e assistente, evals de gestão, fase A (Da
  ideia ao projeto) e a investigação da fase G com modelo.

## 3. Decisões do dono

| Área | Decisões abertas | Onde |
| --- | --- | --- |
| Hub de inteligência | DD-1, DD-2, DD-3, DD-4, DD-7, DD-8, DD-9, DD-10 (a DD-5 já foi decidida pela ADR-0009, mas a tabela ainda a mostra pendente) | [README do hub](engineering-intelligence-hub/README.md) |
| ADRs | 0002 a 0008 ainda como "proposta, aguarda o dono"; na ADR-0001, D3 (embeddings), D5 (Harness Engineer dentro ou fora do produto), D7 (`workspaceId`) e D8 (`seq` ao vivo) | [adr/](architecture/adr/) |
| Plataforma de engenharia com IA | Cliente MCP (C-3, mesmo item que o I2 do hub); prefixo `spock_` com alias por uma versão | [evolução §4](ai-engineering-platform/SPOCK_EVOLUTION_ROADMAP.md) |
| Modos de modelo | DD-MM-2, DD-MM-3, DD-MM-4; DD-MM-5 sem registro, embora a Onda 4 já tenha nascido no modo Sessão | [DESENHO.md](modos-de-modelo/DESENHO.md) |
| Multiversão | DD-MV-1 (libera MV-2 e MV-8 c/d), DD-MV-2, DD-MV-3 (libera MV-10, .NET 11), DD-MV-4; exclusão do antivírus para as pastas `bin` | [ROADMAP](compatibilidade-multiversao/ROADMAP.md) |
| Ciclo 2 | Jira de teste (1), chave (2), o verificador rodar comandos do projeto (3), congelar entregas de IaC (5), migração de nomes depois da D-12 (6), confirmar o segundo provedor de IA (7); sincronização na lista de execuções; marcador de bloqueio configurável | [implementação](plataforma/SPOCK_IMPLEMENTATION_ROADMAP.md) |
| Licenças e marca | D-02 Terraform (parecer), D-08 (confirmação de dois fornecedores), D-09 EULA (redação), D-10 política (aprovação; o `--enforce` só entra depois), D-11 validador JSON Schema, D-12 marca (parecer) | [LICENSE_DECISIONS.md](compliance/LICENSE_DECISIONS.md) |
| Distribuição | Certificado de assinatura no Windows; macOS fora da primeira versão (exige assinatura, notarização e conta Apple) | [distribuicao.md](distribuicao.md) |
| Workspace unificado | D1 tema claro, D2 jornadas com Playwright no repositório e no CI, D3 área Projetos e organização, D4 painel de atividade aberto por padrão | [workspace-unificado.md §10](redesign/workspace-unificado.md) |
| Auditoria SRP | NET-S09 e RS-17, registradas no fim da auditoria | — |

## 4. Entregas não iniciadas ou parciais

### Plataforma de engenharia com IA e hub

- **W0-1, parcial**: falta o aviso do corte de 24.000 caracteres feito no motor (pede campo novo no contrato).
- **C-1 / F3 MCP v2, parcial**: erros tipados, `tools/list_changed` (também no roadmap de nuvem), contrato das demais
  ferramentas, prefixo `spock_` e `iac_validate` honesto.
- **C-2 telemetria mínima**: não iniciada. **C-4 revisão do catálogo**: contínua, a cada 6 meses.
- **H0-6**: CI de release com `SpockEdition` obrigatório (`-Edition commercial`), junto com as decisões de identidade e
  licença.
- **F1, parcial**: aviso de fork, id na tela e recentes pelo id.
- **F2, parcial**: só existem as intenções `bootstrap` e `architecture`; faltam `development`, `management`,
  `infrastructure`, `investigation` e `delivery` (o mesmo que a etapa 3 do ciclo 2).
- **F4 política por destino**: não iniciada; depende da DD-8.
- **C4** (diagnóstico de IDE e evals EV-CTX): não iniciado; a `contextVersion` nos arquivos projetados volta com ele.
- **E1, E3, D1 a D3**: não iniciados (`project_status`, `git_changes`, `delivery_check` não existem). **D4**: a análise do
  W2-2 cumpre o papel, mas as ferramentas `project_architecture` e `project_rules` não foram criadas.
- **P2/P3 sem início**: Q1, A1, A2, I1, I2, I3, G1, G2, G3, M1, M2, M3, W1 do [hub](engineering-intelligence-hub/ROADMAP.md).
- **Modo CLI (MM-1, MM-3, MM-4)**: não iniciado.

### Produto (ciclo 2 e evolução)

- **Etapas 3, 4 e 5 do ciclo 2**: não iniciadas (contexto por intenção; entrega conferida, equivalente a D1-D3; IaC e
  sincronização na central, escopo por marco, previsibilidade e dependências).
- **Etapa 2, parcial**: `ResourceView` nas sete telas, um só indicador de estado, `Term` em Infraestrutura, aba IDEs (ver
  seção 1) e a conferência no Tauri empacotado.
- **Fase B**: nome de exibição do projeto editável. **Fase D**: resumo do período por IA, indicadores visuais e previsão,
  histórico do assistente, uso agregado de tokens.
- **Execução sem endereço próprio**: o link do caminho da investigação abre a lista, não a execução.
- **Redesign do workspace, o que sobra do pedido original**: a avaliação pelos 13 critérios (§11 do registro) não achou
  critério que falhe; as lacunas são um teste de uso com pessoas (iniciante, desenvolvedor, gestor), o leitor de tela e
  as rodadas com dados do dono (ferramenta vinculada, chave de modelo, tarefa esperando aprovação).
  Fonte: [workspace-unificado.md §11](redesign/workspace-unificado.md).
- **Entrada de pedidos: escolher arquivos do projeto para um pedido**: anexar arquivo foi avaliado e não entra, porque
  nenhum destino o recebe e o motor controla o que vai ao modelo. O caminho seguro é escolher só entre os arquivos que o
  índice já autoriza, com um campo novo no contrato do motor e a prévia de contexto mostrando a escolha. Pede decisão do
  dono. Fonte: [workspace-unificado.md §2D](redesign/workspace-unificado.md).
- **Busca: documentos e assistentes do Harness linha a linha**: avaliados e fora, por falta de destino (não há
  visualizador de documento, e o Catálogo não abre uma entrada). Entram se uma dessas telas passar a existir. Fonte:
  [workspace-unificado.md §4](redesign/workspace-unificado.md).
- **Medição**: a listagem de execuções lê o diário inteiro de cada uma.
- **Adiados**: sincronização agendada, MCP de terceiros no plano do Harness, assistente de engenharia, modo de
  diagnóstico, leitura de `.specs`, coluna "conferido pelo Spock", Azure DevOps, mapa de etapas, escrita em GitHub e Jira,
  bancos, identidade.

### Nuvem

- Login por código de dispositivo; `tools/list` dinâmico.
- Agentes internos lendo a nuvem; novas leituras (VPC, Route53, IAM, Logs, Cost); leitura cruzada com a IaC; escrita fora
  do Terraform; mais provedores; provedores fora do executável. Fonte:
  [SPOCK_CLOUD_CONNECTORS_ROADMAP.md](plataforma/SPOCK_CLOUD_CONNECTORS_ROADMAP.md).

### Multiversão

- **MV-2** catálogo periódico e **MV-8 (c, d)** Actions nos majors atuais e Dependabot: esperam a DD-MV-1.
- **MV-9 bloqueado** por dependência entre pacotes: TypeScript 7 (`typescript-eslint` aceita só `<6.1.0`) e ESLint 10
  (`jsx-a11y` só até `^9`).
- **MV-10** (.NET 11): espera a DD-MV-3.
- Partes deixadas de fora: consulta de dependência arbitrária ao registro e comando de fim de sessão (MV-3);
  `compileSdk` do Android e catálogo de Kotlin, Swift e C++ (MV-4); faixas TS↔typescript-eslint no catálogo (MV-5, MV-6);
  `git init` no scaffold e patch exato no salto de versão.
- xUnit v3: adotar `TestContext.Current.CancellationToken` em 59 chamadas; investigar o teste intermitente
  `SafetyTests ... CLAUDE.MD.tmp`.

### Configurar (problemas sem incremento)

- **T6**: termos técnicos sem `Term` fora de uma lista.
- **P4**: dois nomes para o mesmo projeto. **P5, parcial**: "confiança baixa" não diz o que muda e o rótulo fica grudado
  no comando. **P6**: aviso na etapa 1 pedindo uma escolha que só existe na etapa 2.
- **PL2, parcial**: o prefixo antigo `mariliz.` continua nos ids. **RS3, parcial**: "determinístico · obrigatório" ainda
  aparece no detalhe da verificação.

### Licenças

- Riscos abertos: R-01 (P0), R-05, R-06 (EULA e repasse do VC++), R-07 (link para o fonte dos componentes MPL), R-10,
  R-11, R-12 (atribuição CC-BY), R-13, R-14, R-15 (LICENSE ao lado do OpenTofu), R-17. Fonte:
  [LICENSE_RISK_REGISTER.md](compliance/LICENSE_RISK_REGISTER.md).

## 5. Implementado, mas nunca validado de verdade

- **Integrações**: GitHub e Jira de ponta a ponta (o E2 espera a credencial de teste do Jira); conferência da chave OpenAI.
- **Nuvem**: Azure e Google sem uma identidade de leitura real (a AWS foi validada).
- **Cofre** no Linux e no macOS.
- **IDEs**: Codex e Cursor reais (C3); C2 sem a resposta de um modelo em sessão logada, sem o instalador real e sem o
  `.deb` do lançador; prompts MCP na versão do cliente em uso.
- **Pacotes**: instalação real do NSIS e do `.deb`; pacotes Linux e macOS nunca rodaram, porque o CI está sem orçamento.
- **App empacotado**: indicador do menu, resumo de entrega, seção em Gestão, eventos de sincronização e tema do `Drawer`.
- **Workspace unificado**: leitor de tela real; computador sem projeto recente; tarefa real esperando aprovação; entrada
  de pedidos com modelo e ferramenta de IA instalados; envio de pergunta a uma ferramenta de IA; macOS e Linux; uma tarefa
  pendente de verdade no grupo Atividade da busca; uma ferramenta de IA instalada no grupo Agentes da busca (neste
  computador não há nenhuma).
- **Configurar**: conflito de arquivo, comando destrutivo, plano com proposta do agente, execução que falha ou é
  cancelada, comando de rede aprovado e leitor de tela.

## 6. Documentos com trecho desatualizado

Corrigir quando a área for tocada de novo:

- [README do hub](engineering-intelligence-hub/README.md): a DD-5 aparece pendente.
- [README da multiversão](compatibilidade-multiversao/README.md): diz que a criação de projeto não existe (o MV-6 a fez)
  e marca o MV-2 como parcial.
- [DESENHO.md](modos-de-modelo/DESENHO.md): o MM-2 aparece sem o `prepare`, que já existe (`iac_architecture_prepare`).
- [SPOCK_IMPLEMENTATION_ROADMAP.md](plataforma/SPOCK_IMPLEMENTATION_ROADMAP.md): diz que o C3 (Cursor e Codex) não foi
  feito.
- [IMPLEMENTATION_STATUS.md](compliance/IMPLEMENTATION_STATUS.md): trata D-06 e D-07 como pendentes, e o registro de
  riscos dá os avisos do linux-x64 como faltando, embora o status os dê como cobertos.

## 7. Universal Boot

Entregue em 2026-11-26 e 27 (PRs #231, #234, #235, #237 e #239): fundação, retenção dos registros, capacidades e resumos,
Boots recentes com Retomar, período e quantidade tirados do pedido, `spock_boot` no MCP, modelo como segundo estágio da
intenção, segredo omitido do pedido. O desenho, as evidências e os riscos estão em [universal-boot](universal-boot/README.md)
e na [ADR-0010](architecture/adr/0010-universal-boot.md). Este é o único lugar onde as etapas e o que falta validar ficam
registrados; a seção 7.1 lista cada pendência com o que ela exige e como provar.

| Etapa | Situação | O que falta | Depende de |
| --- | --- | --- | --- |
| 0. Auditoria e arquitetura | Feita; o cartão do Início e a lista de Boots foram conferidos no aplicativo aberto | — | — |
| 1. Fundação | Feita: registro, ciclo de vida, checkpoints, recuperação, catálogo de 45 fichas, reconhecimento, permissão, plano, Project Boot, Request Boot; na Home, lista dos últimos Boots do projeto com Retomar e detalhe, conferidos no aplicativo aberto com um projeto temporário | BP-05 (projeto real), BP-06 (queda real) | — |
| 2. Capacidades do produto | Parcial: um fluxo real por domínio, só leitura e análise executam; toda capacidade direta tem resumo próprio (conferido contra a fixture do contrato) e há 45 fichas, com histórico de riscos, revisões do Harness, estado, geração e drift de infraestrutura; período e quantidade tirados do pedido (indicadores, relatório, linha do tempo, auditoria, execuções); `spock_boot` no MCP (BD-3, só leitura, nível 0, 13 leituras do projeto; falta uma IDE de verdade chamar a ferramenta); reconhecimento por modelo como segundo estágio (BD-1) implementado com dublê, só onde o léxico não resolve, com a finalidade própria `boot`, teto de tokens e a mesma eval como régua | BP-02 (medir o modelo), BP-03 (IDE real), BP-04 (app com modelo e MCP), BP-08 (envio duplo no MCP), BP-09 (mais leituras no MCP), BP-10 (filtros), BP-11 (opções ambíguas) | Chave de modelo (H0-7), uma IDE logada com o lançador `spock`, decisões por capacidade |
| 3. Work Intelligence | Não iniciada | Gatilho de ingestão de backlog; sincronizador de roadmap; Harness State incremental; grafo de dependências e Dependency Resolution Brief; avaliação de prontidão do item | BP-14: sincronização agendada (hoje adiada); dependências e responsáveis (hoje inexistentes em Gestão); publicar o Brief num card exige **escrita em Jira ou GitHub**, o que contraria a decisão atual de só leitura |
| 4. Engenharia autônoma | Não iniciada | Work Boot, seleção de tarefas, orquestrador do Harness, execução de agentes com escrita, juízes e Pull Request | BP-15: etapa 3; nível 3 de autonomia com política explícita (ADR-0007); fronteira "a IA propõe, o motor decide" revista; validação com modelo real (H0-7) |
| 5. Automação empresarial | Não iniciada | Gatilhos contínuos, automação por organização, concorrência, priorização, indicadores gerenciais do Boot, evals contínuos | BP-16: identidade e organização (DD-2, inexistentes); etapa 4 |

Decisões do dono: BD-1 (modelo no reconhecimento), BD-2 (texto do pedido guardado com a linha de segredo omitida) e BD-3
(`spock_boot` no MCP) foram tomadas em 2026-11-27 e estão implementadas; a do escopo da auditoria também (só o Boot de
projeto é auditado). Aberta: BD-4 (identidade e retenção da auditoria, BP-01), em [universal-boot §6](universal-boot/README.md).

### 7.1 O que falta validar, e do que cada item depende

A ideia é voltar a esta lista em outro momento e fechar um item por vez: cada linha diz o que precisa existir antes (a
dependência, que muitas vezes é do dono) e o critério de aceite. Um item só sai da lista com o resultado registrado no
documento de origem e a linha daqui removida ou marcada como feita. O detalhe de cada rodada com dado real está em
[validacao-real.md](validacao-real.md).

| Id | Pendência | Depende de | Como validar (critério de aceite) |
| --- | --- | --- | --- |
| BP-01 | Decisão BD-4: o registro do Boot e a auditoria passam a guardar a pessoa, com política de retenção | O dono; só faz sentido com identidade (DD-2, hoje inexistente) | Decisão escrita em [universal-boot §6](universal-boot/README.md); com identidade, o registro e a auditoria trazem a pessoa e há teste da retenção |
| BP-02 | Medir o segundo estágio de modelo com modelo real | Chave de modelo com limite de gasto (H0-7) e rede | Rodar `BootIntentModelEvalTests` (holdout ao vivo, passo a passo em [validacao-real.md](validacao-real.md)). Aceite: acerto com modelo não pior que sem modelo no holdout e no corpus de ajuste; no máximo 1.300 tokens de entrada e 120 de saída por pedido; resposta dentro de 20 s; poucas respostas `failed`. Saída: manter ligado, desligar, ou estender o estágio às resoluções de pontuação baixa (os 4 erros atuais do holdout são resolução errada e o estágio não os alcança) |
| BP-03 | Uma IDE de verdade chamando `spock_boot` | Lançador `spock` no PATH (instalador real, C2); `.mcp.json` do projeto com o servidor `spock` (Harness aplicado para claude-code); IDE logada. Cursor e Codex dependem do C3 e do C5 do hub | Na IDE: a ferramenta aparece; `mode: project` num projeto sem perfil diz que o perfil falta; um pedido de leitura responde; um pedido que altera algo volta como "abre a tela" sem executar; um pedido hostil não executa nada; a auditoria mostra `ide.boot` sem o texto do pedido; nada é gravado no projeto |
| BP-04 | O aplicativo aberto com o modelo e com o MCP | BP-02 (chave) e um build do motor com a inteligência empacotada (`MARILIZ_BUILD_PYTHON` no Python 3.14) | Repetir a rodada do app com CDP (motor real com `scripts/build-runtime.ps1`, `tauri dev` com `--remote-debugging-port`, pasta de dados nova em `MARILIZ_DATA_DIR`; o que a rodada anterior cobriu está em [universal-boot §4](universal-boot/README.md)): o cartão mostra "Entendido com ajuda do modelo" para um pedido que o léxico não resolve; estágio com falha cai no léxico sem erro visível; o registro traz o gasto; uma chamada de `spock_boot` com o app aberto, escrevendo em `boots/` ao mesmo tempo, não corrompe nenhum registro |
| BP-05 | Project Boot num projeto real do dono | O dono escolher um projeto (só leitura, nada de escrita) | Hash dos arquivos do projeto idêntico antes e depois; o relatório bate com o que o dono conhece (tecnologias, Harness, limitações); o perfil só aparece na pasta de dados; tempo aceitável |
| BP-06 | Queda real do processo no meio de um passo | Nada externo, só a máquina | Matar o motor durante um Boot demorado (análise de um projeto grande, `aitool.list`) e subir de novo: o Boot aparece como interrompido com Retomar, e a retomada não repete o que já foi feito. Hoje só foi simulado gravando o registro |
| BP-07 | Boot e MCP no Linux e no macOS | Máquinas reais; macOS está fora da primeira versão (ADR-0001, D2) | Rodar `BootProcessTests`, `McpBootProcessTests` e uma rodada do aplicativo em cada sistema |
| BP-08 | O MCP não barra o envio duplo: duas chamadas seguidas criam dois Boots | Decisão (serializar por projeto ou aceitar) | Teste concorrente pelo MCP com a regra escolhida |
| BP-09 | Mais leituras no MCP (decisões, relatório, linha do tempo, execuções, conhecimento, agentes, modelos, infraestrutura) | Serviços de leitura sem escrita e sem processo (hoje esses tratadores gravam ou sobem processo) e uma decisão por capacidade | Para cada uma: teste que compara o JSON do MCP com o do motor, escopo por projeto, auditoria `ide.boot` e a capacidade deixando de aparecer como indisponível |
| BP-10 | Filtros por tipo e por estado nos parâmetros do pedido (execuções por estado, linha do tempo por tipo, auditoria por categoria) | Nada externo | Extrator e faixa com teste, casos novos na eval de intenção, e a nota no passo como nos parâmetros de hoje |
| BP-11 | O cartão do pedido ambíguo oferece opções que levam a "falta algo" | Decisão de UX (esconder, desabilitar ou ordenar as indisponíveis) | No app: o cartão só oferece o que dá para fazer, ou marca claramente o que não dá |
| BP-12 | Acabamentos vistos no app: "16 órfãos" no índice recém-criado (o catálogo de referência embutido é contado como órfão); a frase "1 execução: 0 em andamento e 0 aguardando aprovação"; âncoras internas (`#h:6.5-...`) na tela da execução (fora do Boot) | Nada externo | Captura e DOM do cartão e da tela da execução depois do ajuste |
| BP-13 | Reconhecimento léxico em frase nova (71% no holdout) | Frases de uso real, escritas sem olhar o reconhecedor, para formar um holdout novo (o atual já foi usado para decidir) | Holdout novo medido uma vez; ampliar fichas e termos só no corpus de ajuste |
| BP-14 | Etapa 3 (Work Intelligence): gatilho de backlog, sincronizador de roadmap, Harness State incremental, grafo de dependências, Dependency Resolution Brief, prontidão | Decisão sobre **escrita em Jira ou GitHub** (hoje só leitura); sincronização agendada; dependências e responsáveis em Gestão. A primeira fatia, que só lê (grafo a partir dos vínculos "is blocked by" já sincronizados), não depende da escrita | Por entrega, com os critérios do desenho da etapa; começar pelo grafo de dependências lido e pela avaliação de prontidão, sem publicar nada |
| BP-15 | Etapa 4 (engenharia autônoma) | BP-14; nível 3 de autonomia com política explícita (ADR-0007); a fronteira "a IA propõe, o motor decide" revista; modelo real (H0-7) | Por entrega; o padrão é implementar automaticamente com revisão humana antes do merge |
| BP-16 | Etapa 5 (automação empresarial) | Identidade e organização (DD-2); BP-15 | Por entrega |
| BP-17 | Teto de gasto do estágio de modelo por período (hoje o teto é por chamada) | Decisão de política de custo | Teste do teto e do aviso ao atingi-lo |
| BP-18 | Build do motor com a inteligência empacotada no Python 3.14 (`MARILIZ_BUILD_PYTHON`) | Já validado no aplicativo (a inteligência subiu e o índice respondeu); falta o interpretador embarcado ser refeito do zero | Build limpo do pacote e do instalador, e a rodada do app depois dele |

## 8. Engineering Quality Intelligence (Q0 a Q7)

Desenho, contratos e o que está provado em [quality](quality/README.md); decisão na
[ADR-0011](architecture/adr/0011-engineering-quality-intelligence.md). Uma onda por vez, cada uma com o seu PR; a ordem só muda
com dependência técnica comprovada. A referência é a categoria das ferramentas de revisão por IA, sem dependência de nenhuma.

| Onda | Situação | Prioridade |
| --- | --- | --- |
| Q0 Estabilização da fundação | Parcial: o PR #231 e os seguintes do Boot estão mergeados e a suíte do Boot passa; as pendências BP-02 a BP-07 da seção 7.1 seguem abertas | Alta |
| Q1 Revisão nativa | **Primeira fatia entregue**: leitor de alterações, revisão das alterações locais e de uma branch, contrato de achados, 15 regras, juízes determinísticos, histórico, tela e Boot; faltam o Requirements Judge e o contexto de código por diff | Alta |
| Q2 Multi-juiz e execução de qualidade | **Parcial (2026-11-28):** revisão por modelo e conferência de critérios (hipóteses com evidência no diff), verificações do projeto com autorização, decisões por achado, assinatura da entrega e pedido de correção. Faltam o Regression e o Dependency Judge mais finos, outras linguagens no catálogo de verificações e a rodada ao vivo no gateway | Alta |
| Q3 Segurança | Não iniciada | Média |
| Q4 Governança de regras | Não iniciada; espera a QD-1 | Média |
| Q5 Inteligência de software | Não iniciada | Média |
| Q6 Revisão de Pull Request | Não iniciada | Baixa |
| Q7 Qualidade autônoma | Não iniciada; espera Q2 e a etapa 4 do Boot | Baixa |

### Q0 — Estabilização da fundação

- **Objetivo:** só ampliar a qualidade sobre um Boot que se prova.
- **Escopo:** fechar BP-02 (medir o estágio de modelo), BP-03 (IDE real chamando `spock_boot`), BP-04 (aplicativo com modelo e
  MCP), BP-05 (Project Boot num projeto real), BP-06 (queda real). Um classificador léxico só (hoje há mais de um); nada de
  reconhecimento por modelo sem medir o léxico antes. Linha de base das evals com modelo real (H0-7).
- **Aceite:** os critérios de cada BP em 7.1; uma capacidade nova (como a de revisão) se registra, é reconhecida, tem a
  disponibilidade avaliada e executa com a permissão certa, provado por teste e no aplicativo aberto.
- **Depende de:** chave de modelo, IDE logada, projeto real escolhido pelo dono. **Risco:** medir o holdout já usado.
- **Evidência esperada:** relatórios em [validacao-real.md](validacao-real.md).

### Q1 — Revisão nativa (entregue em parte)

- **Entregue:** `quality.review.start/list/detail`; Working Tree Review e Branch Review (base sempre explícita); achados com
  arquivo, linha, evidência e confiança por critério; deduplicação, descarte de linha que mudou, gerado/binário/terceiros
  fora da leitura; juízes de Segurança, Arquitetura, Qualidade, Regressão e Entrega; relatório, histórico e tela `/qualidade`;
  capacidades `quality.review_changes` e `quality.review_branch`; eval com gabarito (`code-review.json`).
- **Falta:** Commit Review; Requirements Judge (precisa de uma atividade ligada à alteração); contexto de código ao redor do
  diff além do hunk; leitor de `.gitattributes` (`linguist-generated`); revisão de arquivos fora da pasta do projeto.
- **Aceite do que falta:** cada item com teste sobre um repositório git de verdade e uma linha no [quality](quality/README.md).
- **Risco:** os detectores são padrões e não medem qualidade; sem linha de base sobre alterações reais, a eval só impede regressão.
- **Evidência esperada:** uma revisão de alterações reais do dono, comparada com o que a pessoa teria apontado.

### Q2 — Multi-juiz e execução de qualidade

- **Objetivo:** juízes que dependem de dados novos e verificações que rodam de verdade.
- **Escopo:** revisão por modelo (finalidade própria, teto de tokens, resposta em contrato, achado só vale com arquivo, linha e
  trecho que existem no diff, sempre como hipótese); Regression, Dependency e Final Delivery com mais critérios; catálogo de
  ferramentas de qualidade por stack (comando, versão, diretório, prazo, risco); execução de build, testes, lint e typecheck
  com lista fechada, aprovação e limites, sem afirmar isolamento que o processo filho não tem.
- **Aceite:** execução bloqueada por falta de ferramenta nunca é aprovada; comando só vem do projeto ou do usuário; relatório
  com comando, versão, duração, saída redigida e estado (`passed`, `failed`, `blocked`, `not_run`, `timed_out`, `cancelled`).
- **Depende de:** QD-2, QD-3, a decisão 3 do ciclo 2 e a chave de modelo. **Risco:** executar código do projeto na máquina da pessoa.
- **Evidência esperada:** rodada real num projeto .NET e num React, mais a comparação de modelos nos mesmos casos.

### Q3 — Segurança

- **Escopo:** SAST (avaliar licença e cobertura por stack antes de escolher), SCA (dependências diretas e transitivas, CVEs,
  abandono, licenças, a partir de fontes públicas), normalizados no mesmo contrato de achados, sem duplicar o scanner atual.
- **Aceite:** o catálogo de compatibilidade de versões não é tratado como SCA; ferramenta só entra com plano, origem, licença e aprovação.
- **Depende de:** Q2 (execução). **Risco:** licença e taxa de ferramentas; falso positivo de dependência.

### Q4 — Governança de regras

- **Escopo:** modelo canônico de regra (id, escopo, severidade, tipo de validação, modo informativo/aviso/bloqueante,
  exceção com responsável, histórico); hierarquia organização, portfólio, projeto, repositório, diretório, stack e tipo de
  atividade, com precedência e sem regra local contornando uma obrigatória; medição de conformidade; o Harness segue
  projetando regras nas IDEs. O modelo nunca altera regra obrigatória; sugestão de regra passa por revisão humana.
- **Aceite:** as regras `QR-*` viram o primeiro conjunto do modelo; uma exceção autorizada fica registrada e aparece no relatório.
- **Depende de:** QD-1 e de identidade/organização (DD-2) para as camadas de cima. **Risco:** criar um segundo sistema de configuração.

### Q5 — Inteligência de software

- **Escopo:** evals de contexto de código (Recall@K, MRR, precisão, contaminação, frescor, cobertura de símbolos, dependências,
  context fit) antes de qualquer embedding ou mudança de armazenamento; análise estrutural por AST onde compensar; contexto
  multirrepositório só com controle de acesso entre projetos, sem afrouxar o isolamento de hoje.
- **Aceite:** comparação com o SQLite FTS5 atual no mesmo dataset antes de trocar qualquer coisa.
- **Depende de:** Q1 (dataset de revisão) e de uma decisão do dono sobre escopo entre projetos. **Risco:** vazar contexto entre projetos.

### Q6 — Revisão de Pull Request

- **Escopo:** leitura do PR no GitHub (metadados, base e head, arquivos, diff, comentários, checks); contrato de escrita
  separado do de leitura e desligado por padrão (escopo de credencial, identidade, aprovação, auditoria, idempotência e
  controle de duplicação); Azure DevOps só se for suportado; adaptador opcional e isolado de uma ferramenta de código aberto,
  depois de avaliar licença, manutenção, segurança, consumo de modelo e substituição pelo mecanismo nativo.
- **Aceite:** nenhum comentário sem autorização; a ausência do adaptador não tira nada do núcleo.
- **Depende de:** a decisão sobre escrita em GitHub e Jira (BP-14). **Risco:** permissão de escrita ampliada globalmente.

### Q7 — Qualidade autônoma

- **Escopo:** devolver os achados ao agente que implementou, ciclo de correção com limite de tentativas, revisão e gates de
  novo a cada volta, histórico por ciclo; o agente não altera o resultado dos juízes, não remove teste nem enfraquece regra,
  e não corrige fora do escopo original; integração com o Work Boot e o Boot autônomo; monitoramento e evals contínuos.
- **Aceite:** achados corrigíveis sozinhos e os que exigem pessoa definidos por regra; o ciclo termina em aprovado, no limite
  ou como inconclusivo.
- **Depende de:** Q2, a etapa 4 do Boot (BP-15) e o nível 3 de autonomia com política explícita (ADR-0007). **Risco:** autonomia sem freio.
