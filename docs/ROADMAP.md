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

Fundação entregue em 2026-11-26 (branch `feat/universal-boot-fundacao`). O desenho, as evidências e os riscos estão em
[universal-boot](universal-boot/README.md) e na [ADR-0010](architecture/adr/0010-universal-boot.md). Este é o único lugar
onde as etapas ficam registradas.

| Etapa | Situação | O que falta | Depende de |
| --- | --- | --- | --- |
| 0. Auditoria e arquitetura | Feita | Conferir o cartão do Início no aplicativo aberto | — |
| 1. Fundação | Feita: registro, ciclo de vida, checkpoints, recuperação, catálogo de 35 fichas, reconhecimento, permissão, plano, Project Boot, Request Boot | Botão para recuperar um Boot interrompido; lista de Boots na tela; rodar o Project Boot no aplicativo com um projeto real | — |
| 2. Capacidades do produto | Parcial: um fluxo real por domínio, só leitura e análise executam | Resumos próprios para mais capacidades; ficha para estado, drift e geração de infraestrutura; parâmetros por capacidade (período, filtros); `spock_boot` no MCP (BD-3); reconhecimento por modelo como segundo estágio (BD-1) | BD-1, BD-3; chave de modelo (H0-7) para medir |
| 3. Work Intelligence | Não iniciada | Gatilho de ingestão de backlog; sincronizador de roadmap; Harness State incremental; grafo de dependências e Dependency Resolution Brief; avaliação de prontidão do item | Sincronização agendada (hoje adiada); dependências e responsáveis (hoje inexistentes em Gestão); publicar o Brief num card exige **escrita em Jira ou GitHub**, o que contraria a decisão atual de só leitura |
| 4. Engenharia autônoma | Não iniciada | Work Boot, seleção de tarefas, orquestrador do Harness, execução de agentes com escrita, juízes e Pull Request | Etapa 3; nível 3 de autonomia com política explícita (ADR-0007); fronteira "a IA propõe, o motor decide" revista; validação com modelo real (H0-7) |
| 5. Automação empresarial | Não iniciada | Gatilhos contínuos, automação por organização, concorrência, priorização, indicadores gerenciais do Boot, evals contínuos | Identidade e organização (DD-2, inexistentes); etapa 4 |

Decisões do dono desta entrega: BD-1 (modelo no reconhecimento), BD-2 (guardar o texto do pedido), BD-3 (Boot no MCP) e BD-4
(identidade e retenção), em [universal-boot §6](universal-boot/README.md).
