# Auditoria de gestão de infraestrutura e licenças

> Parte da auditoria estratégica do SPOCK (2026-11-15). Navegação e método em [README.md](README.md).
> Somente leitura: nenhum código foi alterado. Legenda de estados: A implementado e validado, B parcial, C só documentado,
> D não implementado, E substituído por melhoria válida, F substituído sem justificativa, G regressão, H conflito entre
> documento e código, I decisão pendente, J não se aplica.

Data: 2026-11-15. Branch `feat/conformidade-licencas` (HEAD 06443a5, inclui c508d44 e c3786f8). Auditoria **somente leitura**:
nenhum arquivo do repositório foi alterado, nenhuma chamada a AWS, Azure ou GCP, nenhum teste executado (as evidências de
execução citadas vêm dos documentos do próprio repositório e estão marcadas como tal). Consultas web só a fontes oficiais
(URLs no fim).

Legenda: **A** implementado e validado · **B** parcial · **C** só documentado · **D** não implementado · **E** substituído por
melhoria válida, com evidência · **F** substituído sem justificativa · **G** regressão · **H** conflito entre doc e código ·
**I** decisão pendente · **J** não se aplica.

"Validado" aqui significa evidência de execução real registrada no repositório. Testes unitários com fakes contam como B.

---

## Parte 1: capacidades de Infraestrutura

### Tabela INF

| ID | Capacidade | Class. | Evidência (arquivo:linha) | Testes | Observação |
| --- | --- | --- | --- | --- | --- |
| INF-01 | Conector AWS | **A** | `runtime/Mariliz.Infrastructure.Cloud/Aws/AwsCloudProvider.cs:11-115`, `AwsReaders.cs:43-69` | `CloudConnectorTests.cs`, `CloudErrorMappingTests.cs`, `AwsProfileLiveTests.cs` e `AwsPricingLiveTests.cs` (opt-in, `LiveFact`) | Validação real registrada em `docs/plataforma/CLOUD_CONNECTORS_STATUS.md:12,20-39` (SSO, conta real, 8 de 9 leituras). Ver INF-24 sobre a contagem |
| INF-02 | Conector Azure | **B** | `Azure/AzureCloudProvider.cs:16-61,122-126` | Só o formato da configuração (`CloudConnectorTests.cs:184-186`) e o mapeamento de erros (`CloudErrorMappingTests.cs:55-77`). Nenhum teste de leitura nem teste live | O doc reconhece: "implementados, não validados" (`CLOUD_CONNECTORS_STATUS.md:16-18`). Coerente |
| INF-03 | Conector GCP | **B** | `Gcp/GcpCloudProvider.cs:21-54,133-137` | O mesmo do Azure (`CloudConnectorTests.cs:187-189`, `CloudErrorMappingTests.cs:85-101`) | Coerente com `CLOUD_CONNECTORS_STATUS.md:14-18` |
| INF-04 | AWS: perfis, SSO, `aws login`, cadeia padrão | **A** | `AwsCredentials.cs:14-68`; o login interativo é desligado (`:64-67`) | `AwsProfileLiveTests` | `login_session` validado (`CLOUD_CONNECTORS_STATUS.md:29`) |
| INF-05 | AWS AssumeRole | **B**, com defeito | `AwsCredentials.cs:18-22` (15 min, sem session policy) | Só o formato do ARN | **Achado:** para OpenTofu/Terraform, `CloudEnvironment` repassa `AWS_PROFILE = profile ?? sourceProfile` e **ignora o `roleArn`** (`runtime/Mariliz.Application/Iac/IacOperationService.cs:449-452`). Uma conexão "assumir papel X" planeja e **aplica com a identidade do perfil de origem**, não com o papel. A tela de conexões mostra uma identidade e o apply usa outra (possivelmente mais ampla ou de outra conta). O `allowed_account_ids` do modelo mitiga só a troca de conta, não a de privilégio |
| INF-06 | Azure: service principal, workload identity, managed identity | **B** | `AzureCloudProvider.cs:124-125`: `AzureCliCredential` ou `DefaultAzureCredential`, sem navegador | Nenhum | SP, WI e MI só vêm implicitamente pela cadeia padrão (variáveis de ambiente). Não há mecanismo explícito de SP. Para IaC só passam `ARM_SUBSCRIPTION_ID`/`ARM_TENANT_ID` (`IacOperationService.cs:454-456`); o mecanismo de login escolhido na conexão não é repassado ao azurerm |
| INF-07 | GCP: ADC, arquivo de service account, workload identity federation | **B** | `GcpCloudProvider.cs:133-137`; `GOOGLE_APPLICATION_CREDENTIALS` com o caminho da chave (`IacOperationService.cs:458-462`) | Nenhum live | A chave JSON é de longa duração; o Spock guarda só o caminho (`CLOUD_CONNECTION_SECURITY.md:9`). WIF só via ADC/arquivo `external_account`, não verificado |
| INF-08 | Descoberta e consulta de recursos | **A** (AWS) / **B** (Azure, GCP) | `AwsReaders.cs:73-308`; `CloudReadService` | Fakes mais validação AWS real | Colunas fixas, limite e paginação de no máximo 10 páginas (`CLOUD_CONNECTORS_STATUS.md:62`) |
| INF-09 | Escopo e permissões: só leitura | **B** | Só leitura **por allowlist de código** (capacidades fixas, `AwsReaders.cs:43-69`); vínculo por projeto e `agentAccess` (`Mcp/CloudMcpTools.cs:20-31`) | `CloudConnectorTests.cs:83-163` | Não há restrição no IAM: o AssumeRole não aplica session policy (`AwsCredentials.cs:22`). A mesma conexão (às vezes com permissão de escrita) serve às leituras dos agentes e ao apply. O "só leitura" é do Spock, não da credencial. Aceitável, mas o doc (`CLOUD_CONNECTION_SECURITY.md:3`) deve deixar claro que não há contenção por IAM |
| INF-10 | Geração de IaC (AWS) | **B** | `Iac/IacAwsGenerator.cs` (829 linhas), `IacGenerationService.cs:86-109` (execução do Spock com desfazer) | `IacGenerationTests.cs`; live `Terraform_gerado_passa_no_fmt_e_no_validate` (`IacRuntimeLiveTests.cs:279`) | Validado só com `fmt` e `validate`, nunca com plan nem apply em conta real. Azure e GCP sem geração (`INFRASTRUCTURE_MANAGEMENT.md:129-130`) |
| INF-11 | OpenTofu como padrão | **A** | `IacRuntimeService.cs:17,191`; `OpenTofuToolchain.cs` | `IacRuntimeTests`, `OpenTofuRuntimeTests` | D-01 |
| INF-12 | Instalação gerenciada do Terraform | **I** (funciona; política pendente) | `HashiCorpTerraformToolchain.cs`; trava `IacDistributionPolicy.cs:31-42`; gates `IacRuntimeService.cs:235-238,345-350` | 5 testes da política (`IMPLEMENTATION_STATUS.md:169`) | Liberada no build padrão. Ver LIC-02 |
| INF-13 | Versões, adoção, pin e remoção | **A** (Windows) | `IacRuntimeService.cs:89-200`; `OfficialReleaseToolchain.cs:72-147` | `IacRuntimeTests` | Linux e macOS "implementados e não rodados" (`INFRASTRUCTURE_MANAGEMENT.md:128`) |
| INF-14 | Integridade do binário baixado | **A**, com ressalvas | `OfficialReleaseToolchain.cs:91-139`: assinatura OpenPGP do SHA256SUMS com chave embutida, SHA-256 do zip, extração só do binário, hash conferido antes de cada uso (`:88-89`, `IacRuntimeService.cs:216`). Redirecionamento só para a CDN permitida (`:209-233`) | `IacRuntimeLiveTests` (opt-in) | Ressalvas: (a) verificador OpenPGP **próprio** (`OpenPgpSignature.cs:6-9`), só RSA v4, **sem checar expiração nem revogação** da chave; rotação de chave exige nova versão do Spock. (b) O OpenTofu recomenda oficialmente o **cosign** (identidade do workflow do GitHub); o Spock usa o `.gpgsig` (válido, mas não o caminho recomendado). (c) TOCTOU entre `Intact` e a execução (baixo) |
| INF-15 | Validate (fmt e validate) | **A** | `CliIacRuntime.cs:39-42`; `IacOperationService.cs:57-105` | Live `OpenTofu_valida_e_planeja` (`IacRuntimeLiveTests.cs:56`) | Sem backend nem nuvem |
| INF-16 | Plan | **A** (motor) / **B** (nuvem) | `IacOperationService.cs:107-180`; achados de segurança em `IacPlanReader.cs:36-149` | `IacPlanTests`, `IacOperationTests` | O único plan real contra a AWS lê `aws_caller_identity` (`IacRuntimeLiveTests.cs:161-168`). Nada foi planejado sobre recurso real |
| INF-17 | Aprovação humana | **A** | `IacOperationService.cs:182-243`: plano exato (id e hash), plano mais recente, 24 h (`:31`), confirmação de destruição, de riscos e do nome do workspace de produção (`:33,202-207`), auditoria `iac.apply_approved` | `IacOperationTests` | Fora do MCP (`IacMcpTools.cs:8-11`). Aprovador sempre "person" (`:28`): não há identidade de quem aprova nem segundo aprovador. É app desktop, mas sem separação de funções |
| INF-18 | Apply | **B** | `IacOperationService.cs:225-303`; `CliIacRuntime.cs:82-85` (timeout de 55 min) | Live `OpenTofu_aplica_so_o_plano_aprovado` (`IacRuntimeLiveTests.cs:102`) só com `terraform_data` | **Nenhum apply contra nuvem real.** O doc diz "validada com o OpenTofu real" (`INFRASTRUCTURE_MANAGEMENT.md:92`); verdadeiro para o motor, enganoso para "Muda a nuvem". Ver INF-25 |
| INF-19 | Verificação pós-apply | **A** (motor) | `CliIacRuntime.cs:88-94` (plano seguinte, `-detailed-exitcode`); `IacOperationService.cs:296-300` (`verified`, `remaining`) | Live com `terraform_data` | Não exercitado em nuvem |
| INF-20 | State: local, remoto, lock, isolamento | **B** | Local devolvido com conflito (`IacOperationService.cs:249-282`); isolamento (`:118-119`); force-unlock com confirmação (`:384-410`); bootstrap S3/azurerm/gcs (`IacBootstrapTemplates.cs`) | `IacStateTests`; live `Bootstraps_gerados_passam_no_validate` | Bootstraps de Azure e GCP só passam no `validate`. O doc diz "validados com os providers reais" (`INFRASTRUCTURE_MANAGEMENT.md:93`): **H** leve. Sem criptografia de state do OpenTofu (client-side, nativa desde 1.7); o S3 usa SSE `AES256`, não KMS (`TERRAFORM_STATE_MANAGEMENT.md:58`). Migração de backend fora do Spock |
| INF-21 | Segredos em state e plano | **B** | O plano fica em texto puro em `<dados>/projects/<id>/iac/operations/<op>/plan.tfplan` por até 24 h e é apagado só no apply (`IacOperationService.cs:31,288-290`). Outputs `sensitive` filtrados (`CliIacRuntime.cs:184`). `apply.log` gravado com stdout e stderr (`CliIacRuntime.cs:83`) |: | Planos **nunca aplicados** não têm limpeza comprovada (não verificado se existe GC). O `apply.log` pode ter valores não marcados como sensíveis |
| INF-22 | Drift | **A** (motor) / **B** (nuvem) | `IacOperationService.cs:306-341` (`plan -refresh-only`, sem gravar state); MCP só lê o último resultado (`IacMcpTools.cs:33-56`) | Live com `local_file` (`IacRuntimeLiveTests.cs:195`) | Sem agendamento; só o que está no state (`INFRASTRUCTURE_MANAGEMENT.md:132`) |
| INF-23 | Estimativa de custo | **B** | `IacCostEstimator.cs:6-99`, `IacCostService.cs:28-43` (Price List pela conexão, sem inventar uso) | `IacDriftAndCostTests`, `AwsPricingLiveTests` | Só para o desenho gerado (AWS), não para um plano qualquer (`INFRASTRUCTURE_MANAGEMENT.md:131`) |
| INF-24 | Doc das leituras AWS | **H** | `CLOUD_CONNECTORS_STATUS.md:12` diz "9 leituras"; o código tem 12 (`AwsReaders.cs:43-69`: VPCs, sub-redes e `aws.pricing` entraram depois). O roadmap ainda lista "VPC e sub-redes" como futuro (`SPOCK_CLOUD_CONNECTORS_ROADMAP.md:23`) |: | As novas leituras não constam como validadas na tabela de status |
| INF-25 | Afirmação "validada" das entregas 3 e 6 | **H** leve | `INFRASTRUCTURE_MANAGEMENT.md:92,95` |: | A validação é com `terraform_data` e `local_file`. A frase deveria dizer "validada com o OpenTofu real, sem nuvem" |
| INF-26 | Trilha de auditoria | **A** | `Audit/ProjectAudit.cs:128,204-281` (cloud.* e iac.*); `CloudReadService.cs:98`; `IacOperationService.cs:468-483` | Testes de auditoria (não verificado individualmente) | Bem integrada à auditoria do projeto |
| INF-27 | Segurança de credenciais | **A** | `connections.json` só com nomes (`FileCloudStore.cs`; `CLOUD_CONNECTION_SECURITY.md:9`); só variáveis de nome repassadas à ferramenta (`IacOperationService.cs:434-466`); erro do provedor reduzido a código |: | Ressalva do INF-05 (papel não repassado) |
| INF-28 | Governança (achados, produção, sem execução livre) | **A** | `IacPlanReader.cs:111-139`; `IacOperationService.cs:33`; `IIacRuntime` com argumentos fixos | `IacPlanTests` | Produção detectada só pelo nome do workspace (`prd`, `prod`, `production`, `producao`). Um `prod-us` ou `live` escapa |
| INF-29 | Contratos `iac.*` e `cloud.*` | **A** | Fixtures `contracts/fixtures/iac` (11) e `cloud` (4), conferidas pelo Zod (`desktop/ui/src/shared/schema/contracts.test.ts:4-18`) e pelo golden do motor (`ContractGoldenTests.cs`) | 15 testes na tela (`infrastructure-page.test.tsx`) | Não há JSON Schema para estes métodos (`contracts/schemas/protocol/methods` só tem engine, model e workspace). Aceitável, porque Zod mais golden cobrem |
| INF-30 | Lista de ferramentas MCP de IaC | **H** leve | `IMPLEMENTATION_STATUS.md:107-109` lista 6 ferramentas "fixadas por teste"; o código tem 7, com `iac_drift_summary` (`IacMcpTools.cs:16-31`) |: | A ferramenta a mais é só leitura do histórico. Corrigir o doc |
| INF-31 | Provider lock e cache de plugins | não verificado | `CliIacRuntime.cs:251,284` (`TF_PLUGIN_CACHE_DIR` compartilhado por motor) |: | Não verifiquei se o `.terraform.lock.hcl` gerado na cópia volta ao projeto. Sem ele, os hashes dos providers não ficam fixados entre execuções |

### Integração com o resto do produto

| Eixo | Situação | Evidência |
| --- | --- | --- |
| Projetos | **Integrado**: vínculo conexão × projeto; IaC resolve a raiz pelo `WorkspaceAuthorizer` | `CloudProjectService.cs`, `IacOperationService.cs:187` |
| Execuções (runs) | **Parcial**: geração e bootstrap viram execução `infrastructure` com desfazer; validate, plan, apply e drift ficam num histórico próprio (`FileIacWorkArea`), fora do `run.list` | `IacGenerationService.cs:86-93`, `IacStateService.cs:109-113`. Contradiz `INFRASTRUCTURE_MANAGEMENT.md:84` ("cada uma vira execução do Spock"): **H** |
| Aprovações | **Isolado**: a aprovação de IaC é própria e não usa o mecanismo de ações aprovadas dos workflows | `IacOperationService.cs:230-243` |
| Auditoria | **Integrado** | `ProjectAudit.cs:204-281` |
| Agentes externos (MCP) | **Integrado, só leitura** | `IacMcpTools.cs`, `CloudMcpTools.cs` |
| Agentes internos (LangGraph) | **Não integrado** | `CLOUD_CONNECTORS_STATUS.md:48`; roadmap item 5 |
| Gestão (portfólio, timeline) | Não encontrei consumo de iac.* ou cloud.* fora de Audit e Mcp | grep em `runtime/Mariliz.Application` |

Conclusão da Parte 1: o domínio é sólido em engenharia de segurança (aprovação amarrada ao plano, download verificado,
nada de execução livre, trilha completa). Mas o ciclo que muda a nuvem nunca rodou contra conta real, Azure e GCP são
código sem validação, e a integração com runs, aprovações gerais e agentes internos é parcial. Há um defeito funcional e de
segurança no AssumeRole para IaC (INF-05).

---

## Parte 2: decisões de licença D-01 a D-12

### Tabela LIC

| ID | Decisão (como o dono declara) | O que diz `LICENSE_DECISIONS.md` | O que o código faz | Pendente tratado como aprovado? | Class. |
| --- | --- | --- | --- | --- | --- |
| LIC-01 | D-01 OpenTofu padrão (adotada) | Adotada (`:8,25-31`) | `IacRuntimeService.cs:17`; ranking e oferta sem engine = OpenTofu | Não | **A** |
| LIC-02 | D-02 Terraform gerenciado (aguarda aprovação e parecer) | "**Aguarda aprovação e parecer**" (`:9`), mas "Situação atual: liberado, sem trava ... decisão do dono" (`:60`) | Build padrão `development` = `Unrestricted` (`IacDistributionPolicy.cs:29,39`). Instala Terraform de `releases.hashicorp.com`. A trava só vale com `-p:SpockEdition=commercial` (`Mariliz.Runtime.csproj:10-11`, `Program.cs:75-77`, `build-runtime.ps1:15,37`). **O CI não passa a edição**: `ci.yml:144` (Windows, padrão `development`) e `ci.yml:149-150` (Linux, sem a propriedade) | **Sim, de fato.** Todo instalador gerado hoje (os únicos que existem, artefatos do CI) instala e opera o Terraform gerenciado. Os docs de produto tratam como liberado: `INFRASTRUCTURE_MANAGEMENT.md:65-66` ("liberado ... sem trava") e `TERRAFORM_DEPENDENCY_MANAGEMENT.md:17`. A política (LIC-10) registra o Terraform como revisão registrada, que não bloqueia | **I + H** |
| LIC-03 | D-03 download oficial sob demanda (adotada) | Adotada (`:33-38`) | `OpenTofuToolchain.cs:48-55`, `HashiCorpTerraformToolchain.cs:56-60`, `OfficialReleaseToolchain.cs:91-139`; o instalador não leva binário de IaC (`tauri.conf.json`, `bundle.resources` só `intelligence/` e `python/`) | Não | **A** (ressalvas do INF-14) |
| LIC-04 | D-04 CPython embarcado (adotada) | Adotada; obrigações: LICENSE.txt, EULA protetivo (MS), licenças nativas no Linux (`:40-46`) | Windows: `LICENSE.txt` do embed. Linux: 16 licenças do PBS copiadas (`scripts/package-intelligence.py:143-144`, `scripts/notices/python-build-standalone/`) | A obrigação "EULA protetivo" depende da D-09, ainda não redigida. A D-04 está "adotada" com obrigação descumprida | **B** |
| LIC-05 | D-05 remoção do `_dbm`/Berkeley DB (aguarda aprovação) | **Diverge do dono**: "Adotada: cumprir a Sleepycat (2026-11-15); parecer pendente" (`:12,71-85`); a remoção foi "descartada" (`:74-75`). `IMPLEMENTATION_STATUS.md:17,114-127` diz que o pedido de remover não foi seguido porque o DB está ligado estaticamente ao `libpython` | O pacote Linux leva `LICENSE-6.0.19.txt` e `SOURCES.md` (`package-intelligence.py:143`). Nenhuma remoção | **Sim, no doc.** O dono declara "aguarda aprovação" (remoção) e o doc registra outra opção (cumprir) como **adotada**. Risco de fundo: a cláusula 3 da Sleepycat exige oferecer o fonte do DB "and any accompanying software that uses the DB software" (`scripts/notices/berkeley-db/LICENSE-6.0.19.txt:24`). O `SOURCES.md` oferece só DB, CPython e PBS, não o Spock. Se o parecer concluir que o Spock "usa o DB", cumprir a Sleepycat significaria abrir o código. O canal de suporte citado no `SOURCES.md` não existe (pendência 2, `:84`) | **H + I** |
| LIC-06 | D-06 formato Linux (aguarda) | Aguarda; recomenda só `.deb` (`:87-97`) | O CI continua gerando AppImage e `.deb` (`ci.yml:111`); nenhum AppImage inspecionado (`LINUX_DISTRIBUTION_REPORT.md:17`) | Não formalmente, mas o artefato AppImage com bibliotecas LGPL é gerado sem o pacote de conformidade. Não há workflow de release (só `ci.yml`, retenção de 3 dias, `:210`) | **I** |
| LIC-07 | D-07 avisos no produto (aguarda) | Aguarda; geração preparada (`:99-106`) | `generate_sbom.py --bundle` existe; **não** ligado ao `tauri.conf.json` nem à tela "Sobre"; 45 lacunas (`IMPLEMENTATION_STATUS.md:139-146`) | Não. Os instaladores atuais não cumprem os avisos de cerca de 480 componentes permissivos (R-04), nem os do .NET self-contained (R-05) | **I** (risco de distribuição) |
| LIC-08 | D-08 CLIs de IA pelo binário do usuário (adotada; confirmações pendentes) | Adotada; R-10 (Anthropic) e R-13 (Cursor) pendentes (`:48-54`) | `AiToolCatalog` em modo de leitura; sem injetar credenciais (`IMPLEMENTATION_STATUS.md:66-79`), não reverificado nesta auditoria | Não | **B** (confirmações jurídicas abertas) |
| LIC-09 | D-09 EULA (aguarda redação) | Aguarda (`:108-116`) | `tauri.conf.json`: sem `bundle.windows.nsis.license`; nenhum EULA no pacote Linux | Não. Bloqueia o cumprimento da D-04 e da R-05/R-06 (cláusulas da Microsoft) | **I / D** |
| LIC-10 | D-10 política de licenças (aguarda) | Aguarda; tabela `:120-127`: BUSL "Proibida em distribuição ... Só como processo externo do usuário" | `license-policy.json` marcado como PROPOSTA; CI `--policy` em modo relatório (`ci.yml:87-88`) | **Parcial, achado.** (1) No escopo `sob_demanda` a lista `forbidden` está vazia e `registered` tem `"dl:terraform": "R-01 / D-02"`. Pelo `generate_sbom.py:1220-1231`, qualquer componente registrado vira "REVISÃO REGISTRADA", que **não** está em `BLOCKING` (`:1170`). Ligado o `--enforce`, o Terraform gerenciado passaria verde **sem** a D-02 aprovada. O mesmo vale para `bin:berkeley-db` (D-05) e `nuget:JsonSchema.Net` (D-11). "Registrado" (risco conhecido) equivale, na prática, a "aprovado". (2) Conflito com a própria D-10: o Terraform baixado e operado pelo Spock não é "processo externo do usuário" | **H + I** |
| LIC-11 | D-11 validador JSON Schema (aguarda) | Aguarda; três opções (`:129-137`) | `JsonSchema.Net` 9.5.0 continua nos testes, rodados a cada CI, e no utilitário `docs/compliance/tools/validate_sbom.cs:1` (`#:package JsonSchema.Net@9.5.0`), que valida os próprios SBOMs | Uso contínuo sob o EULA de taxa enquanto pendente. Ironia: a conformidade dos SBOMs depende do componente sob risco ALTO (R-17) | **I** |
| LIC-12 | D-12 marca SPOCK (aguarda parecer) | Aguarda (`:139-145`); `TRADEMARK_ASSESSMENT.md:17` cita o registro "SPOCK" nº 1990155 da CBS Studios (classe 28) | O nome está em uso no produto (User-Agent `SpockStudio`, `OfficialReleaseToolchain.cs:42`, documentos, UI) | Não formalmente, mas o nome já está exposto e as buscas INPI/USPTO completas não foram feitas (`TRADEMARK_ASSESSMENT.md:20`) | **I** |

### Governança complementar

| ID | Item | Class. | Evidência | Crítica |
| --- | --- | --- | --- | --- |
| LIC-13 | SBOM | **B** | 6 arquivos: CycloneDX 1.6 e SPDX 2.3 (consolidado, Windows e Linux), 957 componentes no consolidado; 891 com hash, 951 com purl, 6 sem licença; SPDX com 37 `licenseConcluded` NOASSERTION (análise local dos JSON) | Válidos contra os schemas oficiais segundo `IMPLEMENTATION_STATUS.md:180` (não reexecutado; a validação é **manual**, fora do CI). Versões não padronizadas ("OpenTofu (tofu)", versão "dinâmica (...)"). O CI **não regenera** os SBOMs: o `--check` compara só nome e versão do inventário (`generate_sbom.py:1284-1294`) e não confere licença nem os arquivos `SBOM/*.json` |
| LIC-14 | Inventário | **A** | `docs/compliance/data/inventory.{json,csv}`, `DEPENDENCY_INVENTORY.md` | Bom: npm, cargo, nuget, pypi e itens manuais (Berkeley DB, chaves, binários sob demanda) |
| LIC-15 | Checagem automática de licenças no CI | **B** | Job `compliance` (`ci.yml:60-88`): testes do gerador, `--check` e `--policy` sem `--enforce` | Só relata. **O GitHub Actions está sem orçamento** (`IMPLEMENTATION_STATUS.md:184-185`): na prática nenhum gate roda. Sem `cargo-deny`, `license-checker` nem scanner de vulnerabilidade (não pedido, mas ausente) |
| LIC-16 | Integridade dos binários baixados | **A** | INF-14 | Ressalvas: chave sem expiração ou revogação, OpenPGP próprio, sem cosign |
| LIC-17 | Integridade dos instaladores | **B** | `ci.yml:200-202`: só SHA-256 publicado junto; sem assinatura Authenticode nem GPG do `.deb`/AppImage | O SHA-256 publicado no mesmo canal não autentica a origem |
| LIC-18 | Avisos no instalador | **D** | `tauri.conf.json` sem recurso de avisos; só a licença do CPython e as do PBS e do Berkeley DB (Linux) | Ver LIC-07 |
| LIC-19 | Isolamento de dependências | **A** | Ferramentas de IaC em `<dados>/tools/<ferramenta>/<versão>/`, fora do PATH (`OfficialReleaseToolchain.cs:33,149-155`); Python embarcado | Bom |
| LIC-20 | Política de instalação | **A** | Proposta com `offerId` (hash de versão, plataforma, URLs e chave, `IacRuntimeService.cs:269-271`); `InstallAsync` exige o mesmo id (`:89-93`); remoção com confirmação (`:136-150`) | Bom. Falta exibir o texto ou link da licença, e o titular correto (ver LIC-21) |
| LIC-21 | Titular da BUSL no produto | **H** | `HashiCorpTerraformToolchain.cs:27` (no arquivo) `License => "BUSL 1.1 (HashiCorp)"`; comentário `:8-9` e `TERRAFORM_DEPENDENCY_MANAGEMENT.md:15` omitem "embedded" | A D-02 manda corrigir para citar "embedded" e o licenciante IBM (`LICENSE_DECISIONS.md:68`). Ainda não foi feito. Fato verificado: o LICENSE do repositório nomeia "International Business Machines Corporation (IBM)" como Licensor; a página hashicorp.com/bsl ainda mostra o modelo genérico "HashiCorp, Inc." |
| LIC-22 | Datas dos docs | observação | `IMPLEMENTATION_STATUS.md:3` e `LICENSE_RISK_REGISTER.md:3` dizem 2026-11-16; os SBOMs têm `2026-11-16T00:42:58Z` | Compatível com UTC (noite de 15/11 no Brasil). Padronizar o fuso para não parecer data futura |

---

## Crítica consolidada

1. **Pendências tratadas como aprovadas.** Três casos: D-02 (Terraform gerenciado ativo no build padrão e no CI, docs de
   produto dizem "liberado"); D-05 (o doc registra como adotada uma opção diferente da que o dono declara pendente); e a
   mecânica da D-10, que converte "registrado" em não bloqueante para D-02, D-05 e D-11. Mesmo com a ressalva de que o dono
   decidiu "liberar enquanto o jurídico negocia" (`LICENSE_DECISIONS.md:60`), a tabela-resumo diz "aguarda", e o produto
   não reflete isso.
2. **A trava comercial existe, mas nada a aciona.** Não há pipeline de release com `-Edition commercial`. A segurança
   jurídica depende de alguém lembrar de um parâmetro manual.
3. **"Validado" está inflado em pontos-chave.** Apply, verificação e drift foram validados sem nuvem; os bootstraps de Azure
   e GCP só no `validate`. O status dos conectores está correto quanto a Azure e GCP, mas desatualizado na contagem AWS.
4. **AssumeRole quebrado no caminho de IaC** (INF-05): o risco de segurança mais concreto do domínio.
5. **A conformidade de distribuição é a maior lacuna comercial:** sem EULA, sem avisos, AppImage LGPL sem pacote de
   conformidade, instaladores sem assinatura. Tudo pendente de decisão do dono, sem bloqueio técnico que impeça publicar
   um instalador nesse estado.
6. **Pontos fortes:** download oficial verificado e isolado, aprovação amarrada ao plano com expiração e confirmações,
   nenhuma execução livre por agentes, trilha completa na auditoria do projeto, SBOM duplo (SPDX e CycloneDX) com hashes e
   purl.

## Riscos (priorizados)

| # | Risco | Severidade | Origem |
| --- | --- | --- | --- |
| R1 | Instalador comercial gerado sem `SpockEdition=commercial` distribui Terraform gerenciado sob BUSL ("embedded") | Alta | LIC-02, `ci.yml:144-150` |
| R2 | Sleepycat: o parecer pode exigir oferta do fonte do Spock no Linux; o doc já declara a decisão como adotada | Alta | LIC-05 |
| R3 | IaC aplica com a identidade do perfil de origem e não com o papel assumido | Alta (segurança) | INF-05, `IacOperationService.cs:449-452` |
| R4 | `--enforce` futuro aprovaria Terraform, Berkeley DB e JsonSchema.Net por estarem "registrados" | Média | LIC-10, `generate_sbom.py:1170,1220-1231` |
| R5 | Distribuição sem EULA nem avisos (cláusulas MS, ~480 permissivos, .NET) | Alta se publicar | LIC-07, LIC-09, LIC-18 |
| R6 | Plano com segredos em texto puro por até 24 h e sem limpeza comprovada dos não aplicados; `apply.log` persistido | Média | INF-21 |
| R7 | Chave OpenPGP embutida sem expiração ou revogação; verificador próprio | Média | INF-14 |
| R8 | Nenhum gate de CI efetivo (Actions sem orçamento) | Média | LIC-15 |
| R9 | Azure e GCP anunciáveis como prontos sem nenhuma chamada autenticada | Média | INF-02, INF-03 |
| R10 | Uso contínuo de JsonSchema.Net sob EULA de taxa (inclusive para validar os SBOMs) | Média (custo) | LIC-11 |
| R11 | Produção detectada só pelo nome exato do workspace | Baixa | INF-28 |
| R12 | Marca SPOCK em uso sem busca de anterioridade concluída (registro da CBS na classe 28) | Média | LIC-12 |

## Fontes externas consultadas

- HashiCorp BSL e Additional Use Grant (definições de "competitive offering" e "embedded"): https://www.hashicorp.com/en/bsl
- LICENSE do Terraform (Licensor IBM, Change License MPL 2.0): https://raw.githubusercontent.com/hashicorp/terraform/main/LICENSE
- LICENSE do OpenTofu (MPL 2.0): https://raw.githubusercontent.com/opentofu/opentofu/main/LICENSE
- Verificação de releases do OpenTofu (SHA256SUMS e cosign): https://opentofu.org/docs/intro/install/standalone/
