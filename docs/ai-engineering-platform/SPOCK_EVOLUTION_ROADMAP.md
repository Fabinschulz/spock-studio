# Roadmap de evolução: Spock como plataforma de engenharia com IA

> Parte do estudo [Plataforma de engenharia com IA](README.md), 2026-11-17. **Proposta, não plano aprovado.** Cada
> entrega precisa de aprovação do dono e vai em PR próprio contra `master`, uma por vez (sem empilhar PR). As ids `R*`
> são das [recomendações](SPOCK_ARCHITECTURE_RECOMMENDATIONS.md); quando há item equivalente no
> [roadmap do hub](../engineering-intelligence-hub/ROADMAP.md), ele aparece entre parênteses e este roadmap não o
> substitui.

## Situação

| Entrega | Estado |
| --- | --- |
| W0-1 contexto do RAG íntegro | Feita: corpo do trecho no prompt de planning, architecture, judge e prévia; memória da investigação com o corpo; aviso `INDEX_FILES_TRUNCATED`; reserva da entrada estimada no orçamento. Falta o aviso do corte de 24.000 caracteres feito no motor, que precisa de campo novo no contrato |
| C-5 documentos desatualizados | Feita junto com o W0-1 |
| W0-2 fonte única de tecnologias | Feita: `Discovery/Technologies.cs` com id, nome, categoria, família de regras, área e padrão da arquitetura; famílias de regra em `Harness/StackRuleProfiles.cs`; caminhos dos ativos em `Catalog.Asset`. Corrigiu o mapa de áreas do Python, que ignorava React, Next.js e Angular. O catálogo de componentes já tinha paridade testada com as fichas; a única duplicação ali eram os caminhos dos ativos |
| W0-3 testes estáveis | Feita: o teste de servidor do Python falhava por RST do Windows ao recusar o token sem ler o corpo (corrigido, com teste que reproduz); testes de página da interface com limite de 15 s; `verify.py --repeat N`. As 3 falhas .NET do diagnóstico não se repetiram em 5 rodadas, uma delas sob carga |
| W1-1 pasta Organização no índice | Feita: o motor embute `docs/organizacao/` e cada seção vira `@reference/` no índice de todo projeto (em vez de um índice separado: reaproveita sincronização por hash, busca e MCP), com título, fonte e data em cada trecho; prompts e `search_project` marcam como referência; prefixo bloqueado para arquivos do projeto. Sem filtro por tecnologia ainda (W1-2): "estado React" traz também a seção Angular |
| W1-2 filtro por tecnologia | Feita: a linha `Escopo:` de cada seção vira `technologies` no frontmatter do documento; o índice Python guarda isso como metadado (esquema 3) e a busca deixa de fora o que é de outra stack; o fluxo de engenharia e a prévia filtram pelas stacks detectadas, e o `search_project` pelo perfil do projeto. `ai.knowledge.search.v1` ganhou `filters.technologies` e JSON Schema. Numa sonda com 5 perguntas, nenhuma seção de outra stack apareceu (sem filtro, Angular vinha antes de React em "estado React") |
| W1-3 intenção architecture | Feita: o Context Engine compõe identidade, stacks do perfil, avisos e o índice dos padrões da pasta Organização que valem para a stack (id e uma linha, classificação `reference`, data da análise), os da stack antes dos gerais e dentro do orçamento. Exposta na CLI (`context <pasta> --intent architecture`); a ferramenta MCP é o W1-4. Sem perfil, vem tudo, com aviso |
| W1-4 ferramenta de referências na IDE | Feita: `spock_architecture_reference` (só leitura) traz um padrão pelo id com cada coluna da tabela, ou os padrões de uma tecnologia e assunto (palavras do título pesam mais), ou os da stack do projeto; texto mais `structuredContent` com contrato em `contracts/schemas/mcp/`; trilha com os ids entregues. Aparece na tela de Agentes. A busca já aceitava `@reference/` desde o W1-1. **Onda 1 completa** |
| W2-1 frameworks .NET na descoberta | Feita: a descoberta lê os `PackageReference` de cada `.csproj` e do `Directory.Build.props`, com a versão do `Directory.Packages.props` mais próximo (ou `VersionOverride`), e reconhece EF Core, MediatR, FluentValidation, Polly (inclusive pelas extensões de resiliência da Microsoft, sem versão), Serilog, OpenTelemetry, Sentry, MassTransit, Hangfire, AutoMapper, Identity, Swashbuckle, Redis, AWS SDK, SDKs de IA e de MCP e os frameworks de teste. O SDK do projeto dá ASP.NET Core e Worker Service; Blazor vem do SDK WebAssembly ou de componentes `.razor`; MAUI do `UseMaui`. Um registro por framework na raiz da stack. Conferido em EMP-BE e NET-INT: os dois listam os frameworks conhecidos, e o mediador próprio de NET-INT não vira MediatR |
| W2-2 análise de arquitetura determinística | Feita: `mariliz analyze <pasta> [--json]` (só leitura, sem modelo) descreve o que existe. .NET: grafo de `ProjectReference`, camada pelo nome do projeto ou pela pasta, violação de direção (domínio e compartilhado no centro, depois aplicação, infraestrutura e host), framework no domínio e pacote de infraestrutura na aplicação (pelo catálogo do W2-1), projetos sem teste que os referencie. React: pastas de primeiro nível, features, import pelo interior de outra feature (a fronteira pública `index`/`service`/`form`/`domain` vira só aresta do grafo), `shared` importando feature, lint de camadas e features sem teste. Geral: arquivos acima de 400 linhas, sem gerados, testes, `.gitignore` e pastas ocultas. O relatório entra no índice como `@analysis/` (visão geral, projetos .NET, frontend, tamanho e testes), sem data no texto para o hash só mudar com o código; a busca da IDE o entrega como "Análise de arquitetura deste projeto" e os agentes o leem como fato, não regra. Conferido: NET-DOC aponta Application → Infrastructure; EMP-BE, Identity no Domain (e dois imports pelo interior de outra feature em EMP-FE); o Spock, o SDK do MCP na Application |
| W2-3 evals de referência e arquitetura | Feita: o eval de referências roda no índice real, com o filtro de stack, sobre uma cópia dos documentos `@reference/` que o motor gera (um teste .NET falha se ela envelhecer); 20 perguntas por stack com os `REF-*` esperados. Primeira medição: recall@3 = 1,0, MRR = 0,975, contaminação 0 (sem o filtro, 0,25). O eval de arquitetura mede a análise do W2-2 contra um gabarito de 6 casos que reproduzem NET-DOC, EMP-BE, EMP-FE, NET-INT e o Spock sem o código deles: 6 de 6 violações e nenhum falso positivo. Os dois viram gate nos testes e gravam o histórico por data em `intelligence/evaluations/history.jsonl`. Limite: as perguntas foram escritas por quem conhece o catálogo |
| W3-1 regras por framework geradas do catálogo | Feita: o Harness grava uma regra de padrões por framework (`spock-patterns-react.md`, `-angular`, `-dotnet`, `-fullstack`), gerada das tabelas da pasta Organização: um padrão por linha, com id, a data da análise e a indicação de `spock_architecture_reference` para o detalhe. Os `paths` são os das pastas da stack (fullstack vale sempre). React recebe o aviso do `@mariliz/ui-kit` quando o projeto o usa (a descoberta passou a reconhecê-lo); .NET só recebe a §6.9 de integração quando há pacote de resiliência, mensageria ou SDK de nuvem; fullstack exige frontend e backend, e Node só conta como backend com Express, Fastify ou NestJS. A regra da linguagem (`stack-javascript`, `stack-dotnet`) continua. A validação exige a regra que o plano gera, e cada uma pode ser excluída no `spock.yaml`; o Harness Engineer não as propõe, para não mudar o schema da proposta já instalado nos projetos. Projeto Angular: regra Angular e nenhuma regra React |
| W3-3 versão e proveniência por componente | Feita: cada regra de padrões registra no lock a fonte, a data da análise e os padrões usados, com 8 caracteres do hash do texto de cada um (`reference:` no componente; os outros componentes não ganham o campo, e um lock antigo continua válido). O plano compara a regra que vai gravar com a registrada e diz o que mudou (data, padrões novos, removidos, com texto novo). O estado do Harness compara o registro com o catálogo do motor e marca a regra como "Referência desatualizada" mesmo com o arquivo intacto; a tela mostra o que mudou e pede para configurar de novo. Data nova no catálogo marca as regras derivadas |
| W3-4 convenção do projeto existente | Feita: o Harness grava `spock-project-conventions.md`, gerada da análise de arquitetura e marcada `project-convention`: camadas .NET por projeto e a direção das referências, frameworks em uso por projeto, implementações próprias, estrutura do frontend e até 10 divergências já conhecidas (não repetir, nem corrigir de carona). Vale antes dos padrões da pasta Organização. A análise passou a reconhecer implementação própria por projeto ou pasta com código (mediador, mensageria, resiliência), só quando o pacote equivalente não está instalado. Sem tamanho de arquivo nem contagem de testes, para a regra só mudar quando a organização muda. Conferido: EMP-BE registra o mediador de `Domain/Libs/Mediator` com "do not add MediatR"; NET-INT, o mediador e a mensageria de `BuildingBlocks` |
| W3-2 skills curtas por stack | Feita: o Harness grava uma skill por stack (`.claude/skills/spock-stack-react`, `-angular`, `-dotnet`) com o "Não copiar" da seção da stack no catálogo da pasta Organização (problema e, quando a tabela tem, o que fazer no lugar) e um checklist de conclusão escrito à mão; aponta para a regra de padrões, as convenções do projeto e `spock_architecture_reference`. Cada uma fica abaixo de 4.000 caracteres. O Eval novo `EV-SKILL-002` (não obrigatório) reprova qualquer skill do projeto acima de 8.000 caracteres, gerenciada ou escrita à mão, em `.claude/skills` e `.agents/skills`. A validação exige a skill que o plano gera. **Onda 3 completa** |
| C3 Cursor e Codex (roadmap do hub) | Feita (#139-#142) |
| Demais | Não iniciadas |

## 1. Sequência

```text
Onda 0  Base confiável         W0-1 contexto do RAG íntegro (R3) · W0-2 fonte única de tecnologias (R1) · W0-3 testes estáveis (R10)
   │
Onda 1  Conhecimento           W1-1 pasta Organização indexada como @reference/ (R2) · W1-2 filtros e frontmatter (R3)
   │                           W1-3 intenção "architecture" no Context Engine · W1-4 ferramenta MCP de referência
   │
Onda 2  Compreensão            W2-1 frameworks .NET na descoberta (R1) · W2-2 análise de arquitetura determinística (R4)
   │                           W2-3 evals de referência e arquitetura (R8) · W2-4 validação com modelo real (H0-7)
   │
Onda 3  Harness específico     W3-1 regras por framework geradas do catálogo (R5) · W3-2 skills curtas por stack
   │                           W3-3 versão e proveniência por componente · (C3 Cursor/Codex segue no roadmap do hub)
   │
Onda 4  Workflows              W4-1 architecture.assessment (R7) · W4-2 modernization.plan · W4-3 project.design
   │
Contínuo  MCP v2 (R6, F3) · telemetria (R10) · cliente MCP só leitura quando houver necessidade (R9)
```

A ordem segue as dependências: sem contexto íntegro (W0-1) o catálogo não ajuda; sem fonte única de tecnologias
(W0-2) cada referência vira quatro edições; sem análise do que existe (W2-2) não há modernização sem reescrita.

## 2. Entregas

Formato: problema, entrega, depende, esforço, critério de aceite. Esforço: B (dias), M (uma a duas semanas), A (mais).

### Onda 0: base confiável

| Id | Problema | Entrega | Depende | Esforço | Aceite |
| --- | --- | --- | --- | --- | --- |
| W0-1 | Agente vê ~160 caracteres por trecho | Corpo do trecho (até 1.200) no prompt; snippet só para a UI; aviso de corte por arquivo | — | B | Teste que falha hoje: o bloco de contexto do `planning` contém o trecho inteiro; `project_recall` sem regressão |
| W0-2 | Ids de stack em 4 lugares; catálogo de componentes em 3 | `TechnologyCatalog` como registro único; catálogo de componentes numa fonte | — | M | Adicionar uma stack fictícia em teste exige uma única edição |
| W0-3 | 3 falhas .NET e 17 da UI intermitentes; CI sem orçamento | Nomes das falhas (TRX, várias rodadas); `testTimeout` das páginas; verificação local documentada | — | B | Três rodadas seguidas sem falha nas duas suítes |

### Onda 1: conhecimento

| Id | Problema | Entrega | Depende | Esforço | Aceite |
| --- | --- | --- | --- | --- | --- |
| W1-1 | Não há referências técnicas | A pasta Organização (`docs/organizacao/`, Markdown com frontmatter) indexada como `@reference/`, separada do índice de cada projeto | W0-2 | M | Busca por "estado React" num projeto React devolve REF-R4 com a fonte e a data; nenhum trecho cita organização, cliente ou fornecedor |
| W1-2 | Sem filtro nem metadado | Frontmatter lido; `filters` em `ai.knowledge.search.v1` (com JSON Schema); perfil de stacks como filtro padrão | W0-1 | M | Projeto React nunca recebe trecho de §5 (Angular) no dataset de contaminação |
| W1-3 | Contexto inicial sem referências | Intenção `architecture` no Context Engine com a lista de padrões aplicáveis (ids e uma linha) | W1-1 | B | Pacote dentro do orçamento; cada item com classificação (`reference`, `project-convention`...) |
| W1-4 | Agente na IDE sem acesso às referências | `spock_architecture_reference(technology, topic)` com `structuredContent`; `search_project` aceitando `@reference/` | W1-1 | B | Teste pelo fio com `outputSchema` validado contra `contracts/schemas/mcp/` |

### Onda 2: compreensão

| Id | Problema | Entrega | Depende | Esforço | Aceite |
| --- | --- | --- | --- | --- | --- |
| W2-1 | .NET sem frameworks na descoberta | EF Core, MediatR, FluentValidation, Polly, Serilog, OpenTelemetry, Blazor, MAUI por `PackageReference` | W0-2 | B | Descoberta em EMP-BE e NET-INT lista os frameworks conhecidos |
| W2-2 | Arquitetura existente desconhecida | Analisador determinístico .NET e React (grafo de dependências, violações de direção, arquivos grandes, áreas sem teste) com relatório `@analysis/` (D4 do hub) | W0-2, W2-1 | A | Em NET-DOC aponta Application → Infrastructure; em EMP-BE aponta Domain com pacote de framework; no Spock, a Application com SDK MCP |
| W2-3 | Nada mede a qualidade das referências | Datasets de recall e contaminação de referências; dataset de arquitetura com gabarito das referências; histórico de métricas | W1-2, W2-2 | M | Métricas gravadas por data; meta inicial: recall@3 ≥ 0,8 e contaminação 0 |
| W2-4 | Nada rodou com modelo real | `scripts/live-validation.py` com orçamento pequeno (H0-7) | Chave do dono | B | Relatório com fluxos, tokens, latência e falhas |

### Onda 3: Harness específico

| Id | Problema | Entrega | Depende | Esforço | Aceite |
| --- | --- | --- | --- | --- | --- |
| W3-1 | React e Angular recebem a mesma regra | Regras React, Angular, .NET e fullstack geradas das seções do catálogo; `paths` por stack | W1-1, W0-2 | M | Plano do Harness num projeto Angular contém a regra Angular e nenhuma regra React |
| W3-2 | Uma skill genérica | Skills curtas por stack (anti-padrões + checklist), limite de tamanho verificado | W3-1 | M | Check novo no `EvalRunner` reprova skill acima do limite |
| W3-3 | Sem versão por componente | Versão e `REF-*` + data no lock; aviso quando a referência muda | W3-1 | B | Mudar a data de uma seção do pacote marca as regras derivadas como desatualizadas |
| W3-4 | Projeto existente recebe referência no lugar da convenção | Regra de arquitetura gerada do relatório W2-2, marcada `project-convention` | W2-2, W3-1 | M | Em EMP-BE, a regra registra o mediator próprio como convenção, sem propor MediatR |

### Onda 4: workflows

| Id | Problema | Entrega | Depende | Esforço | Aceite |
| --- | --- | --- | --- | --- | --- |
| W4-1 | Sem avaliação de arquitetura | `architecture.assessment` (só leitura): R4 → referências → arquiteto com ferramentas de leitura → juiz | W2-2, W1-4, W2-4 | A | Relatório classifica divergências em risco/custo/estilo e cita arquivos e `REF-*`; juiz aprova em ≥ 80% do dataset. **Modo Sessão feito (MM-5):** o agente da pessoa avalia com `spock_architecture_assessment_prepare` e grava `.spock/architecture-assessment.json`; `spock_architecture_assessment_check` e `project.architecture` conferem sem modelo (arquivo, `REF-*` e achado citados existem; toda divergência classificada e apoiada), com um dataset de 13 avaliações erradas nos testes; tela Engenharia > Visão geral > Arquitetura. Decisão do dono (2026-11-24): no modo Sessão a conferência determinística vale no lugar do juiz; o juiz com modelo entra com o modo Chave, depois do W2-4 |
| W4-2 | Sem modernização | `modernization.plan`: caracterização, passos reversíveis, aprovação por passo, decisão registrada | W4-1 | A | Nenhum plano propõe reescrita sem custo, risco e plano de convivência. **Modo Sessão feito (MM-5):** o agente da pessoa propõe com `spock_modernization_plan_prepare` e grava `.spock/modernization-plan.json`; `spock_modernization_plan_check` e `modernization.plan` conferem sem modelo (17 verificações: caracterização com teste, passo que se desfaz e se verifica, divergência D1, D2... da avaliação, reescrita só declarada e com custo, risco e convivência, passo incremental que fala em reescrever é recusado), com um dataset de 18 planos errados nos testes; a pessoa aprova ou recusa cada passo em Arquitetura (`modernization.decide`), na ordem das dependências, e a decisão vai para a memória de decisões; passo alterado depois da decisão volta a pedir aprovação; as aprovações ficam na pasta de dados do Spock, fora do alcance do agente |
| W4-3 | Criação sem critério de stack | `project.design` sobre a jornada "Da ideia ao projeto": requisitos não funcionais, decisão de stack pelos critérios do catálogo §10, trade-offs explícitos | W1-3, W2-4 | M | Para requisitos iguais, a escolha é estável; cada escolha cita o critério. **Critério nos dois modos e modo Sessão feito (MM-5):** as tabelas do §10 viram critérios com id derivado (`10.4-conhecimento-da-equipe`); a arquitetura da jornada passa a ter fatores (fato do projeto + critério + opção favorecida) e cada escolha de stack cita a pergunta do §10 (backend 10.4, frontend 10.1) e os fatores, ou os requisitos, com alternativa descartada e trade-off; o motor recalcula pelos fatores a opção favorecida e recusa escolha diferente sem exceção escrita (mesmos fatores, mesma escolha); a especificação exige requisito não funcional. No modo Sessão, o agente grava a jornada inteira em `.spock/project-design.json` com `spock_project_design_prepare`, confere com `spock_project_design_check` e a pessoa aprova etapa por etapa e grava os documentos em Da ideia ao projeto (`project.design`). A estabilidade com modelo real (várias rodadas) espera o W2-4 |

### Contínuo

| Id | Entrega | Quando |
| --- | --- | --- |
| C-1 | MCP v2 (R6): `structuredContent` em todas, códigos de erro, prefixo `spock_`, cursor, auditoria completa, `iac_validate` honesto (F3 e MCP-3/4/5 do hub) | Em paralelo à Onda 1 |
| C-2 | Telemetria mínima (R10) | Junto da Onda 2, antes dos workflows |
| C-3 | Cliente MCP só leitura (R9) | Só quando houver fonte sem conector nativo; decisão do dono |
| C-4 | Revisão do catálogo (§12 do catálogo) | A cada mudança relevante nas referências, ou a cada 6 meses |
| C-5 | Atualizar documentos desatualizados ([estado atual §9](SPOCK_CURRENT_STATE.md#9-documentos-desatualizados-encontrados)) | No primeiro PR da Onda 0 |

## 3. Maior valor por esforço

1. **W0-1** (dias): todo agente passa a ver o conteúdo que já é recuperado.
2. **W1-1 + W1-2**: a pasta Organização passa a ser consultada seletivamente.
3. **C-1** (parte de segurança): `iac_validate` deixa de declarar o que não é.
4. **W3-1**: o Harness deixa de ser genérico para React, Angular e .NET.
5. **W2-2 → W4-1**: análise de projeto existente com evidência, base da modernização sem reescrita.

## 4. Decisões que dependem do dono

| Decisão | Opções | Recomendação |
| --- | --- | --- |
| Onde vive o conhecimento extraído de outros projetos | — | **Decidido em 2026-11-18:** na pasta [Organização](../organizacao/README.md) (`docs/organizacao/`), sem mencionar nenhuma organização; fontes privadas por id neutro |
| Ordem entre Harness específico (Onda 3) e workflows (Onda 4) | Harness antes · workflows antes | Harness antes: entrega valor diário na IDE sem depender de modelo |
| Cliente MCP | Agora · quando houver necessidade | Quando houver necessidade (os conectores nativos cobrem Jira, GitHub, AWS) |
| Renomear ferramentas MCP com prefixo `spock_` | Com alias por uma versão · sem alias | Com alias |
| Chave de modelo para H0-7 e W2-4 | — | Necessária antes da Onda 4 |
