# Evolução para plataforma de Engenharia e Gestão

**Data:** 2026-11-10, atualizado em 2026-11-11. **Estado:** Fases 1 e 2 mergeadas (PR #40); Fase 3 e marca mergeadas (PR #41); Fase 4 mergeada (PR #42); Fase 5 entregue; Fases 6 e 7 planejadas.

> O produto passou a se chamar **Spock Studio** (System of Programming, Organization, Code and Knowledge). Ver seção 12.

O Mariliz passa a ter dois ambientes sobre o mesmo projeto (Engenharia e Gestão) e uma central de integrações que
alimenta os dois. Este documento registra o que a auditoria encontrou, o que foi construído, os contratos, as decisões e
o que falta. A arquitetura não mudou: Tauri hospeda, React apresenta, o motor .NET decide e persiste, o Python orquestra
agentes.

## 1. Diagnóstico do produto antes deste ciclo

| # | Tema | O que existe |
| --- | --- | --- |
| 1 | Navegação | Menu lateral único: Início, Projeto, Configurar, Catálogo (indisponível), Configurações. Rotas por hash em `shared/routing/paths.ts`. |
| 2 | Projetos | Um projeto é uma pasta autorizada pelo diálogo nativo. O motor gera o `projectId` a partir do caminho (`WorkspaceAuthorizer`) e guarda a autorização em `workspaces.json`. Os recentes ficam no host Rust. Não há cadastro, nome editável nem metadados próprios. |
| 3 | Domínio | `Mariliz.Core` tem descoberta, plano e evals. Não havia organização, equipe, usuário, papel, marco, risco, métrica nem conexão externa. |
| 4 | Tecnologias | Detectores por stack (`DotnetDetector`, `NodeDetector`, `FlutterDetector`, `CiDetector`) produzem `StackFinding` com versão, confiança e evidência a cada `project.discover`. O resultado não é persistido como perfil. |
| 5 | Harness | Jornada "Configurar projeto": descobrir, planejar, revisar, aplicar, validar, reverter. Um modelo de implementação (Mariliz). |
| 6 | Agentes e Skills | Ativos embutidos (`Assets/agents`, `skills`, `rules`, `hooks`) aplicados pelo plano. Não há tela de gerenciamento individual. |
| 7 | MCP | Só inventário: `HarnessInventory.mcpServers` lista o que o projeto já declara. Sem gerenciamento. |
| 8 | RAG e memória | `intelligence/knowledge` (índice e recuperação) e `intelligence/memory`, alimentados por `KnowledgeSources` com arquivos do projeto, por projeto. |
| 9 | Execuções | `RunTracker`, diário em `FileRunStore`, eventos por `runId`, tela de execução com etapas e evidências. |
| 10 | Autenticação e autorização | Não há usuário nem papel. O limite de acesso é a pasta autorizada e a lista de métodos que o host deixa a interface chamar. |
| 11 | Interface e motor | NDJSON v1 pelo host Tauri (`engine_request`). A lista de métodos é conferida em três lugares: interface, host e motor. |
| 12 | Reutilizável | Protocolo e `EngineSession`, `WorkspaceAuthorizer`, portas (`IClock`), `EngineException`, padrão de arquivo atômico, fixtures de contrato conferidas nos dois lados, `useAsyncResource`, átomos de UI. |
| 13 | Com interface | Início, Projeto, Configurar, Pedido de engenharia, Execução, Configurações (chave de modelo). |
| 14 | Protótipo | Nada: o protótipo visual foi removido no PR #39. "Catálogo" e três atividades da tela inicial são itens declarados como indisponíveis. |

Dois achados que afetam o plano:

- A chave de modelo é gravada em texto puro em `{dataRoot}/model.json`. A central de integrações não repete isso
  (seção 5); migrar a chave de modelo para o cofre é uma pendência (seção 8).
- Não existe identidade de usuário. Sem ela, perfis como Gestor ou Desenvolvedor seriam só rótulos (seção 6).

## 2. Arquitetura funcional

```
Engenharia (Projeto, Configurar, Pedido, Execução)      Gestão (Acompanhamento)
                 \                                         /
                  projeto autorizado (um só, o mesmo id)
                 /                                         \
Inteligência (.NET decide, Python orquestra)        Integrações (conexões, recortes, sincronização)
```

- **Projeto** continua sendo a pasta autorizada. Não foi criado um segundo cadastro: os dois ambientes leem o mesmo
  `projectId`.
- **Integrações** pertence ao motor .NET. O Python não fala com provedores nem guarda credenciais.
- **Gestão** lê só o que foi sincronizado. Não cria tarefas: Jira e GitHub seguem como fonte oficial.

## 3. O que foi entregue

### Fase 1: fundação dos ambientes

- Menu lateral em grupos: **Engenharia** (Projeto, Configurar, Catálogo), **Gestão** (Acompanhamento) e **Plataforma**
  (Integrações, Configurações). Com o menu recolhido, um fio separa os grupos.
- Rotas novas: `/gestao` e `/integracoes`. As rotas existentes não mudaram.
- A atividade "Conectar ferramentas" da tela inicial deixou de ser "Em breve" e abre a central.
- Tela **Acompanhamento**: para o projeto aberto, mostra a contagem de atividades por situação e as fontes de onde veio,
  com a data da leitura. Sem fonte ou sem sincronização, diz que não há dados (não mostra zero). Indicadores, previsão e
  riscos aparecem como "ainda não calculado".

### Fase 2: central de integrações

- **Conexões**: conectar, testar, trocar a credencial, desconectar e remover. A conexão só é gravada depois que o
  provedor aceita a credencial.
- **Vínculo com o projeto**: uma conexão é vinculada a um projeto com um recurso (repositório ou projeto do Jira) e os
  tipos de dado habilitados.
- **Conectores**: GitHub (issues e pull requests) e Jira Cloud (issues com histórico de status).
- **Sincronização**: sob demanda, em segundo plano, incremental, com histórico.
- **Auditoria**: conexões, desconexões, mudanças de recorte e sincronizações em `{dataRoot}/audit.ndjson`.

## 4. Modelo de domínio (`Mariliz.Application/Integrations`)

| Conceito | Tipo | Responsabilidade | Invariantes |
| --- | --- | --- | --- |
| Connection | `IntegrationConnection` | Autenticação e capacidade técnica de acessar um sistema. | Não contém credencial. Só existe se o provedor aceitou a credencial. Não é removida enquanto houver vínculo. |
| Project Integration | `ProjectIntegration` | Associação de uma conexão a um projeto. | Só é encontrada pelo projeto dono. Um recurso é vinculado uma vez por conexão e projeto. |
| Data Scope | `DataScope` | Recurso e tipos de dado que o projeto pode ler. | Nunca vazio. Só tipos que o conector oferece. Reduzir o recorte apaga o que ficou fora. |
| Contextualization Policy | `ContextualizationPolicy` | Se o dado sincronizado pode servir de contexto a agentes. | Nasce fechada (`feedsKnowledge = false`). |
| Sincronização | `SyncExecution`, `SyncedData` | Uma execução e o que ela trouxe: itens, ponto de retomada, histórico. | Uma por integração de cada vez. `created + updated + unchanged = fetched`. |
| Atividade | `WorkItem` | Issue ou pull request no formato comum. | Só fatos do provedor; nada calculado. |

Os quatro primeiros são a separação pedida entre conexão e contextualização. A política existe e é gravada, mas ainda
não tem consumidor: nenhum agente lê dado sincronizado nesta versão. Quem for ler (Fases 5 e 7) precisa conferi-la.

Persistência, tudo no diretório de dados do motor e nunca dentro de um projeto:

| Arquivo | Conteúdo |
| --- | --- |
| `integrations/connections.json` | Conexões e vínculos, sem credencial. |
| `integrations/secrets/{connectionId}.bin` | Credencial cifrada pela DPAPI do Windows para o usuário atual. |
| `integrations/data/{projectId}/{integrationId}.json` | Itens, ponto de retomada e histórico de um vínculo. |
| `audit.ndjson` | Trilha de operações, uma linha por operação. |

## 5. Decisões

1. **Credenciais só em cofre do sistema.** No Windows, DPAPI (`DpapiSecretVault`). Em Linux e macOS ainda não há
   cofre implementado: a central informa isso e recusa criar conexões, em vez de gravar em texto puro.
2. **Isolamento por projeto em três camadas.** O serviço só encontra um vínculo pelo projeto dono; cada vínculo tem um
   arquivo dentro da pasta do seu projeto; a interface remonta a lista ao trocar de projeto.
3. **O recorte monta a chamada.** O conector constrói o endereço a partir do recurso do recorte. No GitHub, o cabeçalho
   `Link` só informa se há próxima página; o endereço que ele traz não é seguido. O serviço de sincronização descarta de
   novo qualquer item de tipo fora do recorte.
4. **Só leitura e só HTTPS.** O transporte faz apenas GET, não segue redirecionamento, e o Jira só aceita site
   `https://<nome>.atlassian.net`.
5. **Sincronização sob demanda.** Não há sondagem automática nem webhooks (o aplicativo é local e não recebe chamadas).
   Cada execução tem teto de 50 páginas; passando disso termina como parcial e a próxima continua.
6. **Ponto de retomada por página.** Os provedores devolvem do mais antigo para o mais recente; cada página gravada
   avança o ponto. Uma falha no meio não perde o que chegou.
7. **Idempotência por identidade no provedor.** O item é gravado por tipo e identificador; reler não duplica, e só conta
   como atualizado quando o conteúdo mudou.
8. **Falha passageira e limite de chamadas.** Até 4 tentativas com espera de 1 s, 2 s e 4 s. Limite de chamadas com
   espera de até 30 s é aguardado; acima disso a sincronização falha dizendo a partir de que horas tentar.
9. **JQL com janela relativa.** O Jira interpreta data absoluta no fuso da conta. A sincronização incremental usa
   `updated >= -Nm`, que não depende de fuso.
10. **Categoria de situação sem inferência.** Jira: a categoria do status. GitHub: issue aberta é "a fazer", pull request
    aberto é "em andamento", fechado é "concluído". O status original fica guardado ao lado.
11. **Catálogo de conectores vem do motor.** A interface desenha campos e rótulos a partir de `integration.catalog`; um
    conector novo não exige mudar telas.

## 6. O que não foi feito, e por quê

- **Papéis (Administrador, Gestor, Líder técnico, Desenvolvedor, Leitor).** O produto não tem identidade de usuário:
  é um aplicativo local de uma pessoa. Papéis sem login seriam uma escolha na tela que qualquer um troca. Precisa de
  decisão de produto sobre organização e autenticação antes (seção 8). O que já vale: a tela de Gestão não chama
  nenhum método de engenharia, e nenhuma resposta do motor carrega credencial.
- **Portfólio de vários projetos.** O motor conhece os projetos autorizados, mas nome e caminho exibidos ficam no host
  (recentes). A visão consolidada entra na Fase 3.
- **Azure DevOps, GitLab, MCP como conexão.** Fora do primeiro ciclo, como pedido. O contrato `IIntegrationConnector`
  é o ponto de extensão.

## 7. Contrato do protocolo

Métodos novos, liberados no host (`FRONTEND_METHODS`), na interface (`ENGINE_METHODS`) e no motor
(`ProtocolMethods.Integrations`). As respostas têm fixtures em `contracts/fixtures/integrations`, geradas pelo motor e
conferidas pelo Zod da interface.

| Método | Parâmetros | Resposta |
| --- | --- | --- |
| `integration.catalog` | | `vaultAvailable`, `connectors[]` |
| `connection.list` | | `connections[]` |
| `connection.create` | `provider`, `name`, `settings`, `secret` | `connection` |
| `connection.test` | `connectionId` | `connection` (a recusa fica em `status` e `lastError`) |
| `connection.rotate` | `connectionId`, `secret` | `connection` |
| `connection.disconnect` | `connectionId` | `connection` |
| `connection.remove` | `connectionId` | `removed` |
| `integration.list` | `projectId` | `integrations[]` com resumo do sincronizado |
| `integration.link` | `projectId`, `connectionId`, `resource`, `dataKinds` | `integration` |
| `integration.configure` | `projectId`, `integrationId`, `dataKinds?`, `feedsKnowledge?` | `integration` |
| `integration.unlink` | `projectId`, `integrationId` | `removed` |
| `integration.sync` | `projectId`, `integrationId` | `sync` (responde na hora, com `running`) |
| `integration.history` | `projectId`, `integrationId` | `syncs[]` |

`secret` só existe nos parâmetros de `connection.create` e `connection.rotate`. Nenhuma resposta o contém.

## 8. Validação

| Suíte | Resultado |
| --- | --- |
| `dotnet test runtime/Mariliz.sln` | 490 aprovados, 3 ignorados (os de pacote instalado). 21 novos em `IntegrationHubTests`. |
| `pnpm --dir desktop/ui check` e `pnpm ui:build` | 233 aprovados; tipos, lint, formatação e build sem erro. |
| `cargo test` e `cargo fmt --check` no host | 23 aprovados. |

O que os testes novos provam, na ordem da lista de validação do pedido:

1. Um projeto não lê, altera, sincroniza nem lista o vínculo de outro (motor e interface).
2. A sincronização só chama o recurso do recorte e só guarda os tipos habilitados.
3. Sincronizar de novo não duplica; a segunda execução só pede o que mudou.
4. Falha no meio mantém o que chegou; a próxima retoma do ponto gravado; execução cortada por queda vira falha.
5. A credencial não aparece em resposta, arquivo de dados, auditoria nem na tela.
6. Sem dados, a tela de Gestão diz que não há dados.
7. As funcionalidades anteriores continuam passando nas suítes existentes.

**Com provedor real:** o motor compilado foi executado contra a API real do GitHub com um token inválido de propósito e
devolveu `INTEGRATION_AUTH_FAILED`, sem gravar nada. Isso valida transporte, cabeçalhos (versão `2026-03-10` da API) e
o tratamento da recusa.

**Não validado com provedor real:** sincronização de um repositório real e todo o conector do Jira. Faltou uma
credencial de teste. O Jira foi escrito contra a API REST v3 (`/rest/api/3/search/jql`) e testado só com dublês; a
documentação oficial não pôde ser lida por ferramenta nesta sessão. Tratar o conector do Jira como não comprovado até
uma sincronização real.

Também não foi feito: conferência no aplicativo Tauri empacotado, leitor de tela e alto contraste. As telas foram
vistas num navegador com host simulado, em 1280 e 760 pixels de largura.

## 9. Limitações conhecidas

- Sem cofre em Linux e macOS: a central abre, mas não cria conexões.
- Jira Server e Data Center não são atendidos (autenticação e API diferentes).
- O histórico de status do Jira vem junto com a busca e pode vir truncado em issues com muitas mudanças.
- Estimativas, sprints e bloqueios marcados no Jira não são lidos (campos personalizados, variam por site).
- No GitHub não há data de início de trabalho nem histórico de status; só criação, atualização e fechamento.
- Itens apagados no provedor não são removidos do que já foi sincronizado.
- A sincronização não emite eventos: a interface relê a lista a cada 1,5 s enquanto houver uma em andamento.

## 10. Fase 3: gestão básica (entregue em 2026-11-11)

- **Portfólio** (`/portfolio`, `management.portfolio`): uma linha por projeto autorizado, com abertos, em andamento,
  entregues em 8 semanas e prazo vencido por tipo de dado, e a leitura mais antiga. Nome e caminho abreviado vêm do motor
  (`WorkspaceAuthorizer.Projects()`), sem depender do host. Cada linha usa só os dados do próprio projeto.
- **Pontos de atenção por regra** (`PortfolioSignals`): nenhuma fonte; fonte nunca sincronizada; última sincronização
  parcial ou com falha; conexão sem acesso; dados lidos há mais de 7 dias; itens abertos com prazo vencido. São condições
  fixas, não previsão. Quem tem mais pontos aparece primeiro.
- **Indicadores de fluxo** (`management.metrics`, tela Acompanhamento), por tipo de dado e nunca somando tipos:

| Indicador | Fórmula | Base | Sem valor quando |
| --- | --- | --- | --- |
| Em andamento (WIP) | itens com categoria "em andamento" agora | situação atual | não há itens |
| Entregues por semana | entregues com conclusão em cada janela de 7 dias terminando agora; média = total ÷ semanas | data de conclusão | não há itens |
| Novos por semana | itens criados em cada janela | data de criação | não há itens |
| Lead time | dias corridos da criação à conclusão dos entregues no período; mediana e percentil 85 pela posição ⌈p·n⌉ | criação e conclusão | menos de 5 entregues no período |
| Prazo vencido | abertos com prazo anterior a hoje (UTC) | prazo do item | a fonte não informa prazo (GitHub) |

  "Entregue" (`DeliveryRules`): concluído, com data de conclusão, sem ser issue descartada ou duplicada nem pull request
  fechado sem merge. Janelas incluem o início e excluem o fim. Uma casa decimal, meio arredondado para cima. Período
  padrão de 8 semanas (2 a 26). Nenhum indicador é por pessoa.
- A resposta marca `complete: false` quando alguma fonte teve a última sincronização parcial ou com falha, e lista em
  `notCalculated` o que foi pedido e ainda não existe (cycle time, bloqueios, mudança de escopo, previsibilidade,
  previsão), com o motivo. A tela mostra esses itens como ausentes.
- Testes em `ManagementTests` (10) conferem cada número à mão; fixtures em `contracts/fixtures/management`.

## 10b. Fase 4: inteligência de entrega (entregue em 2026-11-11)

Na tela Acompanhamento, acima dos indicadores de fluxo: **Riscos** e **Previsão de entrega**. Os dois são calculados
no motor .NET (`ForecastCalculator`, `RiskRules`), só com os dados sincronizados e o relógio. Nenhum modelo de
linguagem participa do cálculo.

**Previsão** (`management.forecast`), por tipo de dado:

- Método: simulação de Monte Carlo com 10.000 rodadas. Cada rodada sorteia, semana a semana, uma das semanas do
  histórico (até 12) e soma as entregas até zerar o trabalho aberto. A semente vem dos próprios dados: os mesmos itens
  no mesmo instante dão sempre a mesma previsão.
- Cenários: otimista (percentil 15), provável (50) e conservador (85), cada um com semanas e data. A tela diz o que o
  percentil significa ("85% das simulações terminam até esta data").
- Sem previsão quando não há item aberto, quando os dados cobrem menos de 6 semanas, quando houve menos de 5 entregas
  no histórico, ou quando o ritmo não conclui em 5 anos. A tela mostra o motivo, nunca uma data.
- Confiança começa alta e cai um nível por motivo, com o motivo exposto: histórico com menos de 10 semanas; mais de um
  terço das semanas sem entrega; variação alta (desvio maior que a média); escopo crescendo (chegadas ao menos 20%
  acima das entregas e 3 itens a mais); fonte incompleta.
- Premissas e o que não é considerado vêm na resposta e aparecem em "Como a previsão é calculada".

**Riscos** (`management.risks`), por regra fixa, com evidências, itens (até 10), tendência quando compara períodos,
recomendação e origem `rule`:

| Regra | Condição | Severidade |
| --- | --- | --- |
| `overdue` | Item aberto com prazo anterior a hoje | alta com 5 ou mais, ou 20% dos itens com prazo; média com 2 |
| `stalled-wip` | Em andamento sem atualização há mais de 14 dias | alta com 5; média com 2 |
| `review-queue` | Pull request aberto há mais de 7 dias | alta com 5; média com 3 |
| `scope-growth` | Últimas 4 semanas: chegadas 50% acima das entregas e 3 itens a mais | alta com o dobro e 6 a mais |
| `throughput-drop` | Entregas das últimas 4 semanas até metade das 4 anteriores (com ao menos 4 antes) | alta quando caiu a zero |

Riscos sugeridos por IA, quando existirem (Fase 5), virão com outra origem e separados destes. O histórico de
acompanhamento de cada risco ainda não é guardado: os riscos são recalculados a cada leitura.

Testes em `DeliveryIntelligenceTests` (12), com cenários e evidências conferidos à mão; fixtures em
`contracts/fixtures/management/forecast.json` e `risks.json`.

Limitações: cycle time continua fora (falta a data de início do trabalho); "parado" usa a data da última atualização
no provedor, que muda com qualquer edição; item aberto abandonado conta como trabalho restante; a previsão não
considera dependências, bloqueios, férias nem mudança de equipe.

## 10c. Fase 5: assistente gerencial (entregue em 2026-11-11)

Nova tela **Gestão > Assistente** (`/gestao/assistente`). A pessoa pergunta em linguagem natural sobre o projeto
aberto, e a resposta vem separada como o pedido exige: dados observados, cálculos, inferências, recomendações e
informações indisponíveis.

Como funciona (`Mariliz.Application/Management/Assistant`):

1. O motor monta um **pacote de evidências** (`EvidenceBuilder`) com os fatos que ele mesmo calculou: fontes e data de
   leitura, indicadores de fluxo, previsão com confiança e fatores, riscos por regra e o que ainda não é calculado.
   Cada fato tem um id e traz os números como o motor os calculou.
2. O modelo recebe a pergunta e os fatos, com a instrução de usar só os fatos, citar os ids e não fazer contas novas.
   A chamada passa pelo proxy de modelo do motor (`ModelGateway`): a chave não sai do motor.
3. O motor **confere a resposta** (`AnswerVerifier`) antes de devolvê-la. Uma afirmação, inclusive o resumo, só passa
   se citar ao menos um fato existente e se todo número que ela traz aparecer nos fatos citados (datas valem por
   partes; "1,5" e "1.5" são o mesmo número). O que não passa aparece como descartado, com o motivo.

Privacidade e consentimento:

- Nada é enviado sem chave de modelo configurada e sem o clique em "Perguntar". Antes disso, a tela mostra exatamente
  o que será enviado (a pergunta e a lista de fatos).
- Não vai código, documento nem arquivo do projeto. Títulos de itens só entram quando todas as fontes do projeto
  autorizaram servir de contexto (a política de contextualização da Fase 2); sem isso, vão só os identificadores.
- A auditoria registra cada resposta (quantos fatos, citados, descartados, tokens e o hash do que foi enviado), sem o
  texto da pergunta.

**Decisão: sem LangGraph nesta fase.** O plano previa um workflow LangGraph. Uma pergunta hoje é uma chamada só ao
modelo, sobre fatos que o .NET já calculou, e o pedido pede para não usar LangGraph onde não há decisões nem etapas.
Um workflow no runtime de IA entra quando houver investigação em várias etapas (por exemplo, cruzar riscos com o
histórico de engenharia na Fase 7). Também não há Agentic RAG aqui: os fatos vêm do motor, não de busca em
documentos.

Testes em `ManagementAssistantTests` (9), com um provedor de mentira que devolve afirmações boas e ruins de propósito;
fixtures em `contracts/fixtures/management/assistant-evidence.json` e `assistant-answer.json`.

Limitações: a verificação confere números e citações, não o sentido da frase (uma inferência mal formulada que cita o
fato certo passa); o modelo pode dizer "indisponível" para algo que os fatos têm; não há histórico de perguntas nem
relatórios salvos; o assistente responde sobre um projeto por vez. **Não foi testado com um modelo real:** só com o
provedor de mentira.

## 11. Próximas fases e dependências

| Fase | Entrega | Depende de |
| --- | --- | --- |
| 4b. Complementos de entrega | Cycle time, histórico de acompanhamento dos riscos, impacto de mais capacidade na previsão. | Histórico de status (Jira já traz; GitHub exige a API de eventos da issue). |
| 5b. Assistente: complementos | Histórico de perguntas, relatórios salvos, comparação entre projetos, workflow LangGraph para investigação em etapas. | Validação com modelo real. |
| 6. Engenharia | Perfil tecnológico persistido a partir da descoberta; gerenciamento de Harness, MCP e conhecimento. | Nenhuma das anteriores: pode andar em paralelo. |
| 7. Inteligência compartilhada | Análises cruzadas autorizadas. | Fases 5 e 6. |

Indicadores e previsão serão serviços determinísticos no .NET. O LangGraph entra só para explicar resultados.

## 12. Troca de nome para Spock Studio

O nome visível passou a ser **Spock Studio** (System of Programming, Organization, Code and Knowledge): nome do aplicativo e do instalador (`productName`), título da janela e da
página, marca no menu, textos da interface ("Modelo de implementação Spock", "Motor Spock") e ícones.

- **Marca, o olhar lógico:** uma sobrancelha reta, a outra erguida em teal, e dois olhos. É a expressão de curiosidade
  cética associada ao personagem, sem rosto, orelha, franja, insígnia, saudação nem outro elemento de obra de
  terceiros. No wordmark "spock", o braço do "k" é a mesma sobrancelha erguida em teal, e o par ícone e nome se lê como
  uma coisa só. Fonte em `desktop/host/src-tauri/icons/source/spock-studio.svg` (o PNG de 1024 px ao lado é
  renderizado dela); o conjunto `.ico`/`.icns`/PNGs é gerado por `pnpm tauri icon`. Foi escolhido entre cinco direções
  (olhar lógico, S afiado, prompt cético, o símbolo "portanto" e o S em trilha de circuito).
- **Mantido de propósito, para não quebrar quem já usa:** `identifier` do Tauri (pasta de dados do host e atualização do
  instalador), `mariliz.yaml`, `mariliz.lock` e `.mariliz/` nos projetos, a pasta de dados do motor, o id
  `implementationModel: mariliz`, o sidecar `mariliz-runtime`, as variáveis `MARILIZ_*`, os namespaces `Mariliz.*` e o
  pacote `@mariliz/ui-kit` publicado no npm.
- **Ainda com o nome antigo:** os textos que o produto grava nos projetos (agentes, regras e hook em
  `runtime/Mariliz.Application/Assets`). Mudá-los altera o hash do Harness aplicado, e cada projeto já configurado
  receberia uma atualização no próximo plano.
- **Não conferido:** o instalador gerado com o nome novo. Trocar o `productName` muda a pasta de instalação; quem tem a
  versão anterior pode ficar com as duas instaladas.

### Sem travessão no código e na interface

Código, interface, contratos, saída da CLI e os arquivos que o produto grava nos projetos não usam travessão (U+2014
nem U+2013); no lugar, dois-pontos, vírgula ou parênteses. Quando um indicador não tem valor, a tela escreve "sem dado".
`TypographyTests` percorre os arquivos versionados (fora de `docs/`) e falha se encontrar um. A documentação ainda tem
travessões e não é coberta pelo teste.

## 13. Decisões que dependem de você

1. **Credencial de teste** para validar GitHub e Jira com dados reais (um repositório e um projeto de teste).
2. **Cofre em Linux e macOS**: implementar com libsecret e Keychain, ou manter a central só no Windows por ora.
3. **Chave de modelo**: migrar `model.json` para o mesmo cofre (muda o arquivo de quem já configurou a chave).
4. **Identidade e papéis**: o produto continua de uma pessoa por instalação, ou passa a ter organização e login.
5. **Textos gravados nos projetos** (`Assets/`): trocar o nome também neles, sabendo que gera atualização do Harness.
   (A skill `harness-contract` já mudou neste ciclo, para tirar dois travessões; quem já aplicou o Harness verá a atualização.)
6. **Identificadores internos** (`mariliz.yaml`, `.mariliz/`, pasta de dados, namespaces): manter, ou migrar com
   compatibilidade de leitura dos nomes antigos.
7. **Travessões na documentação**: limpar também `docs/`, se quiser a regra no projeto inteiro.
