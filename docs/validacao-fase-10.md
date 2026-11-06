# Relatório de validação (Fase 10)

Data: 2026-11-05. Escopo: o que o plano da evolução agentic (`docs/architecture/auditoria-orquestracao-agentic.md`, §8, §9.2 e §10) promete, conferido
contra o que existe e roda. Cada linha diz **qual teste prova** a afirmação e **onde ele roda**. O que não foi provado está na seção final, sem arredondar.

## 1. Resultado

| Suíte | Resultado | Onde |
| --- | --- | --- |
| .NET (motor, CLI, protocolo, integração) | 460 testes: 459 passam e 1 é ignorado de propósito (o smoke do pacote, sem o instalador); mais 2 de integração | local e CI |
| Python (inteligência) | 96 testes passam | local e CI |
| Interface (`pnpm check`: tipos, lint, formatação, testes) | 206 testes passam | local e CI |
| Dependências | `dotnet list package --vulnerable`, `pnpm audit --prod` e `pip-audit` sobre `requirements.lock`: nenhuma vulnerabilidade conhecida | local, 2026-11-05 |
| Instalador (NSIS e `.deb`) | abre, roda o grafo com o Python de dentro e (novo) roda o **motor instalado** de ponta a ponta | CI; NSIS também local |

## 2. Falha encontrada e corrigida

**Chaves de provedores comuns no texto do projeto saíam para o provedor de modelo.** O `SecretScanner` (única fonte da regra de segredo: fontes do
índice, redação do que vai ao modelo e do resultado) só conhecia AWS, GitHub, Slack, JWT, chave privada, URL e connection string. Um documento com
uma chave `sk-proj-…` (OpenAI), `sk-ant-…` (Anthropic), `AIza…` (Google), `sk_live_…` (Stripe) ou `npm_…` entrava no índice e **ia para o modelo**.

- Prova da falha: `Credencial_de_provedor_comum_no_projeto_nao_vira_fonte_nem_sai_para_o_modelo` (5 casos) ficou **vermelho** com o grafo Python de verdade e o proxy do motor.
- Correção: cinco regras novas no `SecretScanner`, com tamanho mínimo para não acusar palavras com hífen; testes de reconhecimento (7 casos) e de palavras parecidas que **não** são acusadas (4 casos); o espelho em Python da varredura dos SQLite recebeu os mesmos formatos.
- Resultado: os 5 casos ficaram verdes, e a suíte inteira continua passando (golden, baselines e recall intactos).
- Limite: um segredo fora desses formatos e sem uma atribuição (`api_key = …`) não é reconhecido. É a natureza de uma lista; ela cresce com os formatos que aparecerem.

## 3. Matriz: o que o plano promete e o que prova

### Segurança e isolamento (§8)

| Promessa | Prova | Roda |
| --- | --- | --- |
| Texto do projeto é dado, nunca instrução: entra só em bloco delimitado, com o delimitador neutralizado | `test_texto_do_projeto_nao_fecha_o_bloco_nem_vira_instrucao`, `test_mensagens_de_prompt_neutralizam_o_delimitador`; **novo, de ponta a ponta:** `O_texto_do_documento_chega_ao_modelo_so_dentro_do_bloco_e_sem_fechar_o_bloco` | Python e .NET |
| Um documento que manda "obedecer" não faz o motor executar nada além do registro aprovado, mesmo com um modelo que obedece | **novo:** `Documento_que_manda_obedecer_nao_faz_o_motor_executar_nada_alem_do_registro_aprovado` (modelo devolve comando, ação e caminho fora do projeto; o disco do projeto e a pasta acima dele ficam intactos antes da aprovação, e depois só o registro aprovado nasce, sem uma palavra do documento) | .NET |
| Saída de agente só vale se passar no validador (sem comando, sem caminho, com evidência) | `test_agents.py` (plano e impacto), e o teste acima (a saída hostil cai em `MODEL_OUTPUT_INVALID`) | Python e .NET |
| O agente não contorna a aprovação | `ActionGovernanceTests` (14) e `ActionGovernanceEndToEndTests` (3): ação sem aprovação, aprovação de outra execução, ação inventada, diário adulterado, retomada repetida, falha depois da aprovação | .NET |
| Um índice e um checkpoint por projeto; nada cruza | `test_dois_projetos_nao_compartilham_trechos`, `test_memory_isolation.py`; **novo, de ponta a ponta:** `Um_projeto_nao_enxerga_a_execucao_nem_o_conhecimento_do_outro` (mesma pasta de dados, mesmo termo: B não vê o arquivo de A, não retoma nem aprova a execução de A, e nada de A está na pasta de B) | Python e .NET |
| O proxy recusa o token de outra execução | `Execucao_nao_usa_o_token_de_outra`, `Sem_o_token_da_execucao_a_chamada_e_recusada...`, `Pelo_HTTP_o_proxy_so_atende_em_127_0_0_1...` | .NET |
| Segredo não vai para índices, checkpoint, diário, eventos nem para o modelo | `test_nenhum_sqlite_guarda_segredo` (com controle negativo); **novo:** `Segredo_plantado_no_projeto_nao_chega_a_nenhum_arquivo_do_motor_nem_ao_modelo_nem_a_tela` (varre **todo arquivo** da pasta de dados e do projeto, inclusive binários, com um controle que prova que a varredura enxerga um segredo plantado) e a bateria de chaves de provedores | .NET |
| Logs do Python desligados; os do .NET passam por redação | `log_message` vazio em `server.py`; `Segredo_em_arquivo_de_configuracao_reprova_o_eval_e_nunca_aparece_em_relatorio_log_ou_diario`, `ProcessOutput` | .NET |
| Chave e token do proxy nunca em estado, evento ou checkpoint | `test_o_token_do_proxy_nunca_chega_a_evento_ack_nem_checkpoint`, `A_chave_e_formatos_de_segredo_nao_saem_nas_mensagens_nem_voltam_na_resposta` | Python e .NET |
| Telemetria de terceiros desligada; nenhuma conexão fora do loopback | `test_execucao_nao_abre_conexao_fora_do_loopback`, variáveis no ambiente do filho (`LoopbackAiRuntime`) e no servidor | Python |
| Dependências fixadas com hash e auditadas | `requirements.lock` com `--require-hashes` no empacotamento; auditorias da seção 1 | local |

### Ciclo de vida e empacotamento (§9.2, Fase 8)

| Promessa | Prova | Roda |
| --- | --- | --- |
| O Python que cai no meio da execução não deixa nada pela metade | `AiRuntimeRestartTests` (processo morto no primeiro evento do fluxo) | .NET |
| Motor que cai não deixa execução "rodando" para sempre | `Execucao_que_a_queda_deixou_em_andamento_vira_falha_e_so_ela` | .NET |
| Python não fica órfão | `test_parent_watch.py` (4) e, **novo, no instalador:** `Motor_instalado_finalizado_a_forca_nao_deixa_o_python_orfao` | Python; CI e local (NSIS) |
| O pacote não passa vazio fora da CI | `[PackagedFact]` (ignorado sem o pacote, falha com `MARILIZ_REQUIRE_PACKAGED=1`) e conferência do `.trx` | CI |
| **O motor instalado conduz uma execução inteira** com o Python instalado, pelo protocolo do host, e encerra sem deixar o Python | **novo:** `O_motor_instalado_conduz_uma_execucao_inteira_e_encerra_sem_deixar_o_python` (handshake, autorização, pausa, aprovação, registro gravado, resultado, `engine.shutdown`) | CI; local (NSIS) |

### Regressão (§8)

| Promessa | Prova |
| --- | --- |
| O grafo não muda de comportamento | `test_baseline.py`, `baseline-model.json`, `test_graph.py`, `WorkflowRuntimeTests`, goldens de contrato |
| Recuperação não piora | `rag.recall.v2` (recall@3 e MRR) no teste de recall |
| Contratos | schemas e fixtures de `contracts/` (válidas e inválidas) conferidos nos testes de contrato |

### Mutações conferidas (o teste precisa falhar quando o código quebra)

- Sem o vigia no Python instalado, o teste do órfão **falha** ("O Python do instalador ficou órfão…"); com ele, passa.
- Sem as cinco regras novas, os 5 casos de chaves de provedores falham (estado anterior à correção).
- Sem a varredura de `running`, e sem o argumento `--parent-pid` (Fase 8), os testes correspondentes falham.

## 4. O que não foi provado

- **Janela do app.** Nenhum teste abre o Tauri: a interface renderizada, o foco, o contraste real e o leitor de tela não foram verificados. O motor instalado e o Python instalado foram.
- **macOS.** Fora da primeira versão, e nenhum teste o cobre.
- **Linux, motor instalado.** O teste do motor instalado está na CI do Linux, mas só foi executado localmente no Windows. Antes dele, a CI já provou o smoke do `.deb`.
- **Modelo real.** Nada foi chamado: todos os testes usam provedor falso. A taxa de recusa e de `PLAN_MODEL_FALLBACK` com um modelo de verdade, a qualidade das notas do juiz e o ganho do Request Analyzer dependem de uma chave sua e seguem sem medida.
- **Injeção contra um modelo real.** O que se prova é que o motor **não executa** o que um modelo convencido devolver. Se um modelo de verdade é convencido ou não por um documento, não foi medido.
- **Segredos fora dos formatos conhecidos** (seção 2).
- **`run.snapshot` não confere o projeto.** Recebe só o `runId` (12 hexadecimais aleatórios). Num aplicativo de um usuário só isso é aceitável; se houver mais de um perfil no mesmo motor, a leitura de uma execução deveria exigir o `projectId`.
- **Diário local.** Quem puder reescrever o hash e o `approvalId` de uma pausa aberta no disco não é detectado (limite já registrado na Fase 7).
- **Atualização automática, assinatura e licenças de terceiros** continuam pendentes (`docs/distribuicao.md`).
