# Auditoria de responsabilidades e refatoração — Spock Studio (2026-11-17)

Escopo: motor .NET 10 (`runtime/`), host Tauri 2/Rust (`desktop/host`, `desktop/launcher`), UI React/TypeScript
(`desktop/ui`) e os contratos entre eles (`contracts/`). A camada Python (`intelligence/`) ficou fora: não mudou de
responsabilidade desde a auditoria anterior e só entrou na validação.

Método: leitura do código por tecnologia, sem considerar o tamanho do arquivo como prova de violação; cada achado
apontado foi conferido no código antes de entrar aqui. Os itens já corrigidos nas auditorias de 2026-11
([arquitetural](auditoria-arquitetural-2026-11.md), [plataforma](auditoria-plataforma-2026-11.md)) não foram repetidos;
os que continuavam abertos aparecem com o identificador antigo.

## 1. Resumo executivo

O código está saudável. As fronteiras entre as camadas continuam respeitadas: não há regra de negócio no Rust, o
React não persiste nada e o .NET não conhece a interface. Não há problema P0. Também não há código morto relevante: uma
varredura dos 766 tipos do motor não encontrou tipo sem referência.

Os problemas que restam vêm do crescimento rápido de IaC, nuvem e Harness nas últimas duas semanas, e repetem três
padrões:

1. **Conhecimento do motor copiado para fora dele.** A UI repetia a regra de aprovação de um plano de IaC e já tinha
   divergido: oferecia aprovar plano vencido. O host repete a lista de métodos e os prazos de cada um. A UI decide
   comportamento comparando texto em português que o motor devolve.
2. **Conhecimento por provedor de nuvem espalhado.** São sete `switch` por string em seis arquivos, com *fallback*
   silencioso para AWS. As regex de identificador estavam duplicadas e já divergiam entre si, e os geradores de Azure e
   Google dependiam do gerador da AWS.
3. **Pontos de composição que crescem por acúmulo.** O `EngineSession` tinha 36 parâmetros opcionais e três listas
   paralelas que precisavam mudar juntas. A UI tem quatro implementações de "acompanhar uma execução". O `engine.rs`
   tem seis campos que formam uma máquina de estados implícita.

**Áreas com maior risco de manutenção:**
- IaC multinuvem, no motor (`Iac/`) e na tela de infraestrutura.
- O ciclo de vida do motor no host (`engine.rs`).
- O acompanhamento de execuções na UI.

## 2. Linha de base (antes da onda 1)

| Tecnologia | Gate | Resultado |
|---|---|---|
| .NET | `dotnet test runtime/Mariliz.sln` | 1598 aprovados, 0 falhas, 28 ignorados (testes ao vivo, que pedem credencial) |
| Rust | fmt, clippy `-D warnings`, `cargo test` (host e lançador) | limpos; 50 testes no host e 4 no lançador |
| UI | typecheck, eslint, prettier | limpos |
| UI | vitest | 842 aprovados e 8 falhas por *timeout* de 5 s. Rodaram com a máquina carregada (motor e cargo em paralelo); veja §7 |
| Python | ruff e unittest | ruff limpo; 1 erro de conexão abortada (`WinError 10053`) num teste de servidor HTTP sob carga, sem relação com o código |

## 3. Inventário

Severidade: P0 crítico (funcionamento, segurança ou integridade); P1 problema arquitetural relevante; P2
manutenibilidade; P3 legibilidade e padronização. A coluna **Status** diz o que a onda 1 fez.

### 3.1 Motor .NET (NET)

| ID | Local | Smell | Princípio | Sev | Impacto | Refatoração | Status |
|---|---|---|---|---|---|---|---|
| NET-6 | `Protocol/EngineSession.cs` | Long Parameter List (36 opcionais); três listas paralelas (campos, capacidades, `switch` com 17 braços) | SRP, OCP | P1 | Cada área nova exige mexer em quatro lugares, e as capacidades podiam divergir do despacho | `IRequestHandler` (grupo de métodos e `HandleAsync`); a sessão roteia por dicionário e anuncia o que os handlers declaram | **Feito** |
| NET-S01 | `Iac/IacAwsGenerator.cs:10,772-828` | Inappropriate Intimacy | DIP | P2 | Azure e Google compilavam contra a classe da AWS | `IacGeneratedFile` e `IacModuleText` (outputs, variáveis de módulo, construtor de variáveis) | **Feito** |
| NET-S05 | `IacBlueprintChecks.cs` × `IacBootstrapTemplates.cs` | Duplicate Code já divergente | DRY | P2 | O blueprint aceitava o container `tf--state`, que o bootstrap e o Azure recusam | `CloudIdentifiers`, com o padrão mais estrito | **Feito** |
| NET-S07 | `Infrastructure/FileModelBudgets.cs`, `FileModelPreferences.cs`, `FileModelCheckStore.cs` | Erro engolido | Fail Fast | P2 | Arquivo travado por um instante virava "vazio" e a gravação seguinte apagava os tetos e escolhas das outras finalidades | A leitura antes de gravar deixa a `IOException` subir e põe o inválido em quarentena; gravação pelo `AtomicFile` | **Feito** |
| UI-17 (motor) | `Iac/IacOperationService.ApplyAsync` | Regra que a UI precisava copiar | SRP | P1 | Ver UI-17 | `IacApplyPolicy`: a mesma regra recusa o pedido e anuncia os requisitos em `iac.operations` | **Feito** |
| NET-S03 | `IacGenerators.cs`, `IacStateService.cs:94,152`, `IacGenerationService.cs:44,146`, `IacOperationService.cs:494`, `IacBlueprintChecks.cs` | Shotgun Surgery; literais `"aws"`/`"azure"`/`"gcp"` em vez de `CloudProviders` | OCP | P1 | Mudar um provedor exige sete edições; um provedor desconhecido cai em AWS ou GCS sem aviso | Tabela única `IacProviders` (um registro por provedor) e erro `ProviderUnknown` | Onda 2 |
| NET-S04 | `IacOperationService.cs:483-528`, `IacStateService.cs:157-167` | Primitive Obsession entre camadas (chave de configuração crua) | DIP | P1 | Renomear um campo no provedor ainda compila. `AWS_PROFILE` ou `GOOGLE_APPLICATION_CREDENTIALS` somem, e a ferramenta cai na cadeia padrão de credenciais, que pode ser outra conta | Os três provedores respondem pela porta `ICloudProcessCredentials` | Onda 2 |
| NET-S08 | `Infrastructure/Iac/CliIacRuntime.cs:11-14` × `engine.rs:39,48` | Configuração espalhada | Design by Contract | P1 | O pior caso do apply no motor (~103 min) passa a espera do host (90 min): a UI mostra *timeout* enquanto a nuvem continua mudando | `IacTimeouts` com total por operação, prazo geral no serviço e teste que compara com o host | Onda 2 |
| NET-S02 | os três geradores | Duplicate Code de regra de produto (`environment_guard`, `name_prefix`, `image_tag`, `>= 1.11`, README) | DRY | P2 | Uma regra muda em quatro a sete lugares | `IacRootCommon` com funções; o texto dos módulos fica como está | Onda 2 |
| NET-S06 | `IacGenerationService:83`, `IacStateService:104`, `IdeaArchitectService:61`, `SecurityService:29` | Duplicate Code do ciclo "diff aprovado → execução reversível" | DRY | P2 | O JSON de resultado (contrato da UI) é montado três vezes | `ReviewedWriteRun.Start(...)` em `Execution`, mais `EngineErrors.StalePlan` | Onda 3 |
| NET-S09 | `IacRuntimeService.cs:55` | Duas fontes de verdade (mínimo 1.6 × `>= 1.11` gerado) | DRY | P3 | Projeto fixado entre 1.6 e 1.10 é recusado depois da geração | Uma constante; decisão de produto (§6) | Decisão |
| NET-S10 | `IacOperationService` (`CommonRoot`, `Combine`, `PlanAsync`) | Feature Envy; validação repetida | SRP | P3 | Serviços chamam estáticos uns dos outros | `IacLayout` estático; `PlanAsync` reaproveita `Target()` | Onda 3 |
| NET-S11 | 28 arquivos | `RequireRoot` e `DisplayName` copiados | DRY | P3 | Ruído | `WorkspaceAuthorizer.RequireRoot` e `DisplayNameOf` | Onda 3 |
| NET-S12 | `Harness/HarnessComposition.cs` | Acoplamento temporal (`null!`, NET-12 antigo), parâmetros de saída | SRP | P3 | A ordem de chamada importa sem que o tipo diga | Seletor criado antes e passado adiante; `SpecificationPlanner` devolve um record | Onda 3 |
| NET-S13 | 6 cópias de leitor de campo JSON | Duplicate Code divergente (`""` × `null`) | DRY | P3 | Campo ausente tratado de dois jeitos | `JsonRead` único | Onda 3 |
| NET-S14 | `WorkflowContextBuilder.cs:77` | `catch (Exception)` sem rastro | Fail Fast | P3 | Um bug na descoberta some do contexto da IA sem aviso | Estreitar o catch ou registrar o tipo | Onda 3 |
| NET-S15 | `Models/ModelGateway.cs` | Divergent Change (administração + invocação) | SRP | P3 | Baixo hoje | Separar só se a configuração de modelos crescer | Monitorar |
| NET-S16 | `Audit/ProjectAudit.cs:137-300` | Switch de 165 linhas sobre strings escritas por 12 serviços | OCP | P3 | Seis ações já aparecem como "Operação registrada" | `AuditActions` e um teste de cobertura | Onda 3 |

### 3.2 Host Rust/Tauri (RS)

| ID | Local | Smell | Princípio | Sev | Impacto | Refatoração | Status |
|---|---|---|---|---|---|---|---|
| RS-4 / RS-11a | `engine.rs:251-319` | Envelope montado em dois lugares; entrada de `pending` vazava em erro de serialização ou future descartado | SRP, encapsulamento | P2 | Vazamento de memória pequeno e lógica duplicada | `protocol::encode_request`/`decode_response` (puros, testados) e `PendingEntry` (RAII) | **Feito** |
| RS-15 | `diagnostic_log.rs:96` × `terminal/env.rs` | Duas listas de marcadores de segredo | DRY, segurança | P2 | `ConnectionStrings__Default=...;Password=...`, `credential=` e `passwd=` chegavam inteiros à tela de Diagnóstico | O log usa a regra do terminal (a mesma do motor), mais `key`/`senha` | **Feito** |
| RS-16 | `launcher/src/main.rs:39` | Conversão com perda | Fail Fast | P2 | Uma queda do motor (NTSTATUS negativo) saía com código 0: o hook da IDE via sucesso | `exit_code`: fora de 0..=255 vira 1 | **Feito** |
| RS-11b | `engine.rs:69-346` | Large Class: spawn, bomba de stdout, correlação, ciclo de vida e prazos | SRP | P1 | Difícil testar o ciclo de vida isoladamente | `engine/{process,rpc,lifecycle}.rs`; o codec já está em `protocol.rs` | **Feito** (onda 2b) |
| RS-12 | `engine.rs` (`write`, `shutdown`) | `Mutex` segurado durante escrita bloqueante no pipe | Concorrência | P2 | Motor que para de ler o stdin trava as requisições, o `on_terminated` e o encerramento | Tarefa escritora dona do `CommandChild`, alimentada por canal | **Feito** (onda 2b) |
| RS-13 | `engine.rs:69-127,227-249` | Máquina de estados implícita (seis campos) | Encapsulamento | P2 | Handshake falho gasta o reinício automático; motor travado na partida deixa a UI 2 min em "iniciando" | `watch<Lifecycle>`, `HANDSHAKE_TIMEOUT`, fase `HandshakeFailed` | **Feito** (onda 2b) |
| RS-10 | `engine.rs:26-59`, `protocol.rs:14-124` | Conhecimento do motor no host (prazos e lista de métodos) | OCP | P2 | Toda função nova do motor muda o Rust; prazo errado cai em 120 s sem aviso | O motor anuncia o prazo dos métodos longos no `engine.hello`; teste .NET que lê `protocol.rs` | Onda 2 (contrato) |
| RS-14 | `lib.rs:191-213`, `terminal/mod.rs` | Comandos síncronos com I/O bloqueante na thread principal | Responsividade | P2 | A UI congela durante o `taskkill` ou num PTY que não lê | `#[tauri::command(async)]`/`spawn_blocking`; escritor por sessão | **Feito** (onda 2b) |
| RS-17 | `resources.rs`, `engine.rs:166`, `launcher` | Layout do runtime conhecido em três lugares | DRY | P2 | No `.deb` do Linux o `spock` provavelmente roda sem o runtime de IA (não verificado em Linux) | O .NET passa a ser o único dono do layout | Onda 3 (empacotamento) |
| RS-18 | `terminal/mod.rs:111-150` | TOCTOU no limite de sessões; shell vaza se a abertura falha | Correção | P3 | Mais de quatro terminais em aberturas concorrentes | Pegar leitor e escritor antes de criar o processo; reservar a vaga atomicamente | Onda 3 |
| RS-19 | `lib.rs` | Comandos de quatro áreas e utilitários num arquivo; busca de recente duplicada | SRP | P3 | Ruído | `commands/{engine,projects,terminal,about}.rs`; `Recents::existing_dir` | Onda 3 |
| RS-20 | `recents.rs:178` (`eprintln!`) e outros | Três canais de log | Consistência | P3 | Falha ao salvar recentes não passa pela redação | Um `Diagnostics` injetável | Onda 3 |

### 3.3 UI React/TypeScript (UI)

| ID | Local | Smell | Princípio | Sev | Impacto | Refatoração | Status |
|---|---|---|---|---|---|---|---|
| UI-17 | `infrastructure/components/iac-operations.tsx:141-154`, `plan-approval.tsx:8,30-40` | Regra de negócio do motor copiada, e já divergente | SRP entre camadas, DRY | P1 | Plano vencido (24 h) aparecia aprovável; a pessoa marcava tudo e só então o motor recusava. Mudar a lista de produção deixaria a UI para trás | A UI renderiza `applyRequirements` vindo do motor; `PRODUCTION` e `approvable` foram removidos | **Feito** |
| UI-32 (parcial) | `plan-approval.tsx:30`, `iac-operations.tsx:113` | `!` em campo opcional | Type safety | P3 | Guarda longe do uso | `summary` e `planId` passam a vir garantidos dos requisitos | **Feito** na aprovação |
| UI-3 | `configure-project/service/harness-api.ts:86`, `execution/hooks/use-run-follow.ts`, `agents/hooks/use-agent-run.ts`, `shared/api/run-until-done.ts` | Quatro implementações de "acompanhar execução" | DRY, SRP | P1 | Configuração assina os eventos depois de `start()` e perde os primeiros. Agentes aceitam lacuna de `seq` e perdem linhas de saída | `shared/api/follow-run.ts` (assina antes, ordena por `seq`, recupera lacuna); os quatro chamadores sobre ele. Começar por teste de caracterização | Onda 2 |
| UI-25 | `runtime-panel.tsx:170`, `blueprint.ts:249` | Comportamento decidido por texto localizado do motor (`'já está instalado'`, `'sim'`) | Contrato explícito | P2 | Reescrever ou traduzir o texto no motor quebra a tela sem aviso | O motor passa booleanos tipados | Onda 2 (contrato) |
| UI-26 | `proposal-guide.tsx:27`, `judge-states.ts:13`, `model-step.tsx:68` | Nomes de arquivo do Harness fixos na UI | DRY entre camadas | P2 | Projeto configurado antes do #132 vê caminhos `spock` que não existem | A descoberta ou o plano devolvem `naming` | Onda 2 (contrato) |
| UI-20 | `infrastructure/domain/blueprint.ts:4,38-80` | Capacidade por provedor na camada de formulário | SRP entre camadas | P2 | Opções e dicas divergem da validação do motor | `iac.generate.options` no motor; a UI mantém só o mapeamento formulário ⇄ DTO | Onda 3 |
| UI-19 | `generate-panel.tsx:46-557` | God Component (dados, três telas, quatro fluxos, cerca de 12 ternários por provedor) | SRP, OCP | P2 | Provedor novo mexe em todos os ternários | `useGenerateFlow` (reducer), tabela `PROVIDER_FIELDS`, subcomponentes | Onda 3, depois de UI-20 |
| UI-18 | `generate-panel.tsx:48`, `iac-operations.tsx:35`, `state-panel.tsx:64` | Mesma busca e junção repetidas; erro vira "nada vinculado" | DRY | P2 | Sete chamadas ao abrir a página; mensagem errada quando a leitura falha | `useLinkedConnections` uma vez na página | Onda 2 |
| UI-21 | `features/integrations/index.ts` | Hooks e formatadores compartilhados morando numa feature (26 imports de fora) | Regra de dependência | P2 | Acoplamento entre features | `shared/hooks/use-action.ts`, `useAsyncResource({ keepPrevious })`, `shared/utils/lib/format.ts` | Onda 2 |
| UI-22 | `knowledge-section.tsx:26`, `harness-section.tsx:18`, `profile-section.tsx:16` | Efeitos de carga escritos à mão (UI-9 voltou) | DRY | P2 | Três máquinas de estado iguais | `useAsyncResource` e `ResourceView` | Onda 2 |
| UI-23 | `runs/hooks/use-run-list.ts` | Quase cópia de `usePagedRead` | DRY | P2 | Duas paginações | Parâmetro `extra` em `usePagedRead` | Onda 2 |
| UI-24 | `execution/domain/run-timeline.ts:1-66` | Contrato do resultado do fluxo numa feature e sem fixture | Contract-first | P2 | Mudança no motor só aparece em produção | Mover para `shared/schema/workflow.ts` e criar fixture gerada pelo motor | Onda 2 |
| UI-33 | `app/routes.tsx` | 19 cópias de `<RequiresHost>`; tudo carregado na partida | DRY | P3 | Chunk de 864 kB (UI-16 antigo) | Rota de layout e `lazy` | Onda 3 |
| UI-27..31 | vários | `<p className={styles.error}>` sem `role="alert"`; `<details>` em vez de `Disclosure`; alternância de `ConfirmAction` repetida; fluxo de salvar arquivos revisados duplicado; recarga forçada em `DecisionsSection` | DRY, a11y | P3 | Ruído e acessibilidade | Ver o relatório da onda 3 | Onda 3 |

### 3.4 Contratos entre componentes

- A lista de métodos que a UI chama é conferida contra a lista do host (`engine-methods.test.ts`). A lista do host
  **não** é conferida contra `ProtocolMethods.cs` (RS-10).
- `iac.operations` ganhou `applyRequirements` (fixture `contracts/fixtures/iac/operations.json` atualizada). É
  campo novo num contrato que só a UI consome, então é compatível com versões anteriores.
- Contratos que hoje dependem de texto livre: o fim de UI-25 e UI-26.

## 4. Priorização

- **P0:** nenhum.
- **P1:**
  - corrigidos na onda 1: NET-6 e UI-17;
  - corrigidos na onda 2a: NET-S03, NET-S04 e NET-S08;
  - corrigido na onda 2b: RS-11b;
  - aberto: UI-3.
- **P2:**
  - corrigidos na onda 1: NET-S01, NET-S05, NET-S07, RS-4/RS-11a, RS-15 e RS-16;
  - corrigidos na onda 2b: RS-12, RS-13 e RS-14;
  - abertos: os demais P2 das tabelas.
- **P3:** onda 3 e itens a monitorar.

## 5. Plano de refatoração

Uma onda por PR, sem empilhar PR. Cada onda começa por testes de caracterização onde a área não tem cobertura.

| Onda | Projeto / módulo | Itens | Alteração | Depende de | Testes necessários |
|---|---|---|---|---|---|
| 1 (este PR) | `Protocol/`, `Iac/`, `Infrastructure/`, `engine.rs`, `protocol.rs`, `diagnostic_log.rs`, launcher, `infrastructure/` (UI) | NET-6, UI-17, NET-S01, NET-S05, NET-S07, RS-4/11a, RS-15, RS-16 | ver §7 | — | ver §7 |
| 2a | `Iac/` (motor) | NET-S03 → NET-S04 → NET-S08 | `IacProviders` como tabela; credenciais pela porta do provedor; `IacTimeouts` com teste contra o host | onda 1 | `IacMultiCloudTests`, `IacStateTests`, `IacOperationTests` + teste novo de prazos |
| 2b | `engine.rs` | RS-11b, RS-12, RS-13, RS-14 | módulo `engine/` com escritor por canal e `watch<Lifecycle>` | onda 1 (codec) | teste com escritor falso; os quatro testes de ciclo de vida |
| 2c | `shared/api`, `configure-project`, `agents`, `execution` | UI-3 | `followRun` único | — | caracterização "evento antes do `start()` aparece no progresso" e lacuna nos agentes |
| 2d | contratos motor ⇄ UI ⇄ host | RS-10, UI-25, UI-26, UI-24 | campos tipados no lugar de texto; prazos no `engine.hello` | decisões em §6 | fixtures novas, `contracts.test.ts`, teste .NET que lê `protocol.rs` |
| 2e | `shared/hooks`, `infrastructure` | UI-21, UI-22, UI-23, UI-18 | hooks para `shared`, uma busca por página | — | testes diretos de `useAction`/`useReloadable` (hoje não há) |
| 3 | vários | P3 restantes, UI-19/UI-20, NET-S02, NET-S06, RS-17..20 | ver tabelas | ondas 2 | os indicados por item |

## 6. Decisões que dependem do dono do produto

1. **Versão mínima da ferramenta de IaC (NET-S09).** O Spock aceita OpenTofu 1.6 e Terraform 1.5, mas tudo o que ele
   gera exige `>= 1.11`. Subir o mínimo para 1.11 ou gerar com o mínimo atual?
2. **Prazos dos métodos longos (RS-10, NET-S08).** Duas saídas:
   - o motor anuncia os prazos no `engine.hello` (muda o contrato do handshake);
   - o apply passa a responder com `runId` e eventos, como o `harness.apply` já faz (muda a tela).
3. **Layout do runtime no Linux (RS-17).** Precisa de uma validação real num `.deb` antes de mexer.

## 7. Onda 1 executada

| Item | Antes | Depois | Testes |
|---|---|---|---|
| NET-6 | 17 campos de handler, 17 `if` de capacidade e 17 braços `when` no `EngineSession` | `IRequestHandler` em cada handler (mais `IdeRequestHandler`); um dicionário de rotas; capacidades na mesma ordem de antes; método em dois grupos falha ao montar a sessão. A assinatura pública do construtor ficou igual, e os 57 pontos de construção nos testes não mudaram | `ProtocolRoutingTests` (grupos disjuntos; anuncia e roteia só o presente) |
| UI-17 | UI recalculava "plano mais recente, sem bloqueio, produção pede digitação", sem prazo | `IacApplyPolicy` (motor) recusa o apply e alimenta `applyRequirements` em `iac.operations`; `PlanApproval` só pergunta o que o motor pede | `IacOperationTests`: a lista anuncia só o aplicável e para de anunciar o vencido e o aplicado. UI: produção pede o nome que o motor indicou; plano não anunciado não oferece aprovação |
| NET-S01 | `IacAwsGenerator.File/Variables/Outputs/ModuleVariables` usados por Azure e Google | `IacGeneratedFile` e `IacModuleText`; os geradores não dependem mais um do outro. Texto gerado idêntico | `IacGenerationTests`, `IacMultiCloudTests` (sem mudança de saída) |
| NET-S05 | 10 regex em dobro; duas divergentes | `CloudIdentifiers` com o padrão mais estrito | `CloudIdentifiersTests` |
| NET-S07 | Arquivo travado lido como vazio antes de gravar | Leitura para gravar falha alto; inválido em `.corrupt-*`; gravação atômica | `FileStoreResilienceTests` (travado e corrompido) |
| RS-4/11a | Envelope em dois lugares; `pending` vazava em dois caminhos | `encode_request`/`decode_response` em `protocol.rs`; `PendingEntry` remove a entrada ao sair de `request` por qualquer caminho | dois testes novos em `protocol.rs` |
| RS-15 | Lista própria de cinco marcadores no log | Regra do terminal (paridade com o .NET) mais `key`/`senha` | `nome_que_o_terminal_esconde_tambem_vira_segredo_no_log` |
| RS-16 | `clamp(0,255)` transformava queda em 0 | `exit_code` | `repassa_o_codigo_do_motor_e_trata_queda_como_falha` |

**Validação depois da onda 1:**
- .NET: 1612 testes aprovados, 0 falhas, 28 ignorados; mais 9 de integração do runtime. O `dotnet build` não tem aviso
  novo.
- Rust: host e lançador com fmt, clippy `-D warnings` e testes limpos (50 no host, 4 no lançador).
- UI: typecheck, eslint, prettier e build limpos. O vitest aprovou 851 de 852; o único que falhou
  (`cloud-page.test.tsx`, *timeout* de 5 s numa área que a onda não tocou) passa isolado, 4 de 4.
- Python: ruff limpo e 197 testes aprovados.
- Não validado: a tela de infraestrutura no app Tauri com um motor real; ficou só nos testes de componente.

## 7a. Onda 2a executada

| Item | Antes | Depois | Testes |
|---|---|---|---|
| NET-S03 | Literais `"aws"`/`"azure"`/`"gcp"` em sete arquivos; provedor desconhecido caía no gerador AWS, nas checagens AWS e no backend `gcs` | `IacProviders`: uma linha por nuvem (id de `CloudProviders`, nome do provider Terraform, backend, gerador, custos, checagens, se o serviço precisa de rede, textos das notas). `Require` recusa com `IAC_PROVIDER_UNKNOWN`; a recomendação de backend fica vazia para o desconhecido em vez de sugerir outra nuvem. Texto gerado idêntico | `IacGenerationTests`, `IacMultiCloudTests`, `IacStateTests` sem mudança de saída |
| NET-S04 | A IaC lia `"profile"`, `"subscriptionId"`, `"projectId"` e `"keyFile"` crus das configurações da conexão | `ICloudProcessCredentials.ProcessNames` devolve as variáveis da ferramenta e o valor padrão do bootstrap; os três conectores implementam com as próprias constantes de campo. Conector sem a porta recusa com `IAC_PROVIDER_WITHOUT_PROCESS_NAMES` em vez de rodar na cadeia padrão de credenciais | `IacOperationTests` (só nomes, papel assumido), `IacStateTests` (bootstrap pela conexão), `IacDriftScheduleTests` |
| NET-S08 | Etapas somadas passavam da espera do host: apply com verificação ~103 min contra 90; validar com várias pastas sem teto | `IacTimeouts` com os prazos das etapas e o total por método (40 min; apply 85, que nunca corta o apply em andamento, só a verificação). O serviço abre um `IacDeadline` ao receber o pedido e o adaptador dá a cada etapa o próprio prazo ou o que sobrou; sem tempo, a etapa nem começa. Não muda o contrato: os prazos no `engine.hello` continuam com a onda 2d (RS-10) | `IacTimeoutsTests`: lê `engine.rs` e confere cada `iac.*` contra o total mais 5 min de folga; todo método de IaC com espera longa no host tem total no motor; o apply recebe os 55 min e a verificação só o que sobrou |

**Validação depois da onda 2a:**
- .NET: 1860 testes aprovados, 0 falhas, 28 ignorados; mais 12 de integração do runtime. Nenhum aviso de build fora
  dos projetos de teste.
- Rust: em `engine.rs` só mudaram comentários; o host não foi recompilado.
- UI e Python: sem mudança de contrato nem de código, então não rodaram de novo.
- Não validado: plano, apply e drift reais com OpenTofu numa conta. A divisão do prazo foi conferida só com a
  ferramenta e o relógio de mentira.

## 7b. Onda 2b executada

| Item | Antes | Depois | Testes |
|---|---|---|---|
| RS-11b | `engine.rs` com 404 linhas: spawn, bomba do stdout, correlação, ciclo de vida e prazos | `engine/mod.rs` só liga as partes; `process.rs` sobe e encerra o sidecar; `rpc.rs` correlaciona pedido e resposta sem conhecer o processo; `lifecycle.rs` decide cada transição em funções puras; `timeouts.rs` tem os prazos por método (o `IacTimeoutsTests` da onda 2a passou a ler este arquivo) | `rpc.rs` com escritor falso: motor que não lê dá `ENGINE_TIMEOUT` sem travar os outros, pipe fechado dá `ENGINE_WRITE_FAILED`, a resposta chega a quem pediu e o fim do motor solta os pendentes |
| RS-12 | `Mutex<Option<CommandChild>>` segurado durante a escrita no stdin | `writer.rs`: uma thread dona do pipe, alimentada por canal; o lock do processo só troca ou copia o escritor. O prazo do pedido cobre escrita e resposta. Encerrar entra na fila e, se a escrita estiver presa, o processo cai pelo pid depois de 2 s | `pipe_travado_nao_prende_quem_escreve`, `pipe_fechado_para_a_thread_e_o_recibo_diz_que_nao_escreveu` |
| RS-13 | Seis campos soltos (`status`, `ready_since`, `terminated`, `shutting_down`...); handshake falho gastava o reinício; hello com 120 s | `watch<Lifecycle>` com `Starting`, `Ready`, `HandshakeFailed`, `Crashed`, `ShuttingDown` e `Stopped`. Na partida só o handshake decide: processo que morreu é queda (uma tentativa), recusa ou prazo vira `HandshakeFailed` sem reinício. `HANDSHAKE_TIMEOUT` de 30 s. Um processo antigo que termina tarde não leva o novo, e o encerramento do app vence um handshake em andamento | Cinco testes de ciclo de vida em `lifecycle.rs` e a espera da autorização pelo `watch` em `engine/mod.rs` |
| RS-14 | `terminal_write`, `terminal_resize` e `terminal_close` síncronos na thread principal; escrita no PTY com o lock da sessão | Os três viraram `#[tauri::command(async)]`; cada sessão do terminal tem o próprio `Writer`, e a resposta ao cursor do ConPTY no encerramento não espera mais um shell que não lê | Testes do terminal com shell de verdade, sem mudança |

**Validação depois da onda 2b:**
- Rust (host): fmt, clippy `-D warnings` e 60 testes limpos (eram 50).
- .NET: `IacTimeoutsTests` (5) com o caminho novo; o resto do .NET só teve comentários alterados.
- App de verdade (`tauri dev` com o motor publicado por `build-runtime.ps1`, pela janela via CDP):
  - partida e um pedido real (`runtime.health`) respondendo em ~20 ms;
  - motor encerrado à força: o host registrou a queda e subiu outro em 0,25 s;
  - segunda queda em seguida: ficou `crashed`, sem outra tentativa;
  - fechar a janela com o motor vivo: o host saiu em 0,3 s sem deixar processo.
- Não validado: handshake recusado ou travado com um motor real, nem um motor que para de ler o stdin; ficaram só nos
  testes com escritor falso e nas funções puras. Linux e macOS não rodaram.

## 8. Arquivos grandes julgados coesos

- **Geradores de IaC** (AWS 829, Google 751 e Azure 723 linhas). O texto de cada módulo é um catálogo de modelos por
  nuvem, e os serviços diferem de fato. A duplicação real era só a de NET-S01, S02 e S03.
- **`IacBootstrapTemplates.cs`**: um modelo por provedor.
- **`IacRuntimeService.cs`**: o ciclo de vida da ferramenta de IaC, sem persistência nem HTTP próprios.
- **`LoopbackAiRuntime.cs`**: o único adaptador do runtime Python, atrás de quatro portas.
- **`ContextEngine.cs`**, **`LanguageDetectors.cs`** e **`CliRunner.cs`**: um algoritmo, um catálogo e um adaptador fino.
- **`protocol.rs`**, **`status.rs`**, **`terminal/{env,shell,utf8}.rs`**, **`recents.rs`** e **`diagnostic_log.rs`**.
- **`shared/schema/iac.ts`** e **`management.ts`**: só declarações.
- **`run-detail-panel.tsx`**, **`runtime-panel.tsx`** e **`agents-page.tsx`**: apresentação.
- **`execution/domain/run-workspace.ts`** e **`run-timeline.ts`**: folds puros e testados. O schema que está neles é
  o item UI-24.
