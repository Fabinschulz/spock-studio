# Análise de lacunas

> Parte da auditoria estratégica do SPOCK (2026-11-15). Navegação e método em [README.md](README.md).

Lacunas agrupadas por tipo. Cada linha remete à matriz ([REQUIREMENTS_TRACEABILITY.md](REQUIREMENTS_TRACEABILITY.md)) ou
ao documento de domínio.

## 1. A lacuna estrutural

**O SPOCK planeja sob governança e entrega a execução para fora; ele não coordena o processo de engenharia.**

O que existe é sólido: descoberta do projeto, Harness com diff, aprovação e reversão, RAG lexical rastreável, governança
de ações por hash, juiz independente, auditoria. O que falta é o elo entre plano e entrega:

1. a demanda não chega estruturada (texto livre, sem issue nem classificação: RT-21, RT-22);
2. o plano não vira tarefas delegadas com contrato de retorno (RT-24);
3. ninguém confere o que voltou: diff, testes, critérios de aceite, pendências (RT-09, RT-10, RT-27);
4. o estado não é atualizado nem homologado (RT-26).

O grafo de engenharia (`intelligence/workflows/engineering/graph.py:123-151`) tem dez nós lineares, três com modelo, e o
único efeito é um registro de decisão em `.mariliz/decisions/`. Implementar, testar e revisar ficam com a CLI de IA da
pessoa em modo leitura ou com a própria pessoa. A promessa de "plataforma de engenharia" está à frente da entrega.

## 2. Capacidades ausentes (D)

| Lacuna | Pilar | Impacto | Referência |
|---|---|---|---|
| Detectar pendências deixadas pelo agente (escopo contra diff, critérios contra evidência) | Harness | Muito alto: é a garantia de controle de escopo que a visão exige | RT-27, HC-16 |
| Delegação estruturada e retorno de resultado | Harness / IA | Muito alto | RT-24, HC-08 |
| Classificação da demanda (bug, correção, funcionalidade) | Harness | Alto | RT-22, HC-04 |
| Identificar a arquitetura do código existente | SE | Alto: pré-requisito para respeitar a arquitetura do projeto | RT-03, SE-04 |
| Execução e validação de testes | SE | Alto | RT-09, SE-13 |
| Revisão de diff e de PR | SE | Alto | RT-10, SE-14, SE-16 |
| Git além de `status` (diff, log, branch) | SE | Médio | RT-11 |
| Leitura de critérios de aceite, descrição e comentários do Jira | Integrações | Alto | RT-51, INT-07, INT-08 |
| Branches, commits, reviews e checks do GitHub | Integrações | Alto | RT-52, INT-14 a INT-16 |
| Subagentes, paralelismo, dependências entre tarefas | IA | Médio, depende do elo de delegação | RT-32 |
| Code smells e refatoração por critério | SE | Médio | RT-04 |
| SPOCK como cliente MCP | IA | Baixo | RT-41 |
| Sistema de plugins | IA | Indefinido: não há decisão de que seja requisito | RT-42 |
| Onboarding, renomear e remover projetos | UX | Médio | RT-89 |
| Base local de conhecimento de referência no índice | Conhecimento | Médio | RT-17, ST-08 |
| Skills por stack | IA / SE | Médio | ST-06 |
| Atualização automática; assinatura de instaladores | Distribuição | Alto antes de distribuir | RT-90, LIC-17 |
| EULA e avisos de terceiros no instalador | Distribuição | Bloqueia distribuição comercial | RT-75, LIC-07, LIC-09 |

## 3. Capacidades parciais (B) com maior distância da visão

| Lacuna | O que falta | Referência |
|---|---|---|
| Validação com modelo real | Nenhum fluxo com modelo foi exercitado com chave válida | RT-38, AI-23 |
| Seleção de modelo por tarefa | Finalidades por etapa; custo, latência, disponibilidade; troca em falha | RT-34 |
| Qualidade do RAG | Recall@5 de 0,25; reformulação só na investigação; arquivos longos truncados | RT-43 |
| Agentes especializados | Só planning, architecture e judge | RT-31 |
| Azure e GCP | Nenhuma chamada autenticada real | RT-60 |
| Apply, drift e state em nuvem real | Validado só com recursos locais | RT-65 |
| Suporte por stack | Regras só .NET e JS genérico; Angular sem `.html`; sem workspace de monorepo | RT-16 |
| Política de contexto por vínculo | Um booleano libera índice, workflow e assistente de uma vez | RT-50 |
| Contratos | 6 schemas para cerca de 110 métodos; lista de métodos em três lugares | RT-82 |
| Retomada após queda | Só a pausa de aprovação sobrevive | RT-33 |

## 4. Regressões e perdas (G e F)

| Item | Natureza | Referência |
|---|---|---|
| Ambiente limpo para processos filhos virou ambiente herdado | G: promessa de segurança perdida | S-16, RT-83 |
| Agentes de profile do discovery abandonados sem registro | F: capacidade planejada sem decisão de descarte | S-07 |
| Critério de continuidade (usuário externo) abandonado | F | S-20 |
| Fronteira "não executa comandos" deslocada sem ADR | F | S-06 |
| Três caminhos de IA fora do broker | F | S-17 |
| Geração de tipos TS trocada por fixtures sem registro | F (com proteção equivalente parcial) | S-12 |
| Texto "nesta fase" na UI | G leve | UX-33 |

**Não foi encontrada perda de funcionalidade causada pelo renome Mariliz para Spock.** O renome trocou só o que é visível;
identificadores e contratos ficaram ([HISTORICAL_EVOLUTION.md](HISTORICAL_EVOLUTION.md) §D.3).

## 5. Conflitos entre documento e código (H)

| Documento | Diz | Realidade | Ref. |
|---|---|---|---|
| `docs/compliance/LICENSE_DECISIONS.md:9,60` | D-02 aguarda aprovação e, ao mesmo tempo, "liberado, sem trava" | Build padrão sem trava | RT-70 |
| `docs/compliance/LICENSE_DECISIONS.md:12` | D-05 adotada (cumprir a Sleepycat) | O dono declara D-05 como aguardando | RT-71 |
| `docs/compliance/TRADEMARK_ASSESSMENT.md:8-11` | A sigla é usada na interface | Nenhuma tela mostra "SPOCK" | UX §4.1 |
| `docs/plataforma/evolucao-engineering-management.md:465-466` e comentários dos SVG | Ícone ligado ao personagem | Conflita com a regra de identidade de 15/11 | RT-87 |
| `docs/plataforma/MARILIZ_CURRENT_STATE.md:116` | Recall@5 0,33 | Medido 0,25 | RT-43 |
| `docs/plataforma/MARILIZ_CURRENT_STATE.md` §3.9 | Apply, state, drift e custo da IaC "Inexistente" | Código e testes existem | SE H-01 |
| `docs/plataforma/INFRASTRUCTURE_MANAGEMENT.md:84` | Validate, plan e apply viram execuções | Só geração e bootstrap | RT-66 |
| `docs/plataforma/CLOUD_CONNECTORS_STATUS.md` | 9 leituras AWS | 12 no código | INF |
| `docs/compliance/IMPLEMENTATION_STATUS.md` | 6 ferramentas MCP de IaC | 7 | INF |
| `docs/architecture/adr/0001-langgraph-e-contratos-v2.md` | D4 só OpenAI; D8 pendente | Três provedores; D8 feita (`d8a661e`) | AI-17 |
| `intelligence/server.py:66` | Checkpoint consistente a cada passo | `durability="exit"` | RT-33 |
| `docs/blueprints/architecture-blueprint.md` | Host sem comandos arbitrários; 3 ou 5 comandos; sem porta; ambiente limpo; transporte no Runtime | Terminal PTY; 10 comandos; duas portas loopback; ambiente herdado; transporte na Application | ARCHITECTURE_AUDIT §7 |
| `docs/architecture/backend-architecture.md`, `frontend-architecture.md` | 8 pastas da Application; 7 features | 25 contextos; 18 features | ARCHITECTURE_AUDIT H8 a H10 |
| `docs/validacao-real.md` | GitHub "não rodado" | Validado com repositório real | SE H-02 |
| `docs/plataforma/SPOCK_PRODUCT_EVOLUTION.md:21,322` | "Não é plataforma de automação" | Visão de 14/11 diz o contrário | S-19 |
| `docs/product-discovery.md`, `implementation-roadmap.md`, `ui-modernization.md` | A mesma nota de 30/10 repetida 60, 55 e 35 vezes | Ruído nos documentos-fonte da visão | HISTORICAL_EVOLUTION §D.3 |

## 6. Pendências de decisão (I) que bloqueiam evolução

| Decisão | O que bloqueia |
|---|---|
| D5: trazer o Harness Engineer para dentro do produto | O elo de execução (RT-07, RT-24) |
| D3: embeddings | Busca semântica (RT-44) |
| D-02, D-05, D-06, D-07, D-09, D-10, D-11, D-12 | Distribuição comercial |
| Nome canônico exibido e identidade visual | Comunicação do produto, landing page |
| Automação empresarial está ou não no escopo | Roadmap de produto (K, L, M) |
| Plugins são requisito? | Arquitetura de extensibilidade |
| Escrita em Jira e GitHub | Atualização de estado e homologação (RT-26) |
