# Universal Boot

> Primeira entrega da fundação, de 2026-11-26. Diz o que o Boot é, o que existe no produto que ele coordena, como foi
> construído e o que está provado. O que é futuro está no [ROADMAP](../ROADMAP.md), seção "Universal Boot", sem backlog
> paralelo. A decisão de arquitetura está na [ADR-0010](../architecture/adr/0010-universal-boot.md).

## 1. O que é, e o que não é

O Boot recebe um pedido ou uma escolha de funcionalidade, descobre qual capacidade do Spock atende, confere se ela pode
rodar agora, monta um plano e faz o que é leitura ou preparação. O que altera o projeto ou a nuvem vira "abrir a tela que
já existe", e é ela que pede a aprovação. O estado fica gravado a cada passo, então uma queda deixa um registro que dá
para recuperar.

Não é um agente que faz tudo, nem um chatbot, nem um segundo sistema de workflows ou de gestão. Ele não escreve
conclusão: o texto do resultado vem do que cada passo leu do motor. Não substitui o Harness, que continua sendo a
governança da engenharia do projeto.

## 2. Auditoria: o que o produto tem

Fonte: leitura do motor (`ProtocolMethods.cs`, handlers, `Program.cs`), da interface (`routes.tsx`, `paths.ts`, features,
Início) e do [diagnóstico de 2026-11-17](../plataforma/MARILIZ_CURRENT_STATE.md). Nenhuma tela foi aberta no aplicativo
para este documento.

### 2.1 Interface

- Mais de 25 caminhos de tela, em Início, Engenharia, Gestão, Atividade, Conexões e Configurações. Todas as telas chamam o motor;
  nenhuma é só visual. Fora do aplicativo (navegador), só Início, Configurar, Pedido, Diagnóstico e Sobre abrem.
- **O Início não chamava o motor para enviar o pedido.** A entrada escolhia um tipo por palavras-chave no cliente
  (`suggestAskKind`) e navegava com o texto no estado da rota; quem chamava o motor era a tela de destino, depois de a
  pessoa confirmar de novo. As seis sugestões também eram só navegação.
- Não havia roteador de intenção no motor, nem catálogo geral de capacidades, nem nível de autonomia.

### 2.2 Motor

- Protocolo NDJSON v1, com a lista de métodos conferida em três lugares (motor, host Rust e interface).
- Sem identidade (usuário, papel, organização). O escopo real é o projeto autorizado pelo diálogo nativo da pasta.
- O que existe de controle e o Boot reutiliza sem enfraquecer: pasta autorizada, aprovação presa ao hash do plano,
  confirmações a mais para destruição de infraestrutura, política dos agentes, leitura de nuvem liberada por vínculo.

### 2.3 Capacidades por domínio

Cada linha é uma ficha em `CapabilityCatalog.cs`. "Executa" quer dizer que o Boot chama o método do motor na hora; "abre a
tela" quer dizer que ele só prepara.

**Engenharia**

| Capacidade | Id | Operação | O Boot | Tela |
| --- | --- | --- | --- | --- |
| Visão geral do projeto | `engineering.project_overview` | leitura | executa | `/projeto` |
| Analisar o projeto de novo | `engineering.analyze_project` | preparação | executa | `/projeto` |
| Estado do Harness | `engineering.harness_status` | leitura | executa | `/projeto/harness` |
| Revisões do Harness | `engineering.harness_revisions` | leitura | executa | `/projeto/harness` |
| Catálogo do Harness | `engineering.catalog` | leitura | executa | `/projeto/catalogo` |
| Conhecimento do projeto | `engineering.knowledge` | leitura | executa | `/projeto/conhecimento` |
| Servidores MCP do projeto | `engineering.mcp_inventory` | leitura | executa | `/projeto/harness` |
| Configurar o ambiente de engenharia | `engineering.configure` | alteração interna | abre a tela | `/configurar` |
| Pedido de engenharia | `engineering.request` | alteração interna | abre a tela | `/pedido` |
| Pergunta sobre o código | `engineering.code_question` | análise | abre a tela | `/agentes` |
| Varredura de segurança | `engineering.security_scan` | análise | abre a tela | `/seguranca` |
| Modernização | `engineering.modernization` | análise | abre a tela | `/projeto` |

**Padrões e governança**

| Capacidade | Id | Operação | O Boot | Tela |
| --- | --- | --- | --- | --- |
| Análise de arquitetura e padrões | `standards.architecture` | análise | executa | `/projeto/arquitetura` |
| Política dos agentes | `standards.agent_policy` | leitura | executa | `/projeto/harness` |

**Gestão**

| Capacidade | Id | Operação | O Boot | Tela |
| --- | --- | --- | --- | --- |
| Portfólio | `management.portfolio` | leitura | executa | `/portfolio` |
| Acompanhamento do projeto | `management.tracking` | leitura | executa | `/gestao` |
| Riscos do projeto | `management.risks` | análise | executa | `/gestao` |
| Histórico de riscos | `management.risk_history` | leitura | executa | `/gestao` |
| Previsão de entrega | `management.forecast` | análise | executa | `/gestao` |
| Marcos | `management.milestones` | leitura | executa | `/gestao` |
| Relatório do projeto | `management.report` | leitura | executa | `/gestao/relatorio` |
| Decisões registradas | `management.decisions` | leitura | executa | `/gestao` |
| Assistente de gestão | `management.assistant` | análise | abre a tela | `/gestao/assistente` |

**Infraestrutura**

| Capacidade | Id | Operação | O Boot | Tela |
| --- | --- | --- | --- | --- |
| Ferramenta de infraestrutura | `infrastructure.status` | leitura | executa | `/infraestrutura` |
| Pastas de infraestrutura | `infrastructure.layout` | leitura | executa | `/infraestrutura` |
| Estado da infraestrutura | `infrastructure.state` | sensível | abre a tela | `/infraestrutura` |
| Gerar a infraestrutura | `infrastructure.generate` | alteração interna | abre a tela | `/infraestrutura` |
| Validar e planejar a infraestrutura | `infrastructure.validate_plan` | análise | abre a tela | `/infraestrutura` |
| Desvio da infraestrutura (drift) | `infrastructure.drift` | configuração | abre a tela | `/infraestrutura` |
| Provisionar infraestrutura | `infrastructure.apply` | destrutiva | abre a tela | `/infraestrutura` |
| Ler recursos da nuvem | `infrastructure.cloud_read` | leitura | abre a tela | `/cloud` |

**Ideias, IA, conexões e atividade**

| Capacidade | Id | Operação | O Boot | Tela |
| --- | --- | --- | --- | --- |
| Da ideia ao projeto | `ideas.idea_to_project` | alteração interna | abre a tela | `/ideia` |
| Da ideia ao desenvolvimento | `composite.idea_to_development` | alteração interna | composta | etapas |
| Modelos de IA | `ai.model_status` | leitura | executa | `/configuracoes/ia` |
| Agentes de IA instalados | `ai.agents_detect` | leitura | executa | `/agentes` |
| Conexões com ferramentas | `connections.list` | leitura | executa | `/integracoes` |
| Contas de nuvem | `connections.cloud` | leitura | executa | `/cloud` |
| Linha do tempo | `platform.timeline` | leitura | executa | `/linha-do-tempo` |
| Trilha de auditoria | `platform.audit` | leitura | executa | `/auditoria` |
| Execuções | `platform.runs` | leitura | executa | `/execucoes` |

### 2.4 O que ficou fora do catálogo, de propósito

- **Configurar modelos, conexões e nuvem** (`model.save`, `connection.create`, `cloud.create`...): guardam credencial. O
  Boot leva à tela (as capacidades de leitura apontam para ela), nunca recebe uma chave.
- **Evals e juízes** não têm método próprio: rodam por `harness.validate` e pelo workflow.
- **RAG e MCP** não têm capacidade própria: o índice entra em `engineering.knowledge` e o inventário em
  `engineering.mcp_inventory`.
- **Custo** não tem ficha própria: `iac.cost.*` entra em `infrastructure.validate_plan`. O drift tem a sua
  (`infrastructure.drift`, com o agendamento, que é configuração e por isso só abre a tela). O plano de bootstrap do estado
  remoto (`iac.bootstrap.plan`) fica fora do catálogo.
- **Triggers, Roadmap e backlog, Dependency Intelligence, Work Readiness**: não existem no produto. Estão no roadmap.
- **Agentes, Skills e Workflows** aparecem como metadados da ficha (`agents`, `skills`, `workflows`), não como capacidade
  separada: o catálogo do Harness já os lista.

## 3. Arquitetura

```text
Início (pedido, sugestão)         ─►  boot.start / boot.project            (protocolo, 6 métodos novos)
                                              │
                                    BootService  (Application/Boot)
   fatos ◄── BootFactsProbe          interpreta ── IntentRecognizer ◄── CapabilityRegistry ◄── CapabilityCatalog
   (projeto, modelo, integrações,    confere   ── PermissionEvaluator
    nuvem, Harness, perfil)          planeja   ── BootPlanner
                                     executa   ── Dispatch ──► o mesmo tratador que a interface chama
                                     resume    ── BootResults / CapabilitySummaries
                                     grava     ── IBootStore (FileBootStore), a cada checkpoint
                                     audita    ── boot.started / boot.finished (sem o texto do pedido)
```

Camadas: tudo do Boot vive em `Mariliz.Application/Boot`; só o `FileBootStore` está na `Infrastructure`. O Python e a
interface não conhecem o Boot além do protocolo.

### 3.1 Papéis

| Componente | Papel | O que o Boot faz com ele |
| --- | --- | --- |
| Universal Boot | Interpretar, preparar e orquestrar | É este documento |
| Harness | Governança da engenharia do projeto | Lê o estado (`engineering.harness`); só a tela `/configurar` o altera |
| Session | Contexto de uma interação | No produto são as execuções (`RunKinds`) e as sessões de IDE; o Boot não cria uma terceira, só correlaciona |
| Agent, Skill, Workflow | Executam responsabilidades | Aparecem como metadados da ficha; o Boot abre `/pedido`, onde o workflow pausa para aprovação |
| Capability | O que o Spock sabe fazer | `Capability` no catálogo |
| Judge | Avalia com critério independente | Não é usado pelo Boot; o verificador do motor continua decidindo |
| Trigger, Connector | Eventos e sistemas externos | Connector: só leitura do estado (`connection.list`). Trigger: não existe ainda |

### 3.2 Reconhecimento de intenção

Léxico e determinístico, de propósito. Cada ficha traz termos com peso (palavra, frase em ordem com até duas palavras no
meio, ou prefixo com `*`). A pontuação soma os termos, a dica do tipo de atividade que a tela já sabe (que só reforça o
que o texto sugeriu, nunca escolhe sozinha) e o contexto (há projeto aberto?). Duas regras evitam palpite:

- candidatos próximos, com diferença menor que 2, viram **pergunta com opções** (`awaitingClarification`);
- abaixo do mínimo, o Boot diz que **não há capacidade** (`unsupported`) e oferece as mais próximas.

Uma capacidade composta (`composite.idea_to_development`) nasce de fichas existentes: vale quando a primeira etapa e mais
uma outra aparecem no pedido. Quem marca o tipo de atividade de propósito, ou escolhe uma opção, manda a capacidade
direto e pula a interpretação.

**Medida (eval `BootIntentEvalTests`)**: 73 de 73 no corpus usado para ajustar os termos; **10 de 14 (71%) em frases
escritas depois e nunca usadas no ajuste**. Os quatro erros são de vocabulário que o léxico não cobre ("permissão para
mexer" para a política dos agentes, "rodar o plano do terraform", "senhas esquecidas" que caiu em pergunta sobre código, e
uma pergunta que devia ter sido de esclarecimento). As quatro fichas da etapa 2 entraram sem mudar esse grupo: os mesmos quatro erros, 10 de 14. O número de 71% é o que vale esperar de frase nova; o 100% mostra só
que os termos foram ajustados ao corpus. Uma troca por reconhecimento com modelo deve ser julgada pela mesma eval.

### 3.3 Permissão e autonomia

Uma só avaliação (`PermissionEvaluator`) para todas as capacidades, em duas perguntas: a capacidade pode rodar agora?
(`CapabilityRegistry`: método roteado, projeto autorizado, modelo configurado, integração vinculada) e o Boot pode
executá-la? (classe da operação contra o nível).

| Classe da operação | Nível mínimo | O Boot |
| --- | --- | --- |
| leitura, consulta, análise | 0 observação | executa se a ficha é direta |
| preparação (grava só na pasta de dados do Spock) | 1 preparação | executa se o nível pedido é 1 |
| configuração, alteração interna, externa, sensível | 2 assistido | só abre a tela; a tela pede a aprovação |
| destrutiva | 3 governado | só abre a tela, com as confirmações extras dela |

O teto da fundação é o **nível 1**. Pedir mais é recusado (`BOOT_AUTONOMY_NOT_ENABLED`), e o padrão é o próprio teto. Os
níveis 3 e 4 só existem como vocabulário. Três travas estruturais, com teste: uma ficha direta com operação acima de
preparação faz o motor falhar na montagem; o método executado vem da ficha, nunca do pedido nem do registro gravado; os
métodos do próprio Boot não são executáveis por ele.

**Disponível não é autorizado.** O catálogo diz o que existe e o que falta (`available`, `partial`, `unavailable`,
`notConfigured`, `blockedByPermission`, `dependsOnIntegration`; `maintenance` está no vocabulário e nada o usa ainda). Uma
capacidade cujo método o motor desta sessão não roteia aparece como indisponível, mesmo que a tela exista.

### 3.4 Ciclo de vida, persistência e recuperação

Estados: `created`, `interpreting`, `awaitingClarification`, `preparing`, `planned`, `executing`, `completed`, `failed`,
`cancelled`, `interrupted`. Cada transição e cada passo concluído grava o registro inteiro, de uma vez e por arquivo, em
`projects/<id>/boots/<bootId>.json` (ou `boots/` para o Boot sem projeto), com `version` crescente e a lista de
checkpoints. Arquivo ilegível vai para `.corrupt-*` e some da lista sem derrubar os outros; o id vira nome de arquivo, então
só o formato `boot-<12 hex>` passa.

**Auditoria.** Só o Boot de um projeto grava `boot.started` e `boot.finished`: a trilha é de projeto, e o Boot sem projeto
(portfólio, conexões, modelos) fica no próprio registro, que já guarda o que ele fez.

**Retenção.** Cada escopo (um projeto, ou o Boot sem projeto) guarda os 200 registros mais novos. Ao subir o motor a poda
vai até o limite exato; ao terminar um Boot ela só roda quando a pasta passa de 220, para não reler tudo a cada pedido.
Saem só os mais antigos que já terminaram: um Boot vivo, ou interrompido e ainda não recuperado, nunca é apagado, porque é o
que a recuperação precisa. Se um arquivo está travado, a poda fica para a próxima vez e o Boot não falha por isso.

**Recuperação.** Ao subir, o motor marca como `interrupted` todo Boot que a queda deixou em andamento. `boot.resume`
cria um Boot de modo `recovery` ligado ao original: o que já estava feito é mantido e **não é repetido**; o contexto é lido
de novo e comparado com o do começo, e o resultado diz se "o contexto do projeto mudou desde a interrupção". O original
guarda `recoveredByBootId`, e recuperar duas vezes é recusado. Um Boot interrompido antes de ter plano também é recusado
(`BOOT_NOT_RESUMABLE`): não há o que retomar, e a pessoa faz o pedido de novo.

**Quatro estados, sem duplicar dados.** O estado do Boot (este registro) correlaciona com os outros por id e não os copia:
a execução/Session (`runId` nos diários), o Harness (lock e declaração no projeto, só lidos), as integrações (conexões e
vínculos, só lidos) e os workflows (checkpoint próprio no Python). O Boot grava o que ele fez e leu; a fonte de verdade de
cada domínio continua nele.

### 3.5 Modos de inicialização

| Modo | Situação |
| --- | --- |
| Request Boot | Implementado e ligado à entrada de pedidos do Início |
| Project Boot | Implementado e ligado à sugestão "Analisar o projeto" |
| Recovery Boot | Implementado (`boot.resume`) e ligado ao botão Retomar da lista de pedidos recentes do Início (§3.7) |
| Work Boot, Autonomous Boot | No vocabulário, recusados; dependem de backlog, dependências e prontidão, que não existem |

### 3.6 Project Boot

Para um projeto autorizado: confere identidade e o escopo; reúne o contexto pelo `ContextEngine` (que já existia); se o
projeto **nunca foi analisado** e o nível permite, analisa uma vez (`engineering.analyze`, que grava só na pasta de dados);
lê tecnologias, estado do Harness, conexões, marcos e execuções; registra como limitação o que não vale para o projeto (sem
integração, sem modelo, sem nuvem); e entrega o relatório: o que foi feito, consultado, alterado, conferido, os problemas, o
que está pendente e o próximo passo. **Projeto existente não é reanalisado nem reinicializado e nada é apagado.** O Boot
não cria nem altera arquivo do projeto.

### 3.6.1 Parâmetros do pedido

Quatro capacidades de leitura aceitam um parâmetro que o Boot tira do texto: o **período em semanas** (indicadores e
relatório: "últimas 12 semanas", "30 dias" vira 5, "2 meses" vira 8, "trimestre" 13, "semestre" 26) e a **quantidade** de
eventos, registros ou execuções (linha do tempo, auditoria e execuções: "últimos 30 eventos"). O número só vale com a
unidade da própria capacidade ("12 semanas" não vira quantidade de eventos). Cada parâmetro tem a faixa que o motor aceita
(semanas de 2 a 26; eventos e registros de 1 a 200; execuções de 1 a 100); fora dela o Boot ajusta e **diz que ajustou** no
passo ("pedido 40, o motor aceita de 2 a 26; usei 26"). O argumento fica no passo do registro, mas ao executar só passa o que a
ficha declara e sempre dentro da faixa, então um registro adulterado não leva um argumento livre ao motor. Só capacidades
que o Boot executa levam parâmetro; as que abrem a tela deixam a pergunta para ela. Filtros por tipo e estado ainda não.

### 3.7 Início

A entrada de pedidos passa a chamar `boot.start` e mostra o resultado em um cartão (objetivo, o que foi feito e
consultado, alterações, o que foi conferido, problemas, pendências, próximo passo, as etapas para quem quiser ver). Abrir a
tela sugerida continua levando o texto, como antes. Se o motor não responde ao Boot, o pedido segue direto para a tela, como
sempre seguiu. "Analisar o projeto" roda o Project Boot. Não há segunda Home nem segundo chat.

**Pedidos recentes.** Com o motor pronto e um projeto selecionado, logo abaixo do cartão aparece a lista dos últimos Boots
do projeto (`boot.list`): o resumo, o resultado ou o estado em português, o horário e, quando for o caso, "recuperado" ou
"recupera outro". Sem nenhum Boot o bloco não aparece; se a lista não carrega, o bloco avisa e oferece tentar de novo.
Clicar numa linha com resumo abre o detalhe (`boot.detail`) no mesmo cartão. O botão **Retomar** só existe no Boot
interrompido que ainda não foi recuperado; ele chama `boot.resume` e o resultado cai no mesmo cartão de um pedido novo,
com a mesma regra de um Boot por vez (retomar e abrir ficam desabilitados enquanto há um em preparo). Se a retomada falha
a tela avisa e a lista é relida. Os mais novos vêm primeiro, até cinco; um Boot interrompido ainda não recuperado fica à
vista mesmo que seja mais antigo, porque é o que dá para retomar. Quem garante isso é o motor: `boot.list` devolve os mais
novos até o limite e mais todo Boot retomável, por antigo que seja (a interface só filtra o que recebeu).

## 4. Evidências

Implementado, validado e planejado são coisas diferentes; esta tabela não as mistura.

| Critério de aceite | Implementado | Prova |
| --- | --- | --- |
| Identidade e ciclo de vida | `BootRecord`, estados, checkpoints, `version` | `BootServiceTests` (registro no disco, checkpoints, queda) |
| Catálogo baseado no produto real | 40 fichas, cada método existe no protocolo e cada tela em `paths.ts` | `BootCatalogTests` (os dois conferem contra o código) |
| Reconhecer intenções e domínios diferentes | `IntentRecognizer` | `BootIntentEvalTests`: 79/79 ajustado, 10/14 em frases novas |
| Recuperar contexto | `ContextEngine` no passo de contexto | `BootServiceTests`, `BootProtocolTests` |
| Verificar permissão antes | `PermissionEvaluator` | Destrutivo nunca executa; autonomia acima do teto recusada; sem projeto, modelo ou integração, bloqueado com o motivo |
| Montar plano | `BootPlanner` | Plano com passos, dependências, aprovações e riscos no registro |
| Iniciar operações existentes | `Dispatch` pelo tratador da interface | `BootProtocolTests`: dez fluxos reais pelo protocolo; o Project Boot analisa um projeto .NET de verdade |
| Histórico e resultado | `FileBootStore`, auditoria, retenção de 200 por escopo | Registro no disco; a auditoria não guarda o texto do pedido; `BootRetentionTests` |
| Recuperar execução interrompida | `boot.resume` | `BootServiceTests`: não repete o que foi feito; detecta contexto que mudou |
| Project Boot em projeto real | Sim, com o processo real do motor (não com o app Tauri) | Projeto .NET de teste; projeto existente não é reanalisado; `BootProcessTests` roda o Project Boot pelo stdin/stdout do motor e confere por impressão digital que nenhum arquivo do projeto mudou |
| Funcionalidades originais seguem | Nenhum método existente mudou | Motor: 2.104 aprovados, 0 falhas, 28 ignorados (os de chave real). Processo real do motor: 21. Interface: 1.148 aprovados. Com o Python 3.12 do PATH, 38 testes do runtime de IA falham por ambiente (o motor exige o 3.14, via `MARILIZ_PYTHON`); não são do Boot |
| Sem duplicação | Reusa `ContextEngine`, tratadores, `IAuditLog`, `WorkspaceAuthorizer` | Revisão do código |
| Testes automatizados | 73 no motor em memória, 9 com o processo real do motor, mais os da interface | Ver abaixo |
| Roadmap registrado | [ROADMAP](../ROADMAP.md) | — |

Testes novos: `BootCatalogTests` (9), `BootIntentEvalTests` (8), `BootServiceTests` (26), `BootParametersTests` (6), `BootRetentionTests` (5), `BootProtocolTests` (9, incluindo
as fixtures de contrato de `contracts/fixtures/boot/`), `BootSummariesTests` (9, cada resumo contra a fixture real do contrato), e na interface o contrato com o Zod, o cartão pelo Início (9) e o próprio cartão (2) e a
conferência das três listas de métodos.

`BootProcessTests` (9, em `runtime/tests/Mariliz.Runtime.IntegrationTests`) sobe o motor de verdade (`dotnet Mariliz.Runtime.dll`, NDJSON no
stdin/stdout, `MARILIZ_DATA_DIR` temporária por teste) e prova o que os testes em memória não alcançam: o `engine.hello` anuncia os seis métodos e os
300 s de `boot.start`, `boot.project` e `boot.resume`; a jornada completa (leitura, Project Boot, lista e detalhe iguais ao arquivo gravado, pedido
ambíguo, destrutivo que só abre a tela sem chamada de infraestrutura, sem capacidade, autonomia 2 recusada, projeto não autorizado); o Project Boot
não muda nenhum arquivo do projeto e grava o perfil só na pasta de dados; um registro `executing` deixado no disco vira `interrupted` ao subir (com o
aviso no stderr), `boot.resume` refaz só os passos pendentes (a análise já feita não roda de novo), o original passa a ter `recoveredByBootId` e a
segunda recuperação, ou a de um Boot sem plano, é recusada; a retenção na subida deixa 200 por escopo e preserva o interrompido não recuperado; as
chaves das respostas reais têm o mesmo conjunto das fixtures de `contracts/fixtures/boot/`; o stdout só tem mensagens do protocolo, o motor sai com
código 0 no `engine.shutdown` e não deixa processo filho. **Não prova**: a interface (Zod, cartão do Início), o host Rust (`protocol.rs`), o app Tauri
aberto nem qualquer fluxo com modelo real. O registro do Boot interrompido é gravado pelo teste (o formato é o do motor, os campos são editados);
não há queda real de processo no meio de um passo.

**Nada disso rodou com modelo real**, e o Boot da fundação não usa modelo: o que depende de modelo (`engineering.request`,
`management.assistant`, `ideas.idea_to_project`) só abre a tela. O Project Boot foi exercitado no teste com um projeto .NET
temporário e, depois, no aplicativo Tauri aberto (próximo parágrafo), nunca em um projeto real do dono.

**No aplicativo real** (Tauri dev, WebView2 e o motor recém-construído, dirigidos pelo CDP, com um projeto .NET temporário,
pasta de dados separada e sem chave, rede, Jira, GitHub ou nuvem): o host compilou com a lista nova de métodos e o motor
respondeu `boot.*`; pedido de leitura, pedido que só abre a tela, pedido ambíguo com escolha de uma opção, Project Boot
(hash dos arquivos do projeto idêntico antes e depois), recuperação com Retomar (dois Boots interrompidos semeados, um com
plano e outro sem), envio duplo (um só Boot, por Enter duas vezes em 29 ms e por dois cliques) e console sem erro. Achou um
bloco repetido no Project Boot, a lista que não distinguia os interrompidos e oferecia Retomar ao que não tem plano, o botão
fora do alinhamento e jargão nas mensagens; todos corrigidos. **Não provou** modelo real, os fluxos Python do motor
(o empacotamento da inteligência falhou no build por causa do Python 3.12 do PATH; o script aceita `MARILIZ_BUILD_PYTHON` apontando para o 3.14, o que não foi refeito) nem projeto real.

## 5. Riscos e limitações

1. **Reconhecimento léxico.** 71% em frase nova. Quem escreve com outro vocabulário cai em pergunta ou em "sem
   capacidade", que é o erro seguro, mas é erro. Frases com negação ou duas intenções dependem do desempate.
2. **Sem identidade.** O Boot registra a origem (`ui`), não a pessoa nem a organização. Isolamento entre organizações não
   existe porque o produto não tem organização; o escopo é o projeto autorizado na máquina.
3. **Texto do pedido gravado em disco** no registro do Boot (campo `request`), na pasta de dados do Spock, como os
   diários já fazem. Não vai para auditoria nem para o contexto de IDE. A linha com cara de segredo (o mesmo detector das
   leituras do MCP) é omitida antes de qualquer uso do texto, e o resultado avisa; o resto do texto continua inteiro, e o
   detector não pega todo formato de segredo.
4. **Leituras que podem demorar** (`aitool.list` roda processos; `engineering.analyze` lê o projeto): o Boot está na lista
   de métodos lentos e anuncia ao host um prazo de 300 s; não tem cancelamento pela interface.
5. **O resumo é só o que o motor sabe resumir.** Toda capacidade direta tem resumo próprio, feito de contagens, estados e nomes
   de ferramenta; títulos, nomes de marcos e mensagens ficam na tela. Valor ausente aparece como "sem dado". Se o formato de
   uma resposta mudar, o teste contra a fixture do contrato quebra; o resumo genérico (contagem de listas) só cobre capacidade nova
   sem resumo.
6. **Uma sessão de IDE (MCP) não usa o Boot.** O servidor MCP não expõe `boot.*`.
7. **Nada é executado em paralelo**, e o Boot não tem fila nem concorrência entre projetos. A tela barra um segundo pedido
   enquanto um está em preparo.

## 6. Decisões para o dono

- **BD-1.** O Boot deve passar a usar um modelo para reconhecer a intenção quando o léxico não resolver (com orçamento de
  tokens e a mesma eval como régua)? Recomendado: sim, só como segundo estágio, depois da rodada real com chave.
- **BD-2.** *(decidida em 2026-11-27)* O registro do Boot guarda o texto do pedido, com a linha de segredo redigida.
- **BD-3.** Expor o Boot ao MCP (`spock_boot`) para a IDE iniciar pelo mesmo caminho?
- **BD-4.** Quando existir identidade, o registro passa a guardar a pessoa; a auditoria precisa então de política de
  retenção.
