# Registro de decisões substituídas

> Parte da auditoria estratégica do SPOCK (2026-11-15). Navegação e método em [README.md](README.md).

Cada item compara a abordagem anterior com a atual. A classe E (melhoria válida) só é usada quando há evidência
comprovável; sem ela, o item fica em F (sem justificativa suficiente), G (perda), H (conflito) ou I (pendente).

## 1. Resumo

| ID | Antes | Agora | Classe | Conclusão em uma linha |
|---|---|---|---|---|
| S-01 | Sem Python; "a intelligence não chama modelo" | Python + LangGraph para workflows de IA | E | Justificado por ADR com medição; manter |
| S-02 | Stdio como único transporte local; "não há porta de rede" | Stdio host-motor e HTTP em 127.0.0.1 com token para Python e proxy de modelo | E | Justificado; atualizar o blueprint |
| S-03 | Chroma embarcado (exploração) | SQLite FTS5 + BM25 + símbolos + RRF por projeto | E | Melhor para desktop; qualidade real ainda baixa |
| S-04 | TLC visível como método e seletor de metodologias | Modelo de implementação próprio; TLC interno | E | Coerente com produto próprio |
| S-05 | D4: só OpenAI, teto fixo | Três provedores por conexão, modelo por finalidade, teto configurável | E / H | Melhoria real, mas tetos incoerentes com o Python |
| S-06 | "O SPOCK não executa comandos" (implícito) | Terminal da pessoa, CLIs de IA só leitura, `tofu`/`terraform` com aprovação | F | Mudança grande sem ADR nem modelo de ameaça |
| S-07 | Agentes de profile (implementer, test-engineer, architect) | Só `harness-engineer`, `code-reviewer`, `verifier` | F | Perda sem registro |
| S-08 | Direção "Sessão" (conversa-canvas) | Workbench | E | Justificado: não prometer interlocutor inexistente |
| S-09 | Teal `#33C1BA` como identidade, tema escuro | Carvão neutro com teal só como assinatura | E | Decisão de produto registrada (fase V) |
| S-10 | Terraform como mecanismo implícito | OpenTofu padrão, Terraform opcional | E | Licença MPL e preferência pelo gratuito |
| S-11 | Evals Tier 0 no MVP, sem LLM | Tier 0 + juiz por modelo + evals de gestão | B | Ampliação nunca validada com modelo real |
| S-12 | Geração de tipos TS a partir dos schemas | Fixtures golden em três linguagens | F / B | Proteção real, mas a decisão não foi registrada |
| S-13 | Chave do modelo em arquivo | Cofre do SO, fallback em arquivo sem cofre | E | Mais seguro; decisão NET-E5 registrada |
| S-14 | Servidor MCP como P2 "depois da fase F" | Antecipado na ordem do ecossistema | E | Reordenação aprovada pelo dono |
| S-15 | Host Rust sem comandos arbitrários (blueprint) | PTY no host | F | Ver S-06 |
| S-16 | Processos filhos com ambiente limpo (blueprint) | Ambiente herdado inteiro | G | Regressão de promessa de segurança |
| S-17 | Toda IA governada pelo broker do motor | Quatro caminhos de IA, três fora do broker | F | Fragmentação sem regra |
| S-18 | Ícone com referência ao personagem (pedido de 11/11) | Proibição de associação (15/11) | H / I | Código e docs ainda descrevem a origem |
| S-19 | "O Spock não é plataforma de automação" (12/11) | "Ecossistema de automação empresarial" (14/11) | I | Visão sem reconciliação |
| S-20 | Critério: um usuário externo antes das Fases 3+ | Avanço sem registro do critério | F | Abandono silencioso de um gate de produto |
| S-21 | Jornada "Modernizar sistema" (Fase 6) | Cartão "Em breve" sem plano | D / H | Promessa na UI sem trabalho correspondente |
| S-22 | "Regras e transporte NDJSON no Runtime" (blueprint) | Transporte em `Application/Protocol` | H | Menor; atualizar o blueprint |

## 2. Análise detalhada das substituições relevantes

### S-01: Python e LangGraph

| Critério | Avaliação |
|---|---|
| Abordagem anterior | Tauri + React + motor .NET 10 + CLI .NET; Python "necessário só para reaproveitar gates" (`product-discovery.md:53`); a intelligence não chamava modelo |
| Abordagem atual | `intelligence/` em Python com LangGraph para o workflow de engenharia e a investigação; .NET continua autoridade |
| Motivo da mudança | ADR-0001 (`docs/architecture/adr/0001-langgraph-e-contratos-v2.md:45-69`): grafos com interrupção, checkpoint e streaming; pedido do dono de 02/11 |
| Benefícios | Checkpoint e pausa de aprovação que sobrevive ao reinício; ecossistema de orquestração maduro; separação clara "Python propõe, .NET decide" |
| Desvantagens | Terceiro runtime; instalador maior (+8 MB medidos no spike); dois saltos HTTP por chamada de modelo |
| Impacto arquitetural | Fronteira de autoridade preservada; fronteira de IA entre .NET e Python não decidida (ver S-17) |
| Impacto operacional | Ciclo de vida a mais (vigia de processo pai testado); três toolchains no CI |
| Segurança | Python nunca vê a chave; token por execução; só 127.0.0.1 |
| Escalabilidade | Suficiente para grafos locais; o grafo atual é linear e não usa o paralelismo disponível |
| Alternativas | Orquestrador em .NET (reescrever 4 mil linhas e perder LangGraph); tudo em Python (perder o .NET que tem 30 mil linhas de domínio) |
| Melhor decisão atual | Manter. Registrar a regra da fronteira de IA (S-17) |
| Evidência | ADR-0001 com spike D1 (48 MB em disco, importação ~0,7 s, sem rede); `intelligence/workflows/engineering/graph.py` |

### S-02: transportes locais

| Critério | Avaliação |
|---|---|
| Abordagem anterior | Stdio NDJSON host-motor; HTTP em loopback rejeitado para esse trecho (`product-discovery.md:240`); "não há porta de rede" (`architecture-blueprint.md:87`) |
| Abordagem atual | Stdio NDJSON host-motor (mantido) + HTTP em 127.0.0.1 com Bearer para o Python e para o proxy de modelo |
| Motivo da mudança | Streaming de eventos do grafo e proxy de modelo com token por execução (ADR-0001) |
| Benefícios | O trecho mais sensível continua sem porta; o Python é isolado e a chave fica no .NET |
| Desvantagens | Duas portas loopback; qualquer processo local pode tentar conectar (mitigado por token) |
| Impacto arquitetural | Neutro |
| Impacto operacional | Portas efêmeras; sem conflito conhecido |
| Segurança | Token comparado com `compare_digest`, limite de 4 MB no corpo, só 127.0.0.1 |
| Escalabilidade | Adequada |
| Alternativas | Named pipes ou stdio também para o Python (mais simples, perde o streaming HTTP pronto do servidor) |
| Melhor decisão atual | Manter; corrigir `architecture-blueprint.md:87` |
| Evidência | ARCHITECTURE_AUDIT §1.1 e §3 |

### S-03: tecnologia de RAG

| Critério | Avaliação |
|---|---|
| Abordagem anterior | Chroma embarcado considerado; Qdrant e LanceDB também; Chroma com Docker não aparece em nenhuma fonte |
| Abordagem atual | SQLite FTS5 com BM25, símbolos e fusão RRF (k=60), um arquivo por projeto, chunking estrutural, incremental por hash |
| Motivo da mudança | Orçamento de 30 MB de dependências; Chroma puxa onnxruntime e numpy (`auditoria-orquestracao-agentic.md:381-383`) |
| Benefícios | Zero processo novo, zero dependência pesada, isolamento testado, fonte rastreável |
| Desvantagens | Sem busca semântica; recall@5 medido em 0,25 (publicado 0,33) em perguntas reais sobre o próprio repositório |
| Impacto arquitetural | Coeso: índice e checkpoint no mesmo tipo de armazenamento |
| Impacto operacional | Sem servidor; o usuário de desktop não precisa de Docker |
| Segurança | Nada sai da máquina |
| Escalabilidade | Adequado a 1 a 15 mil trechos; arquivos acima de 24.000 caracteres indexados só no começo |
| Alternativas | sqlite-vec (já no pacote, um mantenedor, 0.1.9), Chroma servidor (exige Docker), LanceDB (pyarrow estoura o orçamento), pgvector (exige PostgreSQL) |
| Melhor decisão atual | Manter FTS5. Antes da D3, medir dicionário de termos e reformulação da consulta no fluxo de engenharia; depois avaliar sqlite-vec com embeddings do provedor sob consentimento |
| Evidência | AI_ENGINEERING_AUDIT §4, com fontes externas citadas |

### S-05: provedores e modelos

| Critério | Avaliação |
|---|---|
| Abordagem anterior | D4 (05/11): só OpenAI, `gpt-4o-mini`, 8.000 tokens e 60 s por execução |
| Abordagem atual | OpenAI, Anthropic e Google como conexões; modelo por finalidade (`assistant`, `judge`, `workflow`); teto por finalidade configurável |
| Motivo da mudança | Fase F e I1; pedido de independência de fornecedor |
| Benefícios | Fim da dependência de um fornecedor; a pessoa escolhe o modelo do juiz |
| Desvantagens | Finalidades grossas; Anthropic como segundo provedor sem confirmação explícita do dono (`SPOCK_IMPLEMENTATION_ROADMAP.md:806`) |
| Impacto arquitetural | Bom: o agente nunca escolhe modelo nem chave |
| Impacto operacional | Teto da tela acima do teto do Python (64.000 contra 20.000) gera falha silenciosa |
| Segurança | Chave por provedor no cofre |
| Escalabilidade | Sem metadados de custo, latência e disponibilidade; sem troca automática em falha |
| Alternativas | Política declarativa por etapa (planejar, classificar, reformular, julgar) com capacidade, custo e latência |
| Melhor decisão atual | Manter a direção; corrigir os tetos (P0) e refinar as finalidades (P2) |
| Evidência | `ModelPurposes.cs:64-69`, `ModelBudgets.cs:16-21`, `engineering/state.py:17` |

### S-06 e S-15: de "não executa comandos" a "executa com autorização"

| Critério | Avaliação |
|---|---|
| Abordagem anterior | O motor só aplica arquivos gerenciados; o Harness Engineer roda fora; o host "não executa comandos arbitrários" |
| Abordagem atual | Terminal PTY no host (fase W); CLIs de IA em modo leitura (I2); `tofu`/`terraform` plan e apply com aprovação ligada ao plano; download e instalação de binários verificados |
| Motivo da mudança | Pedido de 14/11 (shell com terminal) e pedido de 15/11 (Infrastructure Management) |
| Benefícios | Capacidades reais de infraestrutura; terminal integrado; aprovação ligada ao hash do plano é um controle forte |
| Desvantagens | Superfície de ataque maior; nenhum ADR descreve a nova fronteira; o blueprint ficou falso |
| Impacto arquitetural | Host passou a ter regra de domínio (tabela de prazos em `engine.rs:26-59`) e um shell |
| Impacto operacional | Mais processos filhos; ambiente herdado inteiro pelos filhos do motor (S-16) |
| Segurança | CSP estrita e terminal só por ação da pessoa mitigam; falta modelo de ameaça escrito |
| Escalabilidade | Não se aplica |
| Alternativas | Manter só operações tipadas para agentes e deixar o terminal como recurso da pessoa, explicitamente fora do alcance dos agentes (é o que já acontece, mas não está escrito) |
| Melhor decisão atual | Aceitar a mudança como direção, mas registrar ADR com a fronteira: o que a pessoa executa, o que o SPOCK executa com aprovação, o que agentes nunca executam |
| Evidência | `desktop/host/src-tauri/src/terminal/`; `AiTools/AiToolCatalog.cs:17-23`; `Iac/IacOperationService.cs` |

### S-07: agentes especializados de implementação e teste

| Critério | Avaliação |
|---|---|
| Abordagem anterior | Profiles com `dotnet-architect`, `dotnet-implementer`, `test-engineer`, `performance-optimizer`, `frontend-engineer` (`product-discovery.md` §3; roadmap Fase 2) |
| Abordagem atual | Harness gerado com `harness-engineer`, `code-reviewer`, `verifier` (`runtime/Mariliz.Application/Assets/agents/`) |
| Motivo da mudança | Não encontrado. Dependia da titularidade dos ativos de referência (H-62), nunca resolvida |
| Benefícios | Núcleo menor e original, sem risco de titularidade |
| Desvantagens | O pilar "agentes especializados" ficou sem conteúdo; o prompt desta auditoria cobra exatamente isso |
| Impacto arquitetural | Nenhum código perdido (nunca existiu); perda de capacidade planejada |
| Impacto operacional | Não se aplica |
| Segurança | Não se aplica |
| Escalabilidade | Não se aplica |
| Alternativas | Agentes por stack gerados a partir do perfil tecnológico, escritos do zero |
| Melhor decisão atual | Decidir explicitamente quais agentes especializados existem e quem os executa (relaciona-se a D5) |
| Evidência | HISTORICAL_EVOLUTION H-09 e H-10 |

### S-16: ambiente dos processos filhos

| Critério | Avaliação |
|---|---|
| Abordagem anterior | "Processos com ambiente limpo" (`architecture-blueprint.md:322`) |
| Abordagem atual | `SystemProcessRunner.cs` cria `ProcessStartInfo` (que herda o ambiente) e só sobrepõe variáveis do `ProcessSpec` (conferido nesta auditoria) |
| Motivo da mudança | Não registrado |
| Benefícios | Ferramentas como `npx`, `git` e CLIs de IA acham o que precisam no ambiente |
| Desvantagens | Variáveis com segredos do ambiente do motor chegam a CLIs de IA e outras ferramentas |
| Impacto arquitetural | Dois comportamentos para o mesmo problema: o terminal filtra segredos, o motor não |
| Impacto operacional | Nenhum ganho medido |
| Segurança | Regressão de uma promessa de segurança |
| Escalabilidade | Não se aplica |
| Alternativas | Reaproveitar o filtro do terminal por padrão e deixar `ProcessSpec` pedir variáveis explícitas (cloud, IaC) |
| Melhor decisão atual | Restaurar o comportamento prometido (P1) |
| Evidência | ARCHITECTURE_AUDIT smell S2 |

### S-17: quatro caminhos de IA

| Critério | Avaliação |
|---|---|
| Abordagem anterior | Broker no motor como único caminho para modelo, com teto e diário (ADR-0001) |
| Abordagem atual | LangGraph no Python com broker; laços gerar-conferir-tentar no .NET (Ideias, IaC) com `ModelGateway` direto; assistentes com chamada direta; CLIs externas sem contrato de saída |
| Motivo da mudança | Crescimento por fases sem decisão de fronteira |
| Benefícios | Telas de .NET funcionam sem o Python |
| Desvantagens | Sem orçamento único por projeto; governança repetida à mão em cada caminho |
| Impacto arquitetural | Baixa coesão da camada de IA |
| Impacto operacional | Gasto de tokens fora do teto configurado pela pessoa |
| Segurança | Neutro (todos mantêm a chave no .NET) |
| Escalabilidade | Cada nova capacidade de IA tende a criar um quinto caminho |
| Alternativas | (1) ADR com a regra "várias etapas, interrupção ou RAG vão para Python; pergunta e resposta verificada fica no .NET", com teto comum no `ModelGateway`; (2) mover tudo para o Python; (3) eliminar o Python |
| Melhor decisão atual | Opção 1: custo baixo, resolve a ambiguidade e o orçamento |
| Evidência | `IdeaArchitectService.cs:39`, `IacGenerationService.cs:53`, `ManagementAssistant.cs:87`, `PortfolioAssistant.cs:121` |

## 3. Itens que não são substituições, mas parecem

- **Nomes `Mariliz.*`, `.mariliz/`, `MARILIZ_*`, `@mariliz/ui-kit`:** mantidos por decisão do dono para não quebrar projetos
  configurados (H-94). Não é regressão. Renomear exige migração de dados do usuário (`identifier` do Tauri vira a pasta de
  dados). Classe I.
- **Textos gravados nos projetos com "Managed by Mariliz":** mantidos para não mudar o hash do Harness aplicado (H-95).
  Classe I, com proposta pendente.
- **Kits de terceiros (Superpowers, outros SDD):** exploração abandonada por decisão de 30/10 (H-22, H-23). Classe J.
