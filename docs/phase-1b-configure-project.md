# Mariliz AI Engineering — Fase 1B: jornada "Configurar projeto"

Documento de referência da implementação. Complementa o [roadmap](implementation-roadmap.md), a [arquitetura](architecture-blueprint.md) e o [blueprint de experiência](experience-blueprint.md); onde divergir deles, este documento descreve o que foi de fato construído.

## 1. O que a jornada faz

```
pasta do projeto ─▶ descoberta (somente leitura) ─▶ método (TLC) e ambiente de IA ─▶ plano do Harness
      ─▶ revisão (diff, conflitos, comandos, aprovações) ─▶ aplicação (backup, diário, reversível)
      ─▶ validação (Evals) ─▶ relatório
```

| Etapa | O que acontece | Quem decide |
| --- | --- | --- |
| A · Projeto | Detecta stack (.NET, Node/Next.js/React/Angular, Flutter), comandos de build/teste/lint, CI, ambiente de IA, harness existente, estado do git e do TLC. Cada achado traz evidência e confiança. | Motor |
| B · Método e ambiente | O TLC é o **único** método SDD. A pessoa confirma o ambiente de IA (sugerido só quando há um suportado e único). | Pessoa |
| C · Plano | Uma decisão por componente (`enable`, `keep`, `skip`, `blocked`), com origem e justificativa. | Motor (+ proposta do Harness Engineer, se existir) |
| D · Revisão | Criar/atualizar vêm marcados; remover, conflitos e **comandos** exigem um gesto. Conflito assume "manter o meu arquivo". Diff sob demanda, sem segredos. | Pessoa |
| E · Aplicação | Pré-checagem, backup, comando aprovado, escritas atômicas, lock, validação. Falha ou cancelamento desfazem. | Motor |
| F · Resultado | Arquivos criados/atualizados/mantidos, Evals (`passed`/`failed`/`blocked`/`not_run`), limitações, riscos, "Reverter" e "Validar de novo". | Motor |

O resultado nunca é um "sucesso" genérico: `completed` só existe se todo Eval obrigatório passou com evidência. Qualquer item bloqueado ou não executado vira `completed_with_limitations`; Eval obrigatório reprovado vira `failed`.

## 2. Arquitetura

Uma única classe, `ConfigureProjectService`, implementa os casos de uso; **UI e CLI chamam a mesma classe**.

```
apps/desktop-ui (React) ─ invoke ─▶ apps/desktop-host (Tauri/Rust, allowlist) ─ NDJSON ─▶ Mariliz.Engine
                                                                                  └─ Mariliz.Cli ─────────┘
                                         ▼
                      Mariliz.Application (ConfigureProjectService, ApplyService, EvalRunner…)
                      Mariliz.Core (modelos e políticas puras) · Mariliz.Infrastructure (arquivos, processos, diário)
```

| Camada | Responsabilidade | Principais arquivos |
| --- | --- | --- |
| Core | Modelos de descoberta, plano e Evals; `PathGuard`/`ManagedScope`; `SecretScanner`; hash e diff | `Mariliz.Core/{Discovery,Plan,Evals,Policy,Text}` |
| Application | Descoberta, composição, plano de três vias, aplicação, reversão, Evals, protocolo | `Discovery/ProjectDiscoverer.cs`, `Harness/{HarnessComposer,PlanBuilder,ProposalReader,SettingsMerger,ProjectConfig,ConfigureProjectService}.cs`, `Execution/*`, `Validation/EvalRunner.cs`, `Protocol/EngineSession.cs` |
| Infrastructure | Arquivos com caminho protegido, runner de processos sem shell, diário em disco | `LocalProjectFiles.cs`, `SystemProcessRunner.cs`, `FileRunStore.cs` |
| Host (Rust) | Três comandos (`engine_request`, `pick_project_folder`, `engine_status`) e uma allowlist de métodos; `workspace.authorize` e `engine.*` **não** passam pela UI | `apps/desktop-host/src-tauri/src/{protocol,engine}.rs` |
| UI | Seis etapas, estado num reducer, validação Zod de toda resposta | `apps/desktop-ui/src/features/configure-project/*`, `src/shared/{schemas,services}` |
| CLI | `discover`, `plan`, `apply`, `validate`, `revert` | `src/Mariliz.Cli/*` |

### Protocolo (v1, NDJSON)

Métodos novos: `project.discover`, `harness.plan`, `harness.apply`, `harness.validate`, `harness.revert`, `harness.runs`, `run.snapshot`, `run.cancel`. Aplicação, validação e reversão são **execuções assíncronas**: o pedido devolve `runId`; o progresso chega como eventos (`run.state`, `step.started`, `step.completed`, `file.applied`, `run.completed`) e `run.snapshot` devolve o estado e o resultado (a UI consulta o snapshot, então um evento perdido não perde o resultado).

### Contratos

O formato das respostas é fixado por **fixtures golden** em `packages/contracts/fixtures/golden/` (geradas pelo motor real) e por `packages/contracts/fixtures/proposal/{valid,invalid}`. Os dois lados verificam:

- C#: `ContractGoldenTests` regenera a resposta e compara com a fixture; o schema JSON da proposta é validado contra as fixtures válidas e inválidas.
- TypeScript: `engine.test.ts` parseia as mesmas fixtures com os schemas Zod da UI.

Mudança proposital de formato: `MARILIZ_UPDATE_GOLDEN=1 dotnet test`, e revisar o diff das fixtures. (Isto resolve a decisão pendente da seção 4 do blueprint: validação por Zod no TypeScript e por JSON Schema no C#; **tipos TS gerados continuam fora do escopo**.)

## 3. Como o TLC é usado

- **Único SDD.** Nenhum outro kit é oferecido, detectado como alternativa ou instalado.
- **Detecção** (`TlcInspector`): `not_installed`, `installed`, `drifted` (hash diferente do registro do instalador) ou `invalid` (arquivos obrigatórios ausentes). Validação: 15 arquivos obrigatórios (o conjunto comum às versões testadas), hash do conteúdo igual ao `contentHash` de `.agents/.skill-lock.json`, versão conhecida (testadas: 3.3.0 e **4.0.0**, esta última contra uma instalação real; outra versão é aceita **com limitação dita**). O lock do projeto grava a versão realmente instalada.
- **Instalação.** Só pelo instalador oficial, e **só com a aprovação do comando**, mostrado por inteiro antes:
  `npx -y @tech-leads-club/agent-skills install -s tlc-spec-driven -a claude-code`. O motor não inventa comandos do TLC.
  **Escopo do instalador (achado da execução real):** ele grava na pasta do `package.json` **mais próximo, subindo pelas pastas-pai**, e não necessariamente na pasta de trabalho. Um projeto sem `package.json` (por exemplo, só .NET) abaixo de uma pasta que tenha um instalaria o TLC **fora do projeto**. Por isso o plano checa isso, somente leitura, e **bloqueia** o comando (`TLC_BLOCKED`, dizendo a pasta) em vez de arriscar; a saída é criar um `package.json` na raiz do projeto ou instalar o TLC manualmente. Reinstalar uma skill alterada (`--force`) exige uma permissão separada, o comando é marcado como **destrutivo** e os arquivos atuais são copiados antes.
- **Sempre a versão mais recente.** O instalador oficial não fixa versão, então toda **instalação nova** traz a atual. Para um TLC **já instalado**, o plano compara com a última versão conhecida e validada pelo produto (`TlcManifest.LatestKnownVersion`, hoje 4.0.0; o plano é local e não consulta a rede): se a instalada for anterior, o plano propõe o comando `tlc-update` (o mesmo instalador oficial, aprovado por você, não destrutivo quando a instalação está íntegra). O instalador substitui a pasta da skill inteira, removendo os arquivos que a versão nova aboliu. Para buscar uma versão ainda mais nova que a conhecida, há o pedido explícito de uso único (**"Buscar a versão mais recente do TLC"** na UI, `--update-tlc` na CLI). Se a atualização não for aprovada, o TLC atual é mantido e o resultado diz que ficou pendente (EV-IDEMP `blocked`); se o instalador falhar, a versão anterior é restaurada. Uma versão nova ainda desconhecida do produto é aceita **com limitação dita** (não validada), nunca em silêncio.
- **Validação do TLC** (EV-TLC): estrutura, hash e os dois gates por script — `check_commit.py` e `lessons.py selftest` — executados de fato; sem Python, o Eval fica `blocked` com o motivo.
- **Propriedade.** Os arquivos do TLC só são graváveis por `WriteScope.TlcRestore` (restauração após falha); a configuração gerenciada nunca os toca. Licença da skill (CC-BY-4.0, atribuição ao Tech Leads Club) é mostrada na etapa B.
- **Verificador independente** (`.claude/agents/verifier.md`): reflete a regra do TLC de que quem escreve não verifica.

## 4. Como o Harness Engineer é usado

**Achado do diagnóstico:** nenhum Harness Engineer com origem e licença comprovadas foi autorizado para reuso. Por isso o produto traz um agente **original e genérico** (`.claude/agents/harness-engineer.md`), sem cópia de ativos de terceiros ou de clientes.

Divisão de papéis, para que o motor continue determinístico:

1. O **motor** nunca chama um LLM. Ele descobre, valida e aplica.
2. O **agente Harness Engineer**, rodando no ambiente de IA da pessoa, lê o projeto e escreve `.mariliz/proposal.json` (schema `harness-proposal.v1`, instalado em `.mariliz/schemas/`).
3. O motor lê a proposta (`ProposalReader`) e só aceita: ids do catálogo, decisões `enable|keep|skip`, regras adicionais em texto e observações classificadas como `fact|inference|hypothesis|unverified`. **Rejeita** hooks, permissões, comandos, servidores MCP, instalação, segredos e injeção de frontmatter.
4. Precedência: `mariliz.yaml` (a pessoa) > proposta > regras-base.

Sem proposta, o plano usa as regras-base e **diz isso** (`proposalOrigin: baseline-rules`, limitação `HARNESS_ENGINEER_NOT_INVOKED`). A invocação do agente é manual nesta fase (ver §9).

## 5. Componentes gerenciados

| Tipo | Arquivos | Validação (Eval) |
| --- | --- | --- |
| Agents | `.claude/agents/{harness-engineer,code-reviewer,verifier}.md` | EV-AGENT: frontmatter, seções, ferramentas permitidas, somente leitura sem `Edit`/`Write` |
| Skills | `.claude/skills/harness-contract/SKILL.md` | EV-SKILL |
| Hooks | `.claude/hooks/mariliz-policy.py` + entrada em `.claude/settings.json` | EV-HOOK: executa o script com 11 payloads (rede, processos, escrita fora do projeto, `.env`…) |
| Rules | `.claude/rules/mariliz-{general,stack-dotnet,stack-javascript,repo}.md` (+ regras da proposta) | EV-RULE, EV-REF (referências) |
| Tools/MCPs | Só 4 regras `Read(./.env*)` negadas em `settings.json`; **nenhum MCP é instalado ou alterado**. Um `.mcp.json` existente é inventariado e varrido, não modificado | EV-SEC: sem credenciais em arquivos gerenciados, `.mcp.json`, `mariliz.yaml` e proposta |
| Evals | `.mariliz/schemas/harness-proposal.v1.schema.json` e os 14 Evals abaixo | — |
| Declaração e lock | `mariliz.yaml` (da pessoa; criado uma vez), `mariliz.lock` (gerado, sem timestamps) | EV-CONFIG, EV-LOCK, EV-IDEMP |

**Evals (14):** EV-CONFIG, LOCK, AGENT, SKILL, HOOK, RULE, REF, TLC, SEC, ENV, REQ, IDEMP (obrigatórios), EV-REGR (opcional; compara com a execução anterior) e EV-JUDGE (juiz por modelo, opcional, **sempre `not_run`** nesta fase).

`settings.json` é *mesclado*: só as chaves gerenciadas mudam, o resto do arquivo da pessoa é preservado, e um JSON inválido é avisado e não é tocado.

## 6. Segurança, idempotência e reversão

- **Somente leitura até aprovar.** Descoberta e plano não escrevem nada (teste compara o hash da pasta antes e depois).
- **Escopo de escrita:** `.claude/`, `.mariliz/`, `mariliz.yaml`, `mariliz.lock`. `PathGuard` recusa `..`, caminho absoluto, symlink e qualquer coisa fora do escopo — antes de gravar.
- **Comandos:** sem shell, executável e argumentos explícitos, só os do plano e só se o id foi aprovado. Efeitos declarados: executa programa, acessa a rede, grava no projeto.
- **Três vias** (lock × disco × desejado): arquivo gerenciado não editado → atualiza; editado pela pessoa → preserva; arquivo seu no mesmo caminho → conflito, "manter" por padrão; "substituir" exige decisão explícita e guarda backup.
- **`planId`** é o hash do plano. A aplicação replaneja e recusa (`STALE_PLAN`) se o projeto mudou depois da revisão.
- **Diário e backups** ficam fora do repositório (`MARILIZ_DATA_DIR`, ou `%LOCALAPPDATA%\Mariliz`, `~/Library/Application Support/Mariliz`, `$XDG_DATA_HOME/mariliz`). Falha ou cancelamento fazem rollback; se a pessoa editou um arquivo no meio, ele **não** é sobrescrito e o resultado vira `requires_intervention` com a lista.
- **Reverter** só a última aplicação e só arquivos cujo hash ainda é o gravado.
- **Uma execução por projeto** por vez; encerrar o motor cancela e desfaz as execuções em curso.
- **Segredos:** `SecretScanner` reprova o Eval, omite linhas com credencial dos diffs e nunca repete o valor em relatório, log ou diário.

## 7. Testes e evidências

Última execução local: **.NET 228 (Application) + 2 (integração do motor), UI 50, Rust 8 — todos aprovados.**

| Cenário exigido | Evidência (teste) |
| --- | --- |
| Projeto novo sem Harness | `ConfigureJourneyTests.Projeto_novo_sem_harness_recebe_a_configuracao_completa…` |
| TLC já instalado | `Tlc_ja_instalado_e_mantido_sem_comando`, `DiscoveryTests.Estado_do_tlc_distingue…` |
| TLC ausente ou incompatível | `Tlc_ausente_e_instalado_somente_com_aprovacao…`, `Tlc_com_versao_nao_validada…`, `Tlc_editado_localmente_exige_autorizacao…`, `Instalador_que_falha_nao_deixa_nenhuma_alteracao` |
| Já possui agentes e regras | `Arquivo_existente_nao_gerenciado…vira_conflito`, `DiscoveryTests.Inventario_mostra_agentes…` |
| Alterações locais não commitadas | `Alteracoes_locais_nao_commitadas_geram_aviso…` |
| Conflitos | `Conflito_so_e_substituido_com_decisao_explicita…`, `Arquivo_gerenciado_editado_pela_pessoa_e_preservado…`; UI: padrão "manter" e "substituir" enviado como `overwrite`; CLI: `Substituir_conflito_exige_a_opcao_explicita…` |
| Aplicação repetida sem duplicação | `Segunda_execucao_nao_altera_nada_e_nao_duplica_o_hook`, `ConfigAndPolicyTests.E_idempotente…` |
| Falha no meio e recuperação | `SafetyTests.Falha_no_meio_da_aplicacao_desfaz_tudo…`, `Falha_parcial_com_edicao_concorrente…`, `ProtocolJourneyTests.Encerrar_o_motor_no_meio…` |
| Ferramenta ou integração sem permissão | `Ferramenta_sem_permissao_ou_ausente_e_dita…`, `Permissoes_hooks_comandos_e_mcp_nao_podem_vir_da_proposta`, `Hook_alterado_para_usar_rede…` |
| Eval aprovado, reprovado e bloqueado | `SafetyTests.Eval_aprovado_reprovado_e_bloqueado_sao_estados_distintos…`; UI: resultado com falha/limitações nunca como "Concluído" |
| Ambiente de IA não suportado | `Ambiente_nao_suportado_gera_apenas_a_declaracao…`, `Sem_ambiente_detectado_ou_selecionado…`; UI: aviso ao escolher Cursor/Codex |
| Segredos não vazam | `Segredo_em_arquivo_de_configuracao_reprova_o_eval_e_nunca_aparece…`, `Proposta_com_segredo_e_rejeitada_sem_repetir_o_valor`, `Linhas_com_credencial_sao_omitidas…` |
| Cancelamento antes da aplicação | `Plano_e_descoberta_sao_somente_leitura`; UI: "Cancelar sem aplicar" não chama `apply`; `Cancelar_durante_a_aplicacao_desfaz…` |
| Arquivo fora do escopo bloqueado | `Escrita_fora_do_escopo_gerenciado_e_bloqueada`, `Plano_com_caminho_fora_do_escopo_falha_antes_de_gravar…`, `Caminho_perigoso_e_recusado` |

Além dos testes unitários e de integração, o **sidecar publicado** (`mariliz-engine.exe`) foi exercitado de ponta a ponta pelo protocolo NDJSON num projeto temporário: `hello → authorize → discover → plan → apply → validate → revert`. Resultado observado: `completed_with_limitations` (TLC não instalado e comando não aprovado ⇒ EV-TLC e EV-IDEMP `blocked`, com motivo), 10 Evals `passed`, 2 `not_run`, reversão verificada e pasta restaurada byte a byte, stderr vazio.

## 8. Como executar e validar localmente

Pré-requisitos: .NET 10 SDK, Node ≥ 22 com pnpm 9, Python 3 (os Evals do hook e do TLC executam scripts), Rust + Build Tools (para o instalador).

```bash
pnpm install
dotnet test Mariliz.sln                    # motor, CLI e contratos
pnpm ui:typecheck && pnpm ui:lint && pnpm ui:test && pnpm ui:build
cargo test --manifest-path apps/desktop-host/src-tauri/Cargo.toml
```

**CLI** (mesmo serviço da UI; nada é gravado sem aprovação explícita):

```bash
dotnet run --project src/Mariliz.Cli -- discover <pasta>
dotnet run --project src/Mariliz.Cli -- plan <pasta> --env claude-code [--update-tlc] [--diff] [--json]
dotnet run --project src/Mariliz.Cli -- apply <pasta> --env claude-code [--approve-command tlc-install] \
    [--overwrite .claude/agents/x.md] [--approve-removals] [--yes]
dotnet run --project src/Mariliz.Cli -- validate <pasta>
dotnet run --project src/Mariliz.Cli -- revert <pasta> [--run <id>] [--yes]
```

Códigos de saída: `0` concluído; `10` concluído com limitações; `1` falhou, cancelado ou recusado; `2` uso incorreto. Sem terminal interativo, `apply` e `revert` exigem `--yes`.

**Aplicativo desktop:** `scripts/build-engine.ps1` publica o sidecar; depois `pnpm --filter @mariliz/desktop-host tauri dev` (ou `tauri build --bundles nsis` para o instalador). Na tela inicial: **Configurar projeto → escolher a pasta → seguir as seis etapas**. Use primeiro uma **cópia descartável** de um projeto.

## 9. Limitações e riscos abertos

1. **O instalador real do TLC foi executado** (versão 1.6.0 do `@tech-leads-club/agent-skills`, instalando o TLC 4.0.0) num projeto descartável, pela CLI do produto: instalação, validação (hash e gates reais), 12 Evals obrigatórios aprovados e reversão completa (40 arquivos restaurados e conferidos). A execução expôs e levou a corrigir: (a) o `npx.cmd` do Windows quebrava ao ser iniciado pelo nome simples (agora o caminho absoluto é resolvido no `PATH`); (b) a lista de arquivos exigidos era da 3.3.0 e rejeitava a 4.0.0; (c) o lock gravava a versão do TLC fixa em `3.3.0`; (d) o instalador pode gravar fora do projeto (ver §3); (e) a saída do instalador era descartada, então a mensagem de erro prometia detalhes que não existiam. Os testes automatizados continuam usando um instalador simulado (`npx` nunca roda no CI). O `selftest.py` da própria skill 4.0.0 falha no Windows (`signal.SIGHUP` inexistente): defeito do TLC, que não afeta os gates usados pelo motor (`check_commit.py` e `lessons.py selftest`).
2. **Harness Engineer:** não havia um existente autorizado para reuso (§4); o agente é original. A invocação é manual (a pessoa pede a ele, no ambiente de IA, que escreva `.mariliz/proposal.json`); o motor não dispara o agente. Até haver proposta, o plano usa regras-base e diz isso.
3. **Só `claude-code` gera configuração.** Cursor e Codex são detectados e avisados; para eles só `mariliz.yaml` é criado.
4. **EV-JUDGE (juiz por modelo) é sempre `not_run`.** Nenhum Eval de qualidade semântica dos agentes foi executado; os Evals são estruturais e comportamentais (hook).
5. **Python é requisito dos Evals do hook e do TLC.** Sem ele, esses Evals ficam `blocked` e o resultado é "com limitações".
6. **Concorrência fora do motor:** uma edição feita por outro programa *durante* a aplicação é detectada na reversão (`requires_intervention`), não impedida.
7. **Tipos TypeScript não são gerados a partir dos schemas;** a UI usa schemas Zod mantidos à mão, protegidos por fixtures golden.
8. **Instalador Windows** foi gerado e o sidecar testado; a instalação em máquina limpa continua sendo verificação manual.

## 10. Critérios de aceite (§13 do prompt)

| # | Critério | Situação |
| --- | --- | --- |
| 1 | Jornada de ponta a ponta | Atendido: UI (testes com motor simulado por fixtures + verificação visual) e sidecar real pelo protocolo; CLI com testes |
| 2 | TLC como único SDD | Atendido |
| 3 | TLC existente detectado e validado | Atendido, inclusive contra uma instalação real da 4.0.0 (ver limitação 1) |
| 4 | Ambiente de IA identificado e configuração aplicada onde suportado | Atendido para `claude-code`; os demais declarados sem suporte |
| 5 | Harness Engineer existente inspecionado e integrado | **Parcial:** não havia um autorizado; integrado um agente original via proposta validada (limitação 2) |
| 6 | Plano distingue preservado/criado/atualizado/removido | Atendido |
| 7 | Agents, Skills, Hooks, Rules, Tools/MCPs, Evals como componentes distintos | Atendido (nenhum MCP é instalado, por decisão) |
| 8 | Aprovação antes de mudar projeto ou ambiente | Atendido (UI e CLI) |
| 9 | Idempotência | Atendido (EV-IDEMP + testes) |
| 10 | Conflitos e arquivos locais preservados ou tratados explicitamente | Atendido |
| 11 | Relatório fiel às evidências | Atendido |
| 12 | Falhas e operações não executadas identificadas | Atendido (`blocked`/`not_run`/limitações) |
| 13 | Testes executados e registrados | Atendido (§7) |
| 14 | Nenhum ativo de autorização incerta incorporado | Atendido: todo asset embutido é original |
| 15 | Local-first, sem nuvem | Atendido: nenhuma chamada de rede do produto; a única rede possível é o instalador do TLC, só com aprovação |
