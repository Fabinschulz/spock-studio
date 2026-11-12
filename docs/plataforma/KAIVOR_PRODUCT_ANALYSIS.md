# Kaivor: análise de produto de referência

**Data:** 2026-11-12. Documento 1 da análise de referência. Os demais: `MARILIZ_CURRENT_STATE.md`,
`KAIVOR_SPOCK_COMPARISON.md`, `SPOCK_PRODUCT_EVOLUTION.md`, `SPOCK_DESIGN_DIRECTION.md`,
`SPOCK_IMPLEMENTATION_ROADMAP.md`.

O objetivo aqui é entender o Kaivor como produto: o que ele faz, como se organiza, o que funciona e o que ele mesmo
admite que não funciona. Nada deste documento é proposta para o Spock Studio; as propostas estão nos documentos 4 a 6.

## 1. Fontes e método

| Fonte | O que é | Como foi lida |
| --- | --- | --- |
| Site | `https://kaivorai.com/` (inclui `#automacoes` e a prévia de produto com abas) | Navegador, em 2026-11-12. O site anuncia a versão beta 0.2.89. |
| Documentação pública | `https://www.kaivorai.com/docs` (26 seções, cada uma com data de verificação entre 17 ago e 6 set 2026) | Navegador, texto integral de 20 seções. |
| Repositório local | `C:\Users\fabio\Desktop\MyApps\frontend\kaivor` (versão 0.2.95, Electron e React) | Leitura de código, specs (`specs/`), mapa de produto (`KAIVOR_PRODUCT_MAP.md`), guia, `DESIGN.md` e documentos de auditoria e validação em `docs/`. |

Limites do método:

- **O aplicativo não foi executado.** Não houve login, instalação nem uso real. Todo comportamento descrito vem de
  código, specs e documentação. Fluxos de tela são reconstruídos a partir de componentes, textos de interface e specs.
- Os dois maiores arquivos do Kaivor (`src/main.tsx`, com 10.488 linhas, e `electron/main.cjs`, com 18.631) foram lidos
  por busca dirigida, não por inteiro. Das 395 specs, só uma parte pequena foi lida (as donas das capacidades
  analisadas); a maioria não foi aberta.
- Nenhum código, imagem ou texto de interface do Kaivor foi copiado para o Spock. As capturas de tela de referência são
  citadas pelo caminho no repositório do Kaivor ou pela seção do site, e não foram trazidas para cá.

Classificação usada em todo o documento:

- **Confirmado:** visto no código, ou em spec marcada como implementada com o código conferido, ou afirmado na
  documentação pública e coerente com o código.
- **Parcial:** há evidência (spec, texto de interface, mapa de produto), mas o comportamento completo não foi conferido,
  ou o próprio Kaivor marca como parcial.
- **Inferência:** interpretação a partir de sinais observáveis.
- **Não identificado:** foi procurado e não foi encontrado.

Uma ressalva vale para o documento inteiro: o mapa de produto do Kaivor marca muitas capacidades como implementadas
com a nota "validação com conta real pendente". Implementado, aqui, quer dizer "existe código", não "comprovado em uso".

## 2. O que é o produto

| Achado | Fonte | Confiança |
| --- | --- | --- |
| O Kaivor se define como plataforma de automação e desenvolvimento guiada por um bot autônomo e um hub de conexões. A pessoa descreve um objetivo; o produto planeja, executa com recursos autorizados, verifica e mostra evidências. | `KAIVOR_PRODUCT_MAP.md` seção 1; site (herói) | Confirmado como posicionamento |
| Dois tipos de trabalho no mesmo produto: automação operacional (consultar serviços, gerar relatórios, agir em aplicativos) e desenvolvimento de software (pipeline de agentes que vai de objetivo a spec, plano, código, teste e revisão). | Mapa de produto seção 1 | Confirmado |
| Funciona com ou sem IA: requisições HTTP salvas, operações de banco, ferramentas MCP e agentes determinísticos rodam sem provedor de IA. | Mapa seção 1; docs "Automatize sem gastar tokens" | Confirmado |
| Dois hospedeiros: aplicativo desktop (ações rodam na máquina) e aplicativo web (ações rodam em servidor, sem acesso local). O produto diz onde está rodando ("Neste computador" ou "Servidor"). | Mapa seção 1; site | Confirmado no desktop; web marcada como parcial pelo próprio mapa |
| Diz explicitamente o que não é: não é IDE, não substitui as CLIs de IA (orquestra as instaladas), e modelos não concedem permissões a si mesmos. | Mapa seção 1 | Confirmado |
| Modelo comercial: plano Local e Pro; Database Studio, HTTP Client, MCP manual e fluxos agendados são Pro. Há trial de 3 dias e plano Enterprise por token de empresa. | Docs "Plano Local e Pro" e "Empresas e assentos Enterprise" | Confirmado |

Princípios declarados pelo produto (mapa seção 2): resultado antes de configuração; autonomia limitada por concessões,
orçamentos e prazos; execução transparente; recuperação durável com identificadores estáveis de ação; conexões
configuradas uma vez e concedidas por recurso; eficiência de contexto.

## 3. Navegação e organização

| Achado | Fonte | Confiança |
| --- | --- | --- |
| Navegação em três níveis: cinco itens fixos orientados a resultado (Bot, Tarefas, Rotinas, Conexões, Notas); um menu "Ferramentas" com Execuções, Agentes, Cortex, Cérebro, Databases, HTTP Client, Plugins, Kanban e Tokens, cada uma fixável na barra; e busca por Ctrl/Cmd+K que cobre tudo. | `src/hub-navigation.tsx`; mapa seção 3 | Confirmado |
| Rail de 60 px recolhido e 200 px expandido; faixa superior de 48 px; rodapé de status de 28 px. | `DESIGN.md` | Confirmado |
| Itens de navegação carregam estado vivo: contador de execuções ativas, atividade de banco, e uma seção "Em execução" com até cinco execuções, etapa atual e tempo. | `src/features/executions/running-runs-section.tsx` | Confirmado |
| Projetos aparecem na barra lateral como pastas com conversas aninhadas; menu de contexto para renomear, arquivar e fechar, acessível também por teclado. | `hub-navigation.tsx`; spec `feature-clean-workspace-v1` | Confirmado |
| Configurações é um modo do shell: a navegação principal dá lugar a uma navegação de seções, com trilha ("Configurações / Modelos") e busca própria. | `src/features/desktop-workspace/settings-shell.tsx` | Confirmado |
| Painéis redimensionáveis por ponteiro e por teclado, com "restaurar layout"; terminal com quatro posições de encaixe; divisão conversa e arquivo que vira tela cheia abaixo de 800 px. | `src/shared/use-pane-drag.ts`; docs "Chat, arquivos e espaço de trabalho" | Confirmado |
| Contexto sempre à vista no campo de mensagem: projeto, branch, modelo, nível de acesso. | `src/features/desktop-workspace/access-chooser.tsx` | Confirmado |
| A documentação pública descreve uma navegação anterior (Chat, Conexões, Automações, Execuções, tela Captura). O código atual já renomeou para Bot, Tarefas, Rotinas e removeu a Captura do produto. | Docs "Sua primeira tarefa" contra `hub-navigation.tsx` e mapa seção 4.11 | Confirmado. A documentação pública está defasada em relação ao aplicativo. |
| Como o produto junta muitas ferramentas sem parecer vários aplicativos: um só shell, um só padrão de painel lateral, o mesmo conceito de projeto reutilizado entre banco e HTTP, uma central de execuções única e uma página de conexões que funciona como índice. | Specs `feature-http-client-v1`, `feature-execution-center-v1`; `ConnectionsHome` | Confirmado no shell; Parcial dentro das ferramentas (o Database Studio tem 24 folhas de estilo próprias) |

## 4. Hub de conexões e integrações

### 4.1 Entrada e categorias

| Achado | Fonte | Confiança |
| --- | --- | --- |
| "Conexões" abre uma página com cinco portas: Aplicativos e contas, Minha IA, Bancos de dados, APIs e requisições, e Usar o Kaivor em outro aplicativo. O texto de apoio é a regra do produto: configurar uma vez, escolher depois onde cada recurso pode ser usado; conectar uma conta não autoriza todas as ações. | `ConnectionsHome` em `src/hub-navigation.tsx` | Confirmado |
| A página só roteia. Não existe um cadastro único de conexão: cada categoria tem tela, armazenamento e formulário próprios. | Código e spec `feature-guided-hub-experience-v1` | Confirmado |
| Catálogo com 48 conectores em seis grupos (mensagens, Google Workspace, Microsoft 365, vendas e atendimento, projetos e documentos, código e operações). Inclui Jira Cloud, GitHub, GitLab, Azure DevOps, Linear, ClickUp, Notion, Sentry, Datadog, AWS e Google Cloud. | `src/shared/plugin-catalog.cjs` | Confirmado |
| O catálogo não tem campo de maturidade por conector. Todos aparecem como conectáveis; o estado real só existe no mapa de produto e nas specs. | Mesmo arquivo; mapa seção 4.7 | Confirmado |
| Lista ordenada por utilidade: conectados primeiro, depois "Comece por aqui" com seis destaques, depois por categoria; busca e filtros por categoria; nomes de operação em linguagem simples. | `src/features/plugins/plugin-catalog-view.tsx` | Confirmado |

### 4.2 Credenciais

| Achado | Fonte | Confiança |
| --- | --- | --- |
| A conexão é validada com uma chamada real antes de ser salva. | `electron/main/application/integration/external-provider-service.cjs` | Confirmado |
| Mecanismos: token ou API key colado (maioria); token pessoal com modo de acesso por conexão, leitura ou leitura e escrita (GitHub, GitLab, Azure DevOps); OAuth com PKCE e retorno em loopback (Google, Microsoft 365); conta de serviço (Google Cloud); perfis de CLI (AWS); CLI gerenciada pelo próprio aplicativo (`gh`, `glab`); QR (WhatsApp). | `git-provider-service.cjs`, `connector-oauth-service.cjs`, specs dos conectores | Confirmado |
| Segredo guardado pelo armazenamento seguro do sistema no processo principal; a interface recebe só metadados. Vários serviços recusam funcionar quando a criptografia do sistema não está disponível. | `electron/main.cjs` (`encryptSecret`) | Confirmado |
| Chaves de IA ficam num cofre de provedores e são injetadas como variável de ambiente só no processo filho que precisa delas. | `electron/main.cjs` (`buildProviderEnv`) | Confirmado |
| Um login Microsoft autoriza todos os serviços 365 escolhidos de uma vez; "usar conexão existente" reaproveita o token de um conector já configurado sem que ele passe pela interface. | Specs `feature-microsoft-unified-sign-in-v1`, `feature-git-cli-plugins-v1` | Confirmado no código; validação com conta real pendente |

### 4.3 Teste, estados e ciclo de vida

| Achado | Fonte | Confiança |
| --- | --- | --- |
| Ações de interface: testar leitura, renovar credencial, desconectar, adicionar outra conexão do mesmo provedor (até 32). | `external-provider-service.cjs`; textos de interface | Confirmado |
| Desconectar revoga as concessões de agentes e de consumidores externos ligadas àquela conexão. | `agent-external-plugin-access.cjs` | Confirmado |
| Não há estado de saúde persistido por conexão. Uma credencial vencida só é percebida na próxima execução, como erro de autenticação. | `external-provider-service.cjs` | Confirmado |
| Chaves de IA por API não são validadas ao salvar (o produto declara que evita chamadas e custos). Só provedores locais têm teste, com estados não configurado, configurado, offline, online e pronto. | `electron/main.cjs` (`testProviderConnection`); docs "Configurar uma IA" | Confirmado |
| Aviso proativo de credencial prestes a vencer e reconexão automática. | Busca no código | Não identificado |
| Histórico de uso por conexão (quem usou, quando, qual operação). | Busca no código | Não identificado. Existe apenas "último uso" por consumidor externo. |

### 4.4 Como as ferramentas ficam disponíveis

| Achado | Fonte | Confiança |
| --- | --- | --- |
| O conceito central é a **capacidade**: uma operação com identificador estável, impacto declarado e recursos. O mesmo identificador vale em conversa, agente, diagrama, automação e MCP. | Spec `feature-plugin-complete-surface-contract-v1` | Confirmado como desenho |
| Esse contrato não está cumprido para todo o catálogo: seis das oito tarefas da spec seguem abertas. | Mesma spec | Confirmado |
| Cada operação é classificada como leitura, escrita ou sensível (apagar, enviar, compartilhar, cancelar, pagar, publicar, mesclar). Nome não reconhecido cai em sensível. | `src/shared/plugin-operation-access.cjs` | Confirmado |
| O recurso salvo é a fronteira de autorização: o consumidor escolhe identificadores aprovados (uma requisição salva, uma conexão de banco), nunca informa URL, SQL ou comando. | Specs `feature-http-client-v1`, `feature-agent-database-access-v1` | Confirmado |

### 4.5 Permissões

| Achado | Fonte | Confiança |
| --- | --- | --- |
| Vocabulário: capacidade, consumidor, concessão, invocação. Uma concessão é um par de capacidade e lista de recursos exatos. | Spec `feature-local-integration-gateway-v1` | Confirmado |
| Seletor de concessões agrupa por capacidade, mostra "Leitura" ou "Requer revisão", lista recursos por nome e esconde a descrição técnica. Credencial externa nova começa sem nenhuma concessão. | `src/integration-grant-picker.tsx` | Confirmado |
| Aprovação é um ato do aplicativo: manual por padrão; "confiável" é escolha explícita por consumidor; operação que altera dados ou de impacto desconhecido entra em espera. O consumidor nunca aprova a si mesmo. | `electron/main/application/integration/local-gateway.cjs` | Confirmado |
| Por agente: ao habilitar um conector, leitura e escrita comum entram juntas; o sensível é marcado um a um. | Textos em `src/main.tsx`; spec `feature-plugin-read-write-access-v1` | Confirmado |
| Existem três formatos de concessão por agente e dois vocabulários de impacto convivendo. | `src/types.ts` (`GuildAgent`); `local-gateway.cjs` contra o motor de provedores | Confirmado |
| Em diagramas e na conversa livre com CLI, cada passo recebe todas as capacidades conectadas, e não só as concedidas ao agente. | Specs `feature-cortex-agents-kaivor-plugins-v1` (em andamento), `feature-free-chat-connected-integrations-v1` | Parcial. Enfraquece a promessa de concessão exata por agente. |
| O bot nativo vê todas as conexões de banco; a proteção é a marcação "somente leitura" da conexão. Foi decisão do dono depois que um cliente estranhou respostas de "acesso não concedido". | Spec `feature-bot-full-kaivor-resources-v1` (em andamento) | Confirmado para bancos |

### 4.6 MCP e acesso externo

| Achado | Fonte | Confiança |
| --- | --- | --- |
| Hub de MCP: um servidor MCP é configurado uma vez e reutilizado em conversa, agentes, diagramas e CLIs. A interface avisa quando o runtime escolhido não executa MCP. | Spec `feature-kaivor-mcp-hub-v1`; textos de interface | Confirmado |
| Caminho inverso: o Kaivor se oferece como servidor (HTTP e MCP em loopback) para IAs externas e scripts, com credencial por consumidor, limite de taxa, idempotência e execução assíncrona. | `local-gateway.cjs` | Confirmado |
| Assistente "Usar o Kaivor em outro aplicativo": escolher a IA, definir o acesso, criar a credencial, copiar a configuração por cliente, reiniciar e enviar um pedido de teste. | `src/integration-studio.tsx`; docs "Tela Integrações" | Confirmado |

### 4.7 Jira e GitHub

| Achado | Fonte | Confiança |
| --- | --- | --- |
| Jira Cloud: lê projetos, busca JQL, issue com comentários, transições, quadros e sprints. Escreve criar, editar, comentar e transicionar, sempre com aprovação. Não exclui issue, não trata anexos, não atende Data Center. | Spec `feature-jira-cloud-plugin-v2`; `jira-provider.cjs` | Confirmado no código; validação em site real pendente |
| GitHub e GitLab por REST: uma ferramenta genérica por provedor; conexão de leitura só aceita GET; escrita aceita POST, PUT e PATCH; rotas de alto impacto exigem confirmação; para agentes, DELETE é sempre recusado. | `git-provider-service.cjs` | Confirmado |
| GitHub CLI e GitLab CLI: 15 operações somente leitura cada, sem aprovação. | `src/shared/git-cli-operations.cjs` | Confirmado |
| Dois modelos de segurança de escrita no mesmo produto: operação nomeada e aprovada (Jira) e proxy REST genérico governado por modo da conexão (GitHub). | Comparação dos dois | Inferência |

### 4.8 Falhas

| Achado | Fonte | Confiança |
| --- | --- | --- |
| Erros em três camadas: o que aconteceu, o que fazer, e o texto técnico recolhido com "copiar detalhes". | Spec `feature-friendly-plugin-errors-v1`; `src/features/plugins/error-drawer.tsx` | Confirmado |
| Leitura é refeita até três vezes em erro passageiro; escrita nunca é repetida. | `external-provider-service.cjs` | Confirmado |

## 5. Gerenciamento de agentes

| Achado | Fonte | Confiança |
| --- | --- | --- |
| Catálogo global em cards, cada um com editar, renomear e executar. Dois modos por perfil: com IA e sem IA (determinístico). O card mostra o modo e o runtime. | Spec `feature-agent-studio-v1`; docs "Tela Agentes" | Confirmado |
| Um agente com IA reúne: identidade, runtime (CLI ou API), modelo e nível de raciocínio, instruções, papel de contexto, política de contexto, limite de tokens de entrada, nível de autonomia (seguro, assistido, autônomo, completo), skills, concessões e saídas nomeadas. | Tipo `GuildAgent` em `src/types.ts` | Confirmado |
| Agente determinístico: grafo acíclico de até 30 nós com seis tipos (entrada, capacidade, condição, mapeamento, JavaScript isolado, resposta). O editor guiado tem cinco passos: Origem, Decisão, Transformação, Resposta e Teste. Roda sem provedor de IA. | `electron/main/application/agent/deterministic-agent.cjs`; `src/deterministic-agent-editor.tsx` | Confirmado |
| Dois testes no agente determinístico: com um exemplo fixo, sem chamar a origem, e ao vivo, respeitando concessões e aprovações. | `deterministic-agent-editor.tsx` | Confirmado |
| O agente é uma capacidade reutilizável, não um passo de fluxo: a ordem de execução vive só no diagrama. | Spec `feature-agent-studio-v1` | Confirmado |
| Skill é instrução anexada a agente ou conversa. Não instala, não executa e não concede ferramenta. | Spec `feature-skill-manager-v1` | Confirmado |
| Frases de limite ao lado de cada concessão ("O agente nunca recebe segredos de autenticação", "A autorização vale apenas para as requisições marcadas"). | Textos em `src/main.tsx` | Confirmado |
| Uma visão consolidada "o que este agente pode acessar" numa tela só. | Busca no código | Não identificado. A informação fica espalhada pelas seções do editor. |
| Versionamento de agente e ativar ou desativar agente. | `src/types.ts`; busca | Não identificado |
| Consumo: registro de tokens por quest, etapa, agente e provedor, separando reportado de estimado. Sem valor em dinheiro. | `electron/main.cjs` (`readTokenLedger`); docs "Tela Tokens" | Confirmado |
| Um motor de laço único para todos os provedores, com teto de 30 iterações, correção após três chamadas idênticas e tratamento uniforme de argumento inválido e saída truncada. | `electron/main/application/agent/agent-loop.cjs` | Confirmado |
| Avaliação do laço sem rede: modelo roteirizado e transporte falso. Mede a conformidade do laço, não a qualidade da resposta. | `scripts/evaluate-agent-loop.cjs` | Confirmado. Validação com provedor real pendente. |
| O editor de agente era técnico demais e escondia permissões obrigatórias numa seção "opcional". | `docs/ux-audit-first-value-2026-09.md` (UX-05) | Confirmado como problema admitido; correção registrada como feita |

Diferenças conceituais que o produto mantém: **agente** (perfil reutilizável), **ferramenta** (operação que o runtime
chama), **capacidade** (unidade com identificador estável e impacto), **plugin** (conector que publica capacidades),
**skill** (instrução), **rotina** (sequência salva com gatilho), **diagrama** (ordem dos agentes), **consumidor**
(identidade externa).

## 6. Automações e orquestração

O achado mais importante desta seção é estrutural: o Kaivor não tem um motor de fluxo, tem quatro, cada um com modelo
de dados, aprovação e histórico próprios.

| Motor | Onde aparece | O que tem |
| --- | --- | --- |
| Automações | Menu Rotinas | Etapas por capacidade, aprovação antes de começar, gatilho manual ou webhook |
| Quests | Cortex, Kanban | Pipeline de agentes com ramificação por saída nomeada |
| Tarefas do bot | Menu Tarefas | Plano de até 20 ações, checkpoints, estado "incerto" |
| Agente determinístico | Agentes, etapa de diagrama | Condição, mapeamento e JavaScript isolado |

Fonte: `workflow-automation.cjs`, `cortex-routing.cjs`, `autonomy-task.cjs`, `deterministic-agent.cjs`. Confirmado.

| Achado | Fonte | Confiança |
| --- | --- | --- |
| Criação a partir de uma conexão: guia "Da conexão ao resultado" em três passos; gera um rascunho revisável a partir de um recurso exato; nunca salva nem executa sozinho. | `src/features/automations/first-automation-guide.tsx`; spec `feature-first-automation-delivery-v1` | Confirmado |
| Cada etapa: capacidade, recurso salvo, campos amigáveis e um bloco recolhido "Dados avançados (JSON)". | `src/integration-studio.tsx`; `src/automation-step-inputs.tsx` | Confirmado |
| Passagem de resultado entre etapas por um resolvedor de modelos restrito (sem código). Na interface, a pessoa digita a referência no JSON avançado; não há seletor visual da saída da etapa anterior. | `workflow-automation.cjs`; `integration-studio.tsx` | Confirmado |
| O motor de automações tem nó de condição, rota de falha, repetição por nó e transformação por modelo. A interface de Rotinas não expõe nada disso: salva sempre uma cadeia linear. | `workflow-automation.cjs` contra `src/` | Confirmado |
| Repetição: de uma a três tentativas por nó, com espera crescente. Nunca repete ferramenta MCP, etapa que lançou exceção nem bloqueio de aprovação. A regra é que efeito colateral incerto não se repete. | `workflow-automation.cjs` | Confirmado |
| Toda etapa precisa devolver sucesso explícito; sem isso, vira falha. | `workflow-automation.cjs` | Confirmado |
| Aprovação: ao salvar, se alguma etapa altera dados ou tem impacto desconhecido, a interface insere uma aprovação antes das ações. Ao aprovar, as permissões são conferidas de novo. | `local-gateway.cjs`; `docs/first-automation-validation-2026-09-15.md` | Confirmado |
| A aprovação vale para a execução inteira, antes de começar. Pausa durável no meio de uma automação não está demonstrada. | `docs/reviews/automation-flow-audit-2026-09-09.md` | Confirmado como limite admitido |
| Agendamento nas automações é só um campo: não há agendador que o consuma. O agendamento que funciona está em dois outros lugares (rotinas do bot e "Loop" do diagrama), e todos exigem o aplicativo aberto. | `workflow-automation.cjs`; `autonomy-routines.cjs`; `src/main.tsx` | Confirmado |
| Sem paralelismo, junção, espera nem laço livre em nenhum motor. | Código e auditoria de fluxo | Confirmado |
| Gatilhos por arquivo, Git ou e-mail. | Busca | Não identificado |
| O bot não cria automações da tela Rotinas. Ele propõe rotinas agendadas, salvas inativas, que aparecem dentro de Tarefas. O termo "Rotinas" tem dois sentidos no produto. | `chat/autonomy-tools.cjs`; `src/features/autonomy/routines-section.tsx` | Confirmado |
| O próprio projeto reconhece que a diferença entre os construtores não é evidente para quem chega. | `docs/ux-audit-first-value-2026-09.md` (UX-06) | Confirmado |

## 7. Cortex: diagramas de agentes

| Achado | Fonte | Confiança |
| --- | --- | --- |
| Existe um único tipo de nó: etapa de agente (com ou sem IA). Não há nós de gatilho, condição ou aprovação. | `src/types.ts`; `src/main.tsx` (`ProjectWorkflowCanvas`) | Confirmado |
| Ramificação por saídas nomeadas do agente (até oito), desenhadas como portas no card. O roteador escolhe a aresta pela saída nomeada, depois pelo estado do portão, depois pela aresta padrão. | `services/kaivor-platform/src/shared/cortex-routing.cjs` | Confirmado |
| Laços (por exemplo, QA de volta ao Builder) limitados a dois retornos por aresta; o contador sobrevive a pausa e reinício; cada retorno mostra origem, destino, motivo e "tentativa N de M". | `electron/main/application/quest/revisit-counts.cjs` | Confirmado |
| Aprovação humana marcada por etapa ("pedir aprovação ao concluir"), válida a cada passagem. | `quest/approval-gate.cjs` | Confirmado |
| Rascunho de diagrama por IA: a pessoa descreve o resultado, vê a prévia e escolhe aplicar. O rascunho só usa agentes já cadastrados, não cria ramificações e não salva nem executa. | `electron/main/application/cortex/ai-diagram-draft.cjs`; `docs/cortex-ai-diagram-drafts.md` | Confirmado |
| Não há validação formal do diagrama: ciclos e nós soltos são aceitos. | Spec `feature-project-workflow-canvas-v1` | Confirmado na spec; ausência de validador é Inferência |
| Durante a execução, o nó ativo é destacado e a aresta é animada. O estado por nó é derivado do índice corrente, não de um estado gravado por nó. | `src/main.tsx` | Confirmado o destaque; a aproximação em laços é Inferência |
| "Ajustar direção": nota humana com imagens e escolha de retomar da etapa atual ou da anterior, sem burlar aprovação. | `interveneQuestRun` em `electron/main.cjs` | Confirmado |
| O canvas começou com uma biblioteca de diagramas e foi reescrito em SVG próprio. | `package.json`; histórico citado na spec | Confirmado |
| Custo: cerca de 2.100 linhas dentro de `src/main.tsx`, 115 KB de estilos, uma spec de 44 KB com mais de 45 requisitos (a maioria sobre alças, âncoras e posições) e 61 specs que mencionam o Cortex. | Contagem no repositório | Confirmado |
| O mapa de lacunas do próprio projeto lista como prioridade máxima a normalização duplicada, a falta de testes de gesto no canvas e um teste do ciclo pausar, retomar e parar falhando. | `docs/kaivor-gap-map-2026-06-04.md` | Confirmado na data do documento |

## 8. Quests, Tarefas e Kanban

| Achado | Fonte | Confiança |
| --- | --- | --- |
| Pipeline de desenvolvimento: Discovery, Spec Architect, E2E Captain, aprovação do plano, Builder, QA, Reviewer, evidências. Discovery separa fatos, suposições e perguntas; QA e Reviewer podem bloquear e devolver. | Docs "Fluxo de uma quest"; `buildPrompt` em `electron/main.cjs` | Confirmado |
| O objetivo é texto livre com anexos, pasta e nível de autonomia. Não há objeto estruturado de escopo ou critério; isso nasce dos agentes. | `startQuestRun` | Confirmado |
| Cada etapa termina com um manifesto estruturado obrigatório (estado do portão, resumo, arquivos alterados, testes com resultado, artefatos, riscos, próximas ações, saída escolhida). Ele alimenta o roteamento, o repasse ao próximo agente e o bloco de entrega. | `src/types.ts`; `docs/structured-handoff-todo.md` | Confirmado |
| Etapa sem manifesto não bloqueia: passa com ressalva explícita. | Mesmo documento | Confirmado |
| Cada agente recebe um envelope de contexto por papel, com orçamento de tokens. O detalhe mostra seções, cortes e uso do orçamento. | `src/shared/context-router.cjs` | Confirmado |
| Bloco de entrega "Spec, testes, evidências": conta critérios, validações, riscos e portões bloqueantes, com estado Validado, Com ressalvas, Bloqueado ou Aguardando evidências. | `QuestRunModal` em `src/main.tsx` | Confirmado |
| A classificação de evidências é heurística por nome e caminho de arquivo. | `quest/artifact-classifier.cjs` | Confirmado |
| Kanban: cinco colunas derivadas do estado (Em execução, Aguardando aprovação, Pausadas, Concluídas, Atenção). Sem arrastar, sem colunas configuráveis, sem responsável, prazo ou prioridade. | `KanbanBoard` em `src/main.tsx`; docs "Tela Kanban" | Confirmado. É um painel de estado de execuções, não um quadro de trabalho. |
| Tarefas do bot: o bot propõe um plano de até 20 ações; o aplicativo valida permissão e disponibilidade de cada uma; a aprovação fica presa à revisão exata do plano; plano alterado invalida a aprovação. | `services/kaivor-platform/src/shared/autonomy-task.cjs`; `src/features/autonomy/tasks-panel.tsx` | Confirmado no código; o mapa marca como parcial |
| Consentimento permanente só para planos de leitura; planos que alteram dados sempre esperam revisão. | `tasks-panel.tsx` | Confirmado |
| Estado "resultado incerto": escrita sem confirmação, tempo esgotado ou falha depois do envio nunca viram sucesso nem são repetidas. A tela manda conferir o recurso afetado. | `autonomy-service.cjs` | Confirmado |
| O plano de uma tarefa não é editável pela interface: só aprovar ou parar. | `tasks-panel.tsx` | Confirmado |

## 9. Gestão de projetos e gestores

Este eixo foi procurado de propósito, porque é central para o Spock.

| Recurso procurado | Resultado | Confiança |
| --- | --- | --- |
| Cadastro de projetos | "Projeto" significa pasta de trabalho ou diagrama de agentes. Não há projeto gerenciado com metadados. | Confirmado |
| Visão consolidada de vários projetos (portfólio) | Não identificado | Não identificado |
| Responsáveis, distribuição de atividades | Não identificado. Não há pessoa atribuída a nada. | Não identificado |
| Planejamento, priorização, marcos, prazos | Não identificado. O único "prazo" no código é o do trial. | Não identificado |
| Dependências | Só o "começar depois de" da equipe de agentes na conversa, que é efêmero. | Limitado |
| Bloqueios | Só o estado "bloqueio" de portão de agente. | Limitado |
| Riscos | Lista de texto extraída dos manifestos, contada no bloco de entrega. Sem registro, dono, severidade ou histórico. | Limitado |
| Indicadores, previsão, relatórios gerenciais | Não identificado | Não identificado |
| Ferramentas de gestão como fonte | Jira, Azure Boards, ClickUp, Kanbanize, Taiga e Linear existem como conectores para o bot consultar, não como base de uma visão gerencial. | Confirmado |

Conclusão objetiva: **o Kaivor não é uma plataforma de gestão de projetos e não oferece experiência de gestor.** Ele
acompanha execuções de agentes. Para o Management Workspace do Spock, o Kaivor serve como referência de padrões
(evidência, aprovação, honestidade de estado), não de funcionalidades.

## 10. Suporte da IA ao trabalho

| Achado | Fonte | Confiança |
| --- | --- | --- |
| Conversa com projeto, branch, modelo por conversa, nível de acesso, skills e agentes; fila de mensagens durante a execução; rascunhos preservados. | `docs/reviews/chat-usability-performance-2026-09-30.md` | Confirmado |
| Contexto seletivo: limites de mensagens e caracteres, prioridade para o último pedido e o pedido inicial. Painel "Contexto da próxima resposta" mostra o que foi selecionado, omitido e abreviado, com "excluir do contexto" por mensagem. | Spec `feature-chat-selective-context-v1`; `src/features/free-chat/chat-context-panel.tsx` | Confirmado. A spec assume que é heurística. |
| Por resposta: tokens (marcados quando estimados), tempo, primeira saída e contexto enviado. | `src/features/free-chat/run-status.tsx` | Confirmado |
| Memória do cliente: preferências e fatos, com liga e desliga, salvar explícito e apagar. Rejeita credenciais. | Spec `feature-bot-persistent-memory-v1` | Confirmado |
| Memória aprendida em três tipos: fatos do ambiente com evidência, registro de cada execução escrito pelo aplicativo (nunca pelo modelo) e procedimentos com contagem de sucesso e falha. A memória nunca concede permissão. | Spec `feature-bot-cognitive-architecture-v1` | Parcial (primeiro incremento) |
| Memória de projeto num arquivo dentro do projeto (arquivos, testes, riscos, decisões), incluída no contexto com orçamento. | Spec `feature-context-observability-v1` | Confirmado |
| Atividade da IA como um único elemento recolhível por resposta: ao vivo mostra ação atual, número de ações e tempo; depois, "N ações executadas" e a linha do tempo sob demanda. | `src/features/free-chat/chat-activity-stream.tsx` | Confirmado no código |
| Cards de execução gerados só a partir do rastro estruturado: o texto do modelo não consegue fabricar um card. | Spec `feature-free-chat-execution-cards-v1` | Parcial |
| Aprovações em painel lateral com solicitante, recurso e horário; botões desabilitados se o estado ficou desatualizado. O modelo não pode usar um cartão de pergunta para declarar permissão concedida. | `src/features/free-chat/chat-approvals.tsx` | Confirmado |
| Comandos de barra inserem um texto revisável; não executam nada. | `slash-command-menu.tsx` | Confirmado |
| Gráficos e tabelas em respostas: o modelo envia dados e intenção; cores e estilo são do renderizador; documento inválido mostra explicação, nunca um gráfico parcial; o modelo é instruído a citar fonte, período e filtros. | Spec `feature-chat-data-charts-v1` | Confirmado como implementado localmente |
| Modo automático: trabalha do início ao fim sem perguntar o próximo passo; nunca enfraquece permissões. Foi criado porque o bot "perguntava demais". | Spec `feature-bot-auto-mode-autonomy-v1` | Confirmado no desktop; verificação pendente |
| Separação entre fatos, cálculos, inferências e recomendações na resposta da IA. | Busca | Não identificado como estrutura de resposta. Existe só no agente Discovery (fatos, suposições, perguntas). |

## 11. Execuções e observabilidade

| Achado | Fonte | Confiança |
| --- | --- | --- |
| Central de Execuções é uma projeção somente leitura que junta execuções de diagrama, conversas e automações. Não cria armazenamento novo. | `src/shared/execution-center.mjs`; spec `feature-execution-center-v1` | Confirmado |
| As tarefas do bot não entram na central; ficam só em Tarefas. | Mesmo arquivo | Confirmado |
| Busca sem acento sobre nome, origem, entrada e retorno; filtros por tipo, estado agrupado e origem; quatro contadores; dez itens por página. A spec exige distinguir "não existe nada" de "os filtros não retornaram nada". | `src/features/executions/execution-center.tsx`; docs "Tela Execuções" | Confirmado |
| Detalhe em painel lateral: estado, início, atualização, etapa com falha, bloco "Próximo passo" por código de erro com link para a tela de correção, entrada e resultado com aviso de truncamento. | `execution-detail-drawer.tsx` | Confirmado |
| Para automações, o painel mostra só o nó que falhou, não a lista de etapas com entradas e saídas (os dados existem). | Mesmo arquivo | Confirmado |
| Não há reexecução na central, por decisão: inspecionar nunca reenvia uma operação externa. | Spec `feature-execution-center-v1` | Confirmado |
| Duração ativa descontando pausas só nas quests; custo só em tokens e só nas quests. | `src/shared/running-runs.mjs` | Confirmado |
| Checkpoints e identificadores estáveis de ação existem de forma madura só no motor de Tarefas: checkpoint gravado antes do envio, pedido de admissão idempotente, recibo verificado ao fim. | `autonomy-task.cjs`, `autonomy-service.cjs` | Confirmado |
| Modo de diagnóstico: botão no topo, desligado a cada início; captura console e chamadas internas (só canal, duração e erro), com redação de segredos; painel com filtros, copiar e salvar. | `electron/main/application/debug/debug-log-service.cjs` | Confirmado no serviço |
| Segredos e cabeçalhos de autenticação não devem aparecer em entradas, saídas ou evidências. | Docs "Tela Execuções" | Confirmado como regra declarada |

## 12. Ferramentas de desenvolvimento e dados

| Achado | Fonte | Confiança |
| --- | --- | --- |
| Database Studio: seis motores (PostgreSQL, MySQL, SQL Server, Oracle, SQLite, MongoDB); conexões com TLS e túnel SSH; editor com completar, parâmetros e histórico; grade com edição; transações; planos de execução; diagrama de relacionamentos; comparação de schema; geração de SQL por IA com índice do schema. | Mapa seção 4.5; docs "Database Studio" | Confirmado |
| Seguro por padrão: conexão nova nasce somente leitura; o SQL é classificado no processo principal; alteração exige confirmação em painel lateral com conexão, servidor e prévia. | Spec `feature-database-query-safety-v1` | Confirmado |
| HTTP Client: árvore de pastas, oito tipos de autenticação com herança, ambientes, variáveis, encadeamento resposta para variável, histórico, importação de cURL e Postman (scripts da coleção nunca são executados). | Spec `feature-http-client-v1`; docs "Tela HTTP Client" | Confirmado |
| Reuso por agentes: uma requisição salva ou uma conexão de banco vira ferramenta do agente, com limites. O modelo nunca fornece URL, método, cabeçalho ou segredo. | Specs `feature-agent-database-access-v1`, `feature-http-client-v1` | Confirmado |
| Espaço de trabalho: explorador de arquivos, abertura rápida, Git (sem push), terminais reais, editores externos. | Spec `feature-desktop-workspace-v1` | Confirmado |
| Tamanho: o Database Studio responde por 114 specs, 74 arquivos no processo principal e 24 folhas de estilo. É, na prática, um produto dentro do produto. | Contagem no repositório | Confirmado |

## 13. UX/UI

### 13.1 O que foi observado

| Tema | Observação | Fonte | Confiança |
| --- | --- | --- | --- |
| Layout | Composição de painéis de borda a borda, não pilha de cards flutuantes. Separadores de um pixel. A janela nunca rola; rola a menor região (lista, editor, tabela, painel). | `DESIGN.md` | Confirmado |
| Cor | Neutros de carvão e papel, sem acento cromático no shell; cor de estado restrita ao marcador pequeno. Temas claro e escuro com paridade obrigatória. | `DESIGN.md` | Confirmado |
| Tipografia | Fonte de sistema no shell; mono para código; algarismos tabulares. Escala 10 a 24 px; corpo de 13 a 14 px. | `DESIGN.md`; `docs/design-system.md` | Confirmado |
| Densidade | Controles de 30, 34 e 40 px. Regra escrita: prioridade vem de posição, rótulo, contraste e hierarquia, não de tamanho. | `docs/design-system.md` | Confirmado |
| Elevação | Painéis planos; sombra só em menus sobrepostos. | `DESIGN.md` | Confirmado |
| Movimento | 140, 220 e 320 ms; respeita preferência de movimento reduzido. | `DESIGN.md` | Confirmado |
| Painel lateral em vez de modal | Regra repetida no guia e nas specs: interface operacional não usa modal central. Edição, configuração, permissões e confirmações vão para painel lateral empilhável; Esc fecha o do topo. | `AGENTS.md`; `src/design-system.tsx` | Confirmado |
| Confirmação destrutiva | Painel com o nome do alvo, "não pode ser desfeita" e o que não será afetado. | `src/database-delete-confirmation-drawer.tsx` | Confirmado |
| Feedback | Resposta ao lado da ação, não em aviso flutuante: cerca de 130 regiões de estado e 100 de alerta contra 2 avisos flutuantes. | Contagem no código | Confirmado |
| Carregamento | Um único componente com rótulo obrigatório; quase não há esqueleto de carregamento. | `src/shared/components/kaivor-loading.tsx` | Confirmado |
| Estado vazio | Título, texto e ação. | `tasks-panel.tsx` | Confirmado |
| Erro | A ação oferecida muda conforme o tipo de falha (reconectar, aguardar e tentar). | `src/features/free-chat/chat-failure-actions.tsx` | Confirmado |
| Botão desabilitado | Regra escrita: todo botão primário desabilitado explica o pré-requisito e oferece o próximo passo. | Spec `feature-guided-hub-experience-v1` | Confirmado como regra |
| Recursos avançados | Atrás de "Detalhes técnicos": JSON, identificadores e diagnósticos ficam recolhidos. | Várias telas | Confirmado |
| Primeiro uso | Três caminhos concretos na conversa vazia e um exemplo que roda sem conta, conexão ou IA. | `src/hub-onboarding.tsx` | Confirmado |
| Escrita | Verbo de ação e consequência; honestidade explícita ("Estimativa; não representa cobrança", "Esta ação não será repetida automaticamente"). | Textos de interface | Confirmado |
| Acessibilidade | Atributos de estado sistemáticos, foco devolvido ao gatilho, contorno de foco de 2 px, redimensionamento por teclado. | Código | Confirmado. O próprio projeto diz que não equivale a uma auditoria completa. |

Capturas de referência (não copiadas): no repositório do Kaivor, `.impeccable/review/chat-1440-dark.png` (shell e
conversa), `projects-dark.png` (rail expandido e painel de trabalho), `file-split-dark.png` (divisão conversa e
arquivo), `explicit-model-picker.png` (seletor pesquisável) e a pasta `settings-completion/` (configurações). No site,
a seção de prévia de produto com as abas Bot, Tarefas, Rotinas, Conexões e Notas, toda marcada como "Demonstração
ilustrativa, dados fictícios".

### 13.2 O que o próprio Kaivor aponta como problema

Fonte: `docs/ux-audit-first-value-2026-09.md`. A tese do documento é que o produto expunha o modelo interno de execução
antes de a pessoa concluir uma tarefa útil.

- O primeiro uso começava pela arquitetura (modelos, agentes, diagramas), não pela intenção.
- Conexões tinham vários nomes e lugares concorrentes.
- O acesso externo vinha com 181 capacidades pré-selecionadas em 183 caixas, com descrições técnicas em inglês.
- Automações ficavam escondidas em Configurações, com campos como "ID do recurso salvo" e "Inputs JSON".
- O modo sem IA exigia conhecimento de programação.
- "Descartar alterações" não descartava.
- Botão de conectar desabilitado sem explicação.

A validação posterior registra todos como tratados, com a ressalva de que foi uma revisão de especialista, sem estudo
com usuários.

## 14. Limitações e pontos fracos do Kaivor

1. **Validação real pendente em quase tudo:** Jira, OneDrive, Google Cloud, GitHub CLI, login Microsoft, laço de agente
   com provedor real, sessões interativas, Windows e Linux.
2. **Quatro motores de execução sem modelo comum**, com capacidades que existem num e faltam no outro, e nomes que se
   confundem (Rotinas, Tarefas, Cortex, Agentes, Execuções).
3. **O editor de automações é linear e apoiado em JSON**, embora o motor saiba ramificar e repetir.
4. **Agendamento fragmentado** e dependente do aplicativo aberto.
5. **Permissão exata por agente convive com acesso amplo** do bot nativo e dos passos de diagrama.
6. **Sem estado de saúde de conexão**, sem versionamento de agente, sem custo em dinheiro, sem reexecução.
7. **Três gerações de identidade visual convivendo**, cinco famílias de ícones, 62 folhas de estilo com 752
   ocorrências de `!important`, e arquivos de dez a dezoito mil linhas.
8. **Documentação pública defasada** em relação ao aplicativo, e status de spec que não batem com o código.
9. **Sem gestão de projetos**: nada de portfólio, responsáveis, prazos, indicadores ou previsão.
10. **Amplitude muito grande** para a maturidade de cada parte: 48 conectores, seis motores de banco, canvas próprio,
    voz, documentos, web. O custo aparece nas listas de pendências do próprio projeto.

## 15. O que não foi possível verificar

- Qualquer comportamento em execução, inclusive tempos, animações e a fidelidade do destaque de nós em laços.
- Onde exatamente o aplicativo marca uma operação como aprovada, e se há caminhos que contornam isso.
- O aplicativo web e o serviço de plataforma, além das partes compartilhadas.
- A spec do bot autônomo (105 KB) foi lida por títulos e trechos; a maior parte das 395 specs não foi lida.
- Contraste real, leitor de tela e comportamento em Windows e Linux.
- Se existe um indicador permanente de "onde roda" no campo de mensagem.
