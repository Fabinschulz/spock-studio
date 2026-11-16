# Auditoria de experiência, identidade e produto

> Parte da auditoria estratégica do SPOCK (2026-11-15). Navegação e método em [README.md](README.md).
> Somente leitura: nenhum código foi alterado. Legenda de estados: A implementado e validado, B parcial, C só documentado,
> D não implementado, E substituído por melhoria válida, F substituído sem justificativa, G regressão, H conflito entre
> documento e código, I decisão pendente, J não se aplica.

Data: 2026-11-15. Auditoria só de leitura (nenhum arquivo do repositório alterado, nenhum comando git de escrita, app não executado).
Branch observada: `feat/conformidade-licencas` (limpa). Caminhos relativos à raiz do repositório.

Legenda de classificação: **A** implementado e validado · **B** parcial · **C** só documentado · **D** não implementado ·
**E** substituído por melhoria válida · **F** substituído sem justificativa · **G** regressão · **H** conflito doc x código ·
**I** decisão pendente · **J** não se aplica.

"Validado" aqui significa: há código e teste de tela (vitest + axe) no repositório. Nenhuma tela foi renderizada nesta
auditoria; as únicas evidências visuais são as capturas `docs/validacao-real-2026-11-14/ui-0*.png` (900x650, Tauri real).

---

## 1. Mapa de navegação real

Fonte única dos caminhos: `desktop/ui/src/shared/routing/paths.ts:1-24`; rotas: `desktop/ui/src/app/routes.tsx:31-232`;
menus: `desktop/ui/src/app/layouts/app-shell/navigation.tsx:46-143`.

### 1.1 Shell

```
+----------------------------------------------------------------------------------------+
| [logo "olhar"] spock | [Projeto v] | [Engenharia | Gestão] | [Buscar Ctrl K]            |  AppHeader.tsx:225-243
+-----------------+----------------------------------------------------------------------+
| Início          |  Página (Page: título, trilha, descrição)                            |
| ENGENHARIA (9)  |                                                                      |
|  ou GESTÃO (4)  |                                                                      |
| PLATAFORMA (7)  +----------------------------------------------------------------------+
| Recolher menu   |  Painel inferior: Terminal | Saída  (BottomPanel)                    |
+-----------------+----------------------------------------------------------------------+
| Sistema local: estado do motor e da inteligência     Terminal  Saída  Atalhos  v0.1.0   |  StatusBar.tsx:118-151
+----------------------------------------------------------------------------------------+
```

- Alternador "Ambiente" (Engenharia/Gestão) é `<nav aria-label="Ambiente">` com links (`EnvironmentSwitch.tsx:257-276`);
  é visão, não permissão (`environment.ts:3`).
- Em `/configuracoes/*` o rail troca para o menu de Configurações com "Voltar para ..." (`AppShell.tsx:91-95`,
  `SettingsNav.tsx`).
- Paleta de comandos (Ctrl K) indexa todos os grupos de menu (`command-palette/commands.ts:5-28`); atalhos K, B, J, `
  e / (`shortcuts/shortcuts.ts:16-31`).
- Selo de atividade em "Execuções" (aprovações pendentes em tom de atenção, senão em andamento) (`SideNav.tsx:74-94`).

### 1.2 Telas (24 rotas)

| Grupo de menu | Rótulo | Rota | O que faz | Exige motor | Arquivo |
| --- | --- | --- | --- | --- | --- |
| App | Início | `/` | Herói "Como podemos ajudar?", 6 cartões de atividade (2 "Em breve"), seleção de projeto e recentes | não | `features/home/home-page.tsx:14-38` |
| Engenharia | Visão geral | `/projeto` | Perfil tecnológico, inventário ("O que já existe"), lista de execuções (sem link), links "Mais sobre o projeto" | sim | `features/project/project-page.tsx:25-117` |
| Engenharia | Harness | `/projeto/harness` | Harness aplicado, dispensas, revisões | sim | `features/project/harness-page.tsx` |
| Engenharia | Conhecimento | `/projeto/conhecimento` | Regras, fontes e memória do índice | sim | `features/project/knowledge-page.tsx` |
| Engenharia | Configurar | `/configurar` | Jornada de 6 etapas: Projeto, Modelo e ambiente, Plano, Revisão, Aplicação, Resultado | não | `features/configure-project/constants/journey-steps.ts:3-10` |
| Engenharia | Pedido de engenharia | `/pedido` | Formulário de pedido; workflow de análise com teto de 8.000 tokens; aprovação na tela de execução | não | `features/engineering-request/engineering-request-page.tsx` |
| Engenharia | Agentes de IA | `/agentes` | Pergunta única a uma CLI de IA instalada (Claude Code, Codex etc.), só leitura, com cancelar; seção "O Spock dentro da sua ferramenta" (servidor MCP do Spock para IDEs) | sim | `features/agents/agents-page.tsx:36-110`, `components/ide-connection-section.tsx:16-17` |
| Engenharia | Da ideia ao projeto | `/ideia` | Especificação, arquitetura e backlog etapa por etapa com aprovação | sim | `features/idea/idea-page.tsx:50-51` |
| Engenharia | Segurança | `/seguranca` | Varredura de credenciais e permissões; correção com diff e aprovação | sim | `features/security/security-page.tsx:35-98` |
| Engenharia | Catálogo | `/projeto/catalogo` | Fichas dos componentes do Harness (agentes, skills, regras, hooks, ciclo de especificação), política dos agentes, inventário MCP do `.mcp.json` | sim | `features/project/catalog-page.tsx:15-50` |
| Gestão | Portfólio | `/portfolio` | Todos os projetos; "Perguntar sobre o portfólio" | sim | `features/management/portfolio-page.tsx` |
| Gestão | Acompanhamento | `/gestao` | 10 seções empilhadas: atividades, riscos, histórico, execuções ligadas, previsão, marcos, fluxo, decisões, fontes, não calculado | sim | `features/management/management-page.tsx:64-93` |
| Gestão | Assistente | `/gestao/assistente` | Pergunta e resposta com fatos, cálculo, interpretação e indisponível | sim | `features/management/assistant-page.tsx:78-79` |
| Gestão | Relatório | `/gestao/relatorio` | Relatório do projeto | sim | `features/management/report-page.tsx` |
| Plataforma | Execuções | `/execucoes` | Central com filtros (projeto, tipo, estado), "Carregar mais", painel de detalhe em `Drawer` | sim | `features/runs/runs-page.tsx:40-125`, `components/run-detail-panel.tsx` |
| Plataforma | Linha do tempo | `/linha-do-tempo` | Acontecimentos do projeto, só leitura | sim | `features/timeline/timeline-page.tsx:53-70` |
| Plataforma | Auditoria | `/auditoria` | Trilha do que mudou no acesso do Spock ao projeto | sim | `features/audit/audit-page.tsx:34-35` |
| Plataforma | Integrações | `/integracoes` | Conexões GitHub/Jira, vínculos por projeto, alcance de cada conexão | sim | `features/integrations/integrations-page.tsx:28-39` |
| Plataforma | Conexões cloud | `/cloud` | AWS, Azure, GCP só leitura, por projeto | sim | `features/cloud/cloud-page.tsx:39-93` |
| Plataforma | Infraestrutura | `/infraestrutura` | OpenTofu/Terraform: instalação gerenciada, versão por projeto, validar/planejar/aplicar com aprovação do plano, drift | sim | `features/infrastructure/infrastructure-page.tsx:62-100`, `components/runtime-panel.tsx`, `components/plan-approval.tsx` |
| Plataforma | Configurações | `/configuracoes` | Redireciona para Minha IA | n/a | `routes.tsx:197` |
| Configurações | Minha IA | `/configuracoes/ia` | Provedores e chaves, Modelo por finalidade, Teto de tokens por finalidade, Ferramentas de IA neste computador | sim | `features/settings/model-settings-page.tsx:19-28`, `components/purposes-section.tsx:21`, `components/budgets-section.tsx:19`, `components/ai-tools-section.tsx:17` |
| Configurações | Diagnóstico | `/configuracoes/diagnostico` | Diagnóstico copiável e registro recente | não | `features/settings/diagnostics-page.tsx:27` |
| (sem menu) | Execução | `/execucao/:runId` | Estado, etapas, linha do tempo, impacto, evidências, avaliação, uso, limitações, ações propostas, desfazer, Aprovar | sim | `features/execution/execution-page.tsx:30-118` |

Contagem de itens visíveis no rail em Engenharia: 1 + 9 + 7 = **17** (mais "Recolher menu"). Na captura
`ui-01-inicio.png` (janela padrão 900x650, `tauri.conf.json:18-21`) o rail rola e Auditoria, Integrações, Conexões
cloud, Infraestrutura e Configurações ficam abaixo da dobra; o painel inferior (terminal) ocupa cerca de 40% da altura.

Rotas órfãs: nenhuma. Toda rota tem item de menu, exceto `/execucao/:runId` (alcançada pela central, painel e links).

---

## 2. Tabela de capacidades (UX-xx)

| ID | Capacidade | Classe | Evidência e comentário |
| --- | --- | --- | --- |
| UX-01 | Onboarding / primeira execução | **D** | Não há fluxo de primeiro uso (nenhuma checagem guiada de chave de modelo, ferramentas de IA, cofre ou Python). O Início é a porta (`home-page.tsx:30-37`). Busca por "boas-vindas/onboarding" sem resultado em `desktop/ui/src`. O blueprint chama de "onboarding" a jornada Configurar (`docs/blueprints/experience-blueprint.md:51`), o que não cobre o primeiro uso do produto. |
| UX-02 | Selecionar projeto | **A** | Seletor no topo (`ProjectSwitcher`, `AppHeader.tsx:228`), recentes, restauração do último projeto (`project-selection/hooks/use-project-selection.ts:12-56`). Teste `project-switcher.test.tsx`. |
| UX-03 | Criar projeto | **B** | Projeto = pasta existente aberta pelo seletor. Não há "novo projeto". "Da ideia ao projeto" gera especificação, arquitetura e backlog dentro de uma pasta já aberta (`idea-page.tsx:51`). |
| UX-04 | Gerenciar projetos (renomear, remover dos recentes, nome de exibição) | **D** | Nenhuma ação de renomear/remover/esquecer na UI (busca sem resultado). A proposta previa "nome de exibição editável guardado pelo motor" como P0 (`docs/plataforma/SPOCK_PRODUCT_EVOLUTION.md:108`). |
| UX-05 | Configuração do ambiente do projeto (Harness) | **A** | Jornada Configurar com plano, diff, aprovação, aplicação com cancelar-e-desfazer (`steps/run-step/run-step.tsx:20-115`), revisão com "Cancelar sem aplicar" (`steps/review-step/review-step.tsx:137-150`). |
| UX-06 | Configuração de modelo: provedores e chaves | **A** | "Provedores" em Minha IA (`model-settings-page.tsx:22-28`); chave no cofre; aviso quando falta chave (`assistant-page.tsx:91`). Validação com modelo real ainda pendente (memória do dono). |
| UX-07 | Modelo por finalidade | **A** | `purposes-section.tsx:21`. |
| UX-08 | Orçamento (teto de tokens por finalidade) | **A** | `budgets-section.tsx:19`. Não há orçamento em dinheiro nem consumo agregado por período (uso aparece por execução em `UsageSection`, `execution-page.tsx:100`). |
| UX-09 | Agentes (ver o que fazem) | **B** | Fichas no Catálogo (`catalog-page.tsx:15-23`), só leitura por desenho. Ambiguidade: o item de menu "Agentes de IA" (`navigation.tsx:76`) não mostra os agentes do Harness; é uma caixa de pergunta para CLIs externas. Dois conceitos com o mesmo nome. |
| UX-10 | Skills | **B** | Só como fichas do Catálogo e componentes do plano; sem gestão própria (por desenho, `SPOCK_PRODUCT_EVOLUTION.md:141-144`). |
| UX-11 | MCP | **B** | Inventário só leitura do `.mcp.json` (`project/components/mcp-section.tsx:19-29`) e servidor MCP do Spock para IDEs com snippets para Claude Code, Codex, Cursor e Gemini (`agents/domain/ide-setup.ts:27-56`). Gerenciar MCP pelo plano: não implementado (P2 na proposta). Localização: o servidor MCP do Spock fica dentro de "Agentes de IA", não em Integrações nem no Catálogo. |
| UX-12 | Plugins | **D** | Zero ocorrências de "plugin" em `desktop/ui/src`. A memória do dono chama o servidor MCP local de "plugin de contexto"; a UI não usa o termo nem oferece extensões de terceiros. |
| UX-13 | Workflows | **B** | Não há lista nem catálogo de workflows. Eles aparecem como telas avulsas: Pedido de engenharia, Da ideia ao projeto, Segurança, Investigação (só como tipo de execução, `shared/utils/lib/run-labels.ts:1-12`). |
| UX-14 | Execuções (central) | **A** | `runs-page.tsx:40-125`: filtros, vazio diferente de filtro sem resultado (linhas 97-101), paginação, `Drawer`. |
| UX-15 | Histórico | **B** | Execuções, Linha do tempo, Auditoria e Revisões do Harness existem. Mas o texto do pedido nunca é guardado nem mostrado (`runs/domain/run-summary.ts:23-28`); o resumo cai em "Execução do Spock Studio." para agent, security, idea e infrastructure, que faltam em `DEFAULT_REQUEST` (linhas 13-20) embora estejam em `run-labels.ts:5-9`. Na Visão geral as execuções não são links (`project-page.tsx:101-117`). |
| UX-16 | Evidências | **A** | Seção "Evidências" na execução (`execution-page.tsx:86-97`), respostas do assistente com citação (`assistant-answer.tsx:73`). |
| UX-17 | Configurações | **B** | Só Minha IA e Diagnóstico (`navigation.tsx:125-137`). Faltam "Sobre", licenças/avisos de terceiros na UI e pasta de dados, previstos em `SPOCK_DESIGN_DIRECTION.md:141` e `experience-blueprint.md:42`. A única menção a licença na UI é no painel de instalação do Terraform (`runtime-panel.tsx`). |
| UX-18 | Infraestrutura (IaC) | **A** | Separação analisar/preparar/executar dita na tela (`infrastructure-page.tsx:62`); aprovação do plano exato (`plan-approval.tsx:21-46`). |
| UX-19 | Dependências gerenciadas (OpenTofu/Terraform/Python) | **B** | OpenTofu/Terraform: estado gerenciado, externo, adotado, versão mínima, remoção (`infrastructure/domain/labels.ts:6-30`, `runtime-panel.tsx:54,140`). Python: vem embarcado (`tauri.conf.json:44-47`); a UI só mostra "inteligência local parada/pronta" na barra de status (captura `ui-01`). Não há tela de estado do Python. |
| UX-20 | Aprovações | **B** | Aprovação por execução (`execution/components/approve-button.tsx:21-25`), selo no menu. Não há caixa de entrada de aprovações; o caminho é o filtro "Estado" da central. A execução não tem título do que se aprova nem aviso no topo: só o chip "Aguardando aprovação" e o botão no fim da página (`execution-page.tsx:68-115`; captura `ui-04-aprovacao.png`). Não há "Recusar/Cancelar" na execução aguardando aprovação (busca sem resultado em `features/execution`). |
| UX-21 | Erros | **A** | `ErrorState` com "Tentar de novo" em 28 arquivos; `RequiresHost` explica a dependência do motor (`requires-host.tsx:21`); mensagens de falha específicas (ex.: `agents-page.tsx:30-34`). |
| UX-22 | Retorno de execução: progresso | **B** | Etapas e linha do tempo na execução; frase de estado na jornada. Nenhuma barra de progresso (`LoadingBar` sem uso fora do ui-kit); o padrão é texto ("Lendo…"). |
| UX-23 | Retorno de execução: cancelamento | **B** | Existe em Configurar (`run-step.tsx:26-115`) e em Agentes de IA (`agents-page.tsx:97-101`). Ausente no Pedido de engenharia, em Da ideia ao projeto e na tela de Execução, embora `run.cancel` exista no cliente (`shared/api/engine-client.ts:30`). |
| UX-24 | Estados vazios | **A** | `EmptyState` em 15 arquivos, `NoProjectState` em 9, sempre com próximo passo (ex.: `management-page.tsx:104-114`). |
| UX-25 | Acessibilidade | **B** | Fortes: link "Ir para o conteúdo" (`AppShell.tsx:82-84`), landmarks rotulados, `aria-current` por forma e texto, 244 atributos `aria-*`, 26 suítes com axe, atalhos, foco ao trocar de tela (`AppShell.tsx:76-78`). Pendente: conferência com leitor de tela, zoom e alto contraste no Tauri empacotado, "nunca foi feita" (`SPOCK_DESIGN_DIRECTION.md:382`). |
| UX-26 | i18n | **J / I** | Só pt-BR, strings fixas no código, sem biblioteca de i18n (`desktop/ui/package.json:16-31`); `app.language: 'pt-BR'` (`shared/config/app.ts:5`). O instalador NSIS oferece PortugueseBR e English (`tauri.conf.json:51`): o instalador em inglês leva a um app só em português. Internacionalizar é decisão de produto. |
| UX-27 | Paleta de comandos e atalhos | **A** | `command-palette/*`, `shortcuts/*` com testes. |
| UX-28 | Terminal e saída integrados | **A** | `BottomPanel` (Terminal/Saída). Na captura, o terminal mostra o caminho completo de uma pasta temporária local; ocupa muito espaço na janela padrão. |
| UX-29 | Visão geral como página de entrada com "próximo passo" | **D** | A proposta queria Visão geral com estado do Harness, última execução, aprovações pendentes e próximo passo, substituindo o Início (`SPOCK_PRODUCT_EVOLUTION.md:91-93`, `SPOCK_DESIGN_DIRECTION.md:158`). Hoje o Início continua genérico e a Visão geral não mostra aprovações nem próximo passo (`project-page.tsx:33-50`). |
| UX-30 | Harness com abas (Estado, Catálogo, Revisões) | **E** | Viraram rotas separadas no menu (Harness, Catálogo). Funciona, mas aumenta o rail. `TabBar` só é usado no painel inferior. |
| UX-31 | Visibilidade do ciclo de especificação (`.specs`) | **D** | Nenhuma leitura de `.specs`/fase do pedido na UI. Proposta em `SPOCK_PRODUCT_EVOLUTION.md:162-170`. |
| UX-32 | Itens da jornada "Em breve" no Início | **H** | "Importar requisitos: Em breve" (`journeys.ts:50-54`) enquanto "Da ideia ao projeto" já gera especificação e backlog. "Modernizar sistema: Em breve" (linha 61-65). O Início promete o que não há e esconde o que há (Segurança, Infraestrutura, Ideia não têm cartão). |
| UX-33 | Linguagem de fase de desenvolvimento na UI | **G (leve)** | "nesta fase" em 3 textos visíveis: `engineering-request-page.tsx:17`, `model-step/model-step.tsx:67`, `model-step/ai-environment-section.tsx:30`. |

---

## 3. É "plataforma de engenharia" ou "mais um chat de IA"?

**Veredito: a UI comunica plataforma de engenharia, não chat.** Evidências:

- Decisão explícita registrada: a direção "Sessão (conversa-canvas)" foi rejeitada porque "implica um interlocutor que
  não existe" e "venderia uma conversa que o motor não tem" (`docs/redesign/proposta-de-redesign.md:89-118`). "Conversa
  livre" fica fora do escopo (`SPOCK_PRODUCT_EVOLUTION.md:24-25`) e "composição centrada na conversa" não se aproveita
  (`SPOCK_DESIGN_DIRECTION.md:83`).
- Shell de workbench com projeto, ambiente, menu de engenharia, central de execuções, terminal.
- Princípio "a IA propõe; o motor decide; a pessoa aprova o plano exato" aparece nas descrições de tela (Ideia,
  Segurança, Infraestrutura, Pedido).

**Superfícies com cara de chat** (todas de turno único, sem histórico de conversa):

| Superfície | Evidência | Risco |
| --- | --- | --- |
| Agentes de IA: "O que você quer saber" + "Pedir ao agente" | `agents-page.tsx:76-96` | Médio: é um campo livre de pergunta a CLIs externas, o mais próximo de um chat. O ícone e o nome competem com os agentes do Harness. |
| Assistente (Gestão), com ícone `ChatIcon` | `navigation.tsx:95`, `assistant-page.tsx:78-79` | Baixo: resposta estruturada em fato/cálculo/interpretação/indisponível. |
| Perguntar sobre o portfólio | `portfolio-page.tsx` | Baixo. |
| Herói do Início: "Como podemos ajudar?" | `features/home/hero/hero.tsx:7-12` | Médio: frase de atendimento/chatbot, genérica, sem dizer o que o produto é. |

**Laço principal que a pessoa vive hoje:** abrir pasta -> Configurar (descoberta, plano, diff, aprovar, aplicar,
verificar) -> Pedido de engenharia (análise com evidência) -> Execução (aprovar ações, desfazer) -> Execuções/Linha
do tempo. Em paralelo, Gestão lê GitHub/Jira e calcula sinais.

**Lacuna de posicionamento "ciclo de engenharia":** o próprio produto documenta que Specification, Execution e Review
acontecem fora (no Claude Code) e Learning não existe (`SPOCK_PRODUCT_EVOLUTION.md:148-160`); a UI não lê esse trabalho
(UX-31). Hoje o Spock cobre bem Discovery, Analysis, Planning, Validation do Harness e Governança, mas não mostra o ciclo
completo de um pedido até a entrega. A ligação execução <-> item (issue/PR) existe (`runs/components/run-links.tsx:53-88`).

---

## 4. Identidade

### 4.1 Nomes visíveis ao usuário

| Superfície | Nome | Evidência |
| --- | --- | --- |
| Nome do produto no instalador e no app | **Spock Studio** | `desktop/host/src-tauri/tauri.conf.json:3` |
| Título da janela | Spock Studio | `tauri.conf.json:17` |
| Título HTML | Spock Studio | `desktop/ui/index.html:6` |
| Config da UI | `name: 'Spock Studio'`, tagline "Engenharia de software assistida por IA" (tagline sem nenhum uso) | `desktop/ui/src/shared/config/app.ts:2-3` |
| Marca no rail | logo + wordmark "spock" (minúsculo, SVG) | `AppShell.tsx:87-89`, `shared/components/ui/atoms/logo/Logo.tsx:22-33` |
| Nome acessível da marca | "Spock Studio, página inicial" | `AppShell.tsx:87` |
| Textos de tela | "Spock Studio" em 27 linhas; "Spock" sozinho em 40 linhas ("O Spock...", "Motor Spock", "Modelo de implementação Spock", "pasta do Spock") | contagem em `desktop/ui/src` sem testes |
| Servidor MCP registrado nas IDEs | `spock` / `spock-<projeto>` | `agents/domain/ide-setup.ts:1,24` |
| Mensagem de pânico do host | "falha ao iniciar o aplicativo Spock Studio" | `src-tauri/src/lib.rs:237` |
| "SPOCK" em caixa alta ou a expansão "System of Programming, Organization, Code and Knowledge" | **0 ocorrências na UI** | só em comentários dos SVG (`icons/source/spock-studio.svg:2`, `desktop/ui/public/favicon.svg:2`) e em docs |
| README na raiz | **não existe** | nenhum `README*` fora de `docs/compliance/README.md` |

**Inconsistência central:** o nome oficial informado é "SPOCK: System of Programming, Organization, Code and Knowledge",
mas o produto se chama "Spock Studio" (instalador, janela) e "spock" (wordmark), e nunca mostra a expansão. O documento
de marca afirma que a sigla "é a única definição usada na documentação, na interface e nos materiais técnicos"
(`docs/compliance/TRADEMARK_ASSESSMENT.md:8-11`), o que não confere com a interface (**H**). A memória do dono pede
"Spock Studio" ao citar o produto; o pedido desta auditoria usa "SPOCK": **I**, decisão do dono sobre o nome canônico
(SPOCK, Spock ou Spock Studio) e se a expansão aparece em "Sobre"/instalador.

### 4.2 "Mariliz" remanescente

**Na UI (`desktop/ui/src`, sem testes): 20 linhas.** Nenhuma usa "Mariliz" como nome do produto.

- **15 linhas visíveis**, todas nomes de arquivo gravados nos projetos (`mariliz.yaml`, `mariliz.lock`, `.mariliz/`):
  `configure-project/components/component-request-notice.tsx:14`, `constants/judge-states.ts:13`,
  `domain/harness-engineer-prompt.ts:13,16` (texto que a pessoa copia), `domain/labels.ts:63,65`,
  `steps/model-step/model-step.tsx:68`, `steps/plan-step/plan-step.tsx:34`, `steps/result-step/proposal-guide.tsx:26`,
  `idea/components/save-panel.tsx:27`, `project/catalog-page.tsx:110`, `project/components/harness-section.tsx:79,142,170`,
  `project/components/knowledge-section.tsx:82`.
- **5 linhas internas** (comentários): `component-request.ts:6`, `harness-engineer-prompt.ts:1`, `harness-api.ts:23`,
  `harness-section.tsx:64`, `shared/schema/harness.ts:6`.
- 34 linhas adicionais em testes; o import `@mariliz/ui-kit` em todo arquivo de tela é interno.
- A direção de texto manda esconder `mariliz.yaml`/`mariliz.lock` atrás de "Detalhes técnicos"
  (`SPOCK_DESIGN_DIRECTION.md:260-266`); hoje eles aparecem na primeira camada (**B**).

**No host e no empacotamento:**

| Item | Valor | Visível? |
| --- | --- | --- |
| `identifier` | `dev.mariliz.ai-engineering` (`tauri.conf.json:5`) | Sim, como nome da pasta de dados em AppData; mantido de propósito (`docs/plataforma/evolucao-engineering-management.md:471-474`) |
| Sidecar | `binaries/mariliz-runtime` (`tauri.conf.json:43`) | Sim: pasta de instalação, Gerenciador de Tarefas e o caminho do executável dentro dos snippets de MCP copiados para as IDEs (`ide-setup.ts:27-56` usa o caminho do motor) |
| Cargo | `name = "mariliz-desktop-host"`, `description = "Host desktop do Mariliz Studio"` (`Cargo.toml:2-4`) | Provavelmente interno; se vai para as propriedades do .exe no Windows: **não verificado** |
| `package.json` | `mariliz-ai-engineering`, `@mariliz/desktop-ui`, `@mariliz/desktop-host` | Interno |
| Variáveis | `MARILIZ_AI`, `MARILIZ_PYTHON` (`src-tauri/src/engine.rs:157,160`) | Interno |

**Nos arquivos que o produto grava nos projetos do usuário** (`runtime/Mariliz.Application/Assets`, 55 linhas com
"mariliz"), com três nomes antigos diferentes, visíveis para o usuário nas ferramentas de IA:
- "Mariliz policy: ..." como motivo de decisão de permissão no Claude Code (`hooks/mariliz-policy.py:62,94`);
  "Managed by Mariliz AI Engineering" (`hooks/mariliz-policy.py:3`);
- "Managed by Mariliz Studio" (`rules/general.md:3`);
- "The Mariliz engine..." (`agents/harness-engineer.md:13`), "Mariliz specification cycle" (`agents/verifier.md:3`).
Mantidos de propósito porque mudá-los altera o hash do Harness aplicado (`evolucao-engineering-management.md:475-477`): **I**.

### 4.3 Associação com Star Trek / Mr. Spock

**Achado de risco alto (reportado, sem proposta de mudança de identidade).**

- O ícone e o logo são "o olhar lógico": uma sobrancelha reta e **a outra erguida**, dois olhos
  (`icons/source/spock-studio.svg:2-10`, `desktop/ui/public/favicon.svg:2-4`, `Logo.tsx:9-19`). No wordmark, o braço do
  "k" é a mesma sobrancelha erguida (`Logo.tsx:31`).
- O comentário embutido nos dois SVG (o `favicon.svg` vai no bundle da interface) diz: "a expressão de curiosidade
  cética que inspira o nome".
- A documentação é mais explícita: "É a expressão de curiosidade cética **associada ao personagem**, sem rosto, orelha,
  franja, insígnia, saudação nem outro elemento de obra de terceiros" (`docs/plataforma/evolucao-engineering-management.md:465-467`).
  Também `docs/ui-modernization.md:27` e `SPOCK_DESIGN_DIRECTION.md:18`.
- A busca preliminar encontrou "SPOCK" nº 1990155 no USPTO de **CBS Studios Inc.** (classe 28)
  (`docs/compliance/TRADEMARK_ASSESSMENT.md:17`); D-12 aguarda parecer (`docs/compliance/LICENSE_DECISIONS.md:19`).

Avaliação: a sobrancelha erguida é o gesto mais reconhecível do personagem. Somada ao nome, ela cria a associação
conceitual que o documento de marca diz que o produto evita (`TRADEMARK_ASSESSMENT.md:10-11`) e que a memória do dono
proíbe ("não associar a personagem/franquia"). A documentação registra a intenção de associação por escrito, o que piora
a posição numa eventual oposição. Isso é **H** (doc de marca x doc de design x artefato) e **I** (decisão do dono, já
apontada em `TRADEMARK_ASSESSMENT.md:42-46`). Não há texto, imagem, saudação ou "Vulcan" em nenhum outro lugar do
código (busca por star trek, vulcan, vulcano, paramount, nimoy sem resultado fora dos trechos citados).

---

## 5. Landing page, apresentação comercial e posicionamento

- **Não há landing page, site, README de produto nem material comercial no repositório** (**D**). O redesign cita "a cor
  do site" (`proposta-de-redesign.md:144`), mas o site não está aqui: **não verificado**.
- Frases de posicionamento existentes:
  - "Engenharia de software assistida por IA" (`shared/config/app.ts:3`), sem uso em tela.
  - "O Spock governa a engenharia assistida por IA de um projeto e mostra a quem gere o que está acontecendo, com dados
    reais" (`SPOCK_PRODUCT_EVOLUTION.md:10-11`).
  - "o Mariliz é uma ferramenta de configurar, aprovar e verificar" (`proposta-de-redesign.md:118`); conceito "AI
    Engineering Workspace" (linha 122).
  - Princípios diferenciadores: conectar não é autorizar; nunca afirmar sucesso não observado; toda resposta separa fato,
    cálculo, inferência e recomendação (`SPOCK_PRODUCT_EVOLUTION.md:31-42`).
- Nenhuma dessas frases aparece no app. O primeiro texto que a pessoa lê é "Como podemos ajudar? Escolha uma atividade
  para começar. Vamos configurar o ambiente conforme as necessidades do seu projeto." (`hero.tsx:7-12`), que não diz o
  que é o SPOCK nem o que o diferencia.
- Documentos de intenção ainda com o nome antigo no título: `docs/product-discovery.md:1`,
  `docs/blueprints/experience-blueprint.md:1`, `docs/ui-modernization.md:1`, `docs/redesign/proposta-de-redesign.md:1`,
  `docs/plataforma/MARILIZ_CURRENT_STATE.md`. Em `docs/product-discovery.md:3-28` a mesma "Nota (2026-10-30)" está
  repetida **seis vezes** (defeito de edição).

---

## 6. Nomes de metodologias de terceiros na UI

| Termo | Ocorrências em `desktop/ui/src` (sem testes) | Situação |
| --- | --- | --- |
| TLC | 0 | Conforme |
| Tech Leads Club | 0 | Conforme no código da tela. Em tempo de execução, o cartão de aprovação do comando mostra o comando real `npx ... @tech-leads-club/agent-skills install -s tlc-spec-driven` vindo do motor (`runtime/Mariliz.Application/Specification/SpecificationManifest.cs:8,13`), por decisão (a pessoa aprova o comando real) |
| Superpowers | 0 | Conforme |
| SDD / spec-driven | 0 | Conforme ("Ciclo de especificação", `catalog-page.tsx:22`) |
| **Harness Engineer** | **7** | **Não conforme** se o pedido do dono incluiu este nome: `configure-project/domain/labels.ts:62`, `domain/participants.ts:44`, `steps/model-step/model-step.tsx:51`, `steps/plan-step/plan-step.tsx:30,34`, `steps/result-step/proposal-guide.tsx:22,30`. O agente é original do produto (`docs/phase-1b-configure-project.md:74`), mas o nome ecoa o termo de mercado "harness engineering" |
| Harness | 91 linhas; item de menu e título de tela | **I**: termo técnico em inglês na primeira camada e jargão de mercado; o glossário explica (`shared/components/ui/atoms/term/glossary.ts:5`). Decidir se vira conceito próprio do SPOCK |

Classificação geral: **B** (conformidade parcial). O contrato e o disco já foram renomeados para "modelo de
implementação" e "estrutura de especificação" (nota em `docs/product-discovery.md:3`).

---

## 7. Pendências de UX adiadas pelo dono: estado atual

| Pendência | Estado | Classe | Evidência |
| --- | --- | --- | --- |
| Renomear "Configurar" x "Configurações" | Os dois continuam lado a lado no rail | **I** | `navigation.tsx:69` ("Configurar") e `:121` ("Configurações"); a direção pedia "Preferências" (`SPOCK_DESIGN_DIRECTION.md:142`); 0 ocorrências de "Preferências" |
| "Modelo e ambiente" | Mantido | **I** | `journey-steps.ts:5`, `model-step.tsx:49`. "Modelo" aqui é o modelo de implementação (`implementation-model-badge.tsx:16`), não o modelo de IA de "Minha IA"; "ambiente" é o ambiente de IA (Claude Code), não o alternador "Ambiente" |
| Seletor "Ambiente" (Engenharia/Gestão) | Mantido | **I** | `EnvironmentSwitch.tsx:261`. Colisão tripla de vocabulário: Ambiente (visão), ambiente de IA (ferramenta), Modelo (implementação x LLM) |
| Resumo do Acompanhamento | Parcial: seção "Atividades" com contagens por categoria; sem faixa de indicadores no topo (em andamento, entregues/semana, lead time, prazo vencido) nem previsão e riscos lado a lado | **B** | `management-page.tsx:76-129` x esquema `SPOCK_DESIGN_DIRECTION.md:163-184` |
| Título/aviso de aprovação na execução | Não feito: título fixo "Execução", sem nome do pedido, sem aviso no topo; botão no fim da página | **D / I** | `execution-page.tsx:68-115`, `approve-button.tsx:21-25`; captura `ui-04-aprovacao.png` |
| Protótipo `src/prototype` | Removido (PR #39) | **A** | `desktop/ui/src/prototype` e `desktop/ui/prototype.html` não existem |
| UI-3 (acompanhamento único de execuções) | Central `/execucoes` com `Drawer` + página `/execucao/:runId` | **E** | `runs-page.tsx`, `run-detail-panel.tsx`; ainda há duas formas de ver uma execução (painel e página), mas com papéis distintos |
| Página "Sobre" (versão fora da barra) | Não feito; versão continua na barra de status | **D** | `StatusBar.tsx:146-148` x `SPOCK_DESIGN_DIRECTION.md:141` |
| Execuções da Visão geral com link | Não feito | **B** | `project-page.tsx:101-117` x problema 6 em `SPOCK_DESIGN_DIRECTION.md:45` |

Divergências de intenção já superadas por decisão (classe **E**): blueprint "sem menu lateral" e jornada linear
(`experience-blueprint.md:45`) substituídos pelo Workbench aprovado; "Fonte do sistema" (`proposta-de-redesign.md:148`)
substituída por Inter (`SPOCK_DESIGN_DIRECTION.md:15`); referências a "Synthra" no blueprint (`experience-blueprint.md:111`)
são o nome antigo do ui-kit.

Observação: `desktop/ui/package.json` fixa `@mariliz/ui-kit` em `0.6.0`; a memória do dono registra 0.6.1 publicada
(**não verificado** no npm).

---

## 8. Crítica

1. **O produto é maior do que o rail consegue mostrar.** 17 itens na janela padrão 900x650; Plataforma fica escondida
   abaixo da dobra; terminal aberto rouba 40% da altura. A janela padrão é pequena para um workbench com rail, painel
   inferior e páginas largas.
2. **Primeira impressão genérica.** "Como podemos ajudar?" e cartões que incluem dois "Em breve" e omitem Segurança,
   Infraestrutura e Ideia. Não há primeiro uso guiado: quem instala não descobre que precisa de chave de modelo, cofre,
   CLIs de IA, Python ou OpenTofu até cair num aviso dentro de uma tela.
3. **Vocabulário colidindo exatamente onde o dono já apontou:** Configurar/Configurações, Ambiente/ambiente de IA,
   Modelo de implementação/modelo de IA, "Agentes de IA" (CLIs externas) x agentes do Harness (Catálogo).
4. **A aprovação, momento mais importante do laço, é a parte menos desenhada da execução:** sem título, sem aviso, sem
   recusar, botão no fim de uma página longa.
5. **Histórico sem memória do pedido:** a central mostra "Pedido de engenharia" sem o texto do pedido; tipos novos caem em
   "Execução do Spock Studio.".
6. **Identidade em três camadas desalinhadas:** SPOCK (sigla oficial, invisível), Spock Studio (instalador), spock
   (wordmark) e restos de Mariliz em sidecar, pasta de dados e nos arquivos gravados nos projetos.
7. **Risco de marca:** o logotipo da sobrancelha erguida e a documentação que o liga "ao personagem" contradizem a
   declaração de identidade e a orientação do dono.
8. **Linguagem de bastidor vazando:** "nesta fase", nomes de arquivo internos (`mariliz.yaml`, `.mariliz/proposal.json`)
   na primeira camada, "Harness Engineer".
9. **Pontos fortes reais:** honestidade de estado, vazios com próximo passo, separação analisar/preparar/aplicar na
   infraestrutura, "conectar não é autorizar" nas integrações, a11y acima da média, paleta e atalhos. A decisão de não ser
   chat está bem fundamentada e cumprida.

## 9. Recomendações (nenhuma mexe em identidade sem aprovação do dono)

Prioridade P0 (decisões do dono, baratas de executar depois):
1. **Decidir o nome canônico** (SPOCK, Spock ou Spock Studio) e onde a expansão "System of Programming, Organization,
   Code and Knowledge" aparece (Sobre, instalador, título do Início). Alinhar `TRADEMARK_ASSESSMENT.md:8-11` com a decisão.
2. **Levar ao jurídico, junto do D-12, o logotipo da sobrancelha erguida** e os textos que o associam ao personagem
   (`evolucao-engineering-management.md:465-467`, comentários dos dois SVG). Só o dono decide se troca.
3. **Fechar as pendências U:** Configurações -> "Preferências" (ou equivalente), renomear a etapa "Modelo e ambiente",
   desambiguar "Agentes de IA" (ex.: "Perguntar a uma ferramenta") e o rótulo do alternador.

P1 (UX):
4. Execução aguardando aprovação: título com o tipo e o pedido, aviso no topo dizendo o que será feito, botão fixo no
   rodapé e uma ação "Recusar/Cancelar" usando `run.cancel`.
5. Cancelar em Pedido de engenharia e em Da ideia ao projeto.
6. Primeiro uso: lista de prontidão (chave de modelo, cofre, CLIs de IA, Python, OpenTofu) no Início ou em Diagnóstico.
7. Início: trocar o herói por uma frase de posicionamento do produto; remover ou reordenar os "Em breve"; incluir
   Segurança, Infraestrutura e Ideia; mostrar aprovações pendentes e próximo passo do projeto aberto.
8. Resumo do Acompanhamento conforme o esquema de 5.1 da direção de design.
9. Completar `DEFAULT_REQUEST` (agent, security, idea, infrastructure) e dar links às execuções da Visão geral.
10. Remover "nesta fase" dos 3 textos; mover `mariliz.yaml`/`.mariliz/` para "Detalhes técnicos".

P2:
11. Rever tamanho padrão da janela (ou abrir maximizada) e painel inferior fechado por padrão.
12. Página "Sobre" com versão, licenças e avisos de terceiros (também exigido pela auditoria de licenças).
13. Decidir i18n: ou retirar English do NSIS, ou planejar a UI em inglês.
14. Decidir se "Harness" e "Harness Engineer" viram conceitos próprios do SPOCK.
15. Atualizar títulos "Mariliz Studio" dos documentos de intenção e remover as cinco notas duplicadas de `product-discovery.md`.
16. Plugins: se o servidor MCP é o "plugin" do SPOCK, dizer isso na UI e dar a ele um lugar fora de "Agentes de IA".

## 10. Não verificado

- Nenhuma tela renderizada nesta auditoria; leituras do código e das 5 capturas de 2026-11-14.
- Se `Cargo.toml` `description` aparece nas propriedades do executável do Windows.
- Instalador gerado com o nome novo (já anotado como não conferido em `evolucao-engineering-management.md:478-479`).
- Versão publicada do `@mariliz/ui-kit`.
- Existência de site ou material comercial fora do repositório.
- Comportamento com leitor de tela, zoom e alto contraste no app empacotado.
