# SPOCK como Engineering Intelligence Hub

**Estudo de arquitetura e estratégia de produto.** Data: 2026-11-16. Branch observada: `master`, HEAD `bea3302`, árvore
limpa no início. Base: a auditoria estratégica de 15/11 (removida do repositório em 2026-11-24; está no histórico do git), reavaliada
contra o código atual.

## Navegação

| Documento | Conteúdo | Equivale no pedido a |
| --- | --- | --- |
| [PRODUCT_VISION.md](PRODUCT_VISION.md) | Tese, duas experiências, públicos, posicionamento contra as IDEs, invariantes e tensões | SPOCK_PRODUCT_VISION, ENGINEERING_INTELLIGENCE_HUB |
| [CURRENT_STATE_REASSESSMENT.md](CURRENT_STATE_REASSESSMENT.md) | Achados da auditoria hoje; inventário reutilizar, ampliar, corrigir | §18 |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Diagrama atual e proposto; fluxos de dados, autenticação, autorização, contexto, gestão | §21.1 |
| [IDE_COMPATIBILITY_MATRIX.md](IDE_COMPATIBILITY_MATRIX.md) | Claude Code, Cursor, Codex, Gemini CLI, Copilot: versões, mecanismos, fontes e datas | IDE_COMPATIBILITY_MATRIX |
| [CONTEXT_ENGINE.md](CONTEXT_ENGINE.md) | Modelo canônico, classificação da origem, composição por intenção, frescor, RAG | Partes I e VI, CONTEXT_RETRIEVAL_STRATEGY |
| [CONTEXT_PERSISTENCE_AND_ADAPTERS.md](CONTEXT_PERSISTENCE_AND_ADAPTERS.md) | Bloco de bootstrap, vínculo do projeto, adaptadores, blocos gerenciados, sincronização, ciclo de vida | Partes II e VIII, CONTEXT_CONFIGURATION_STRATEGY |
| [MCP_CAPABILITY_CATALOG.md](MCP_CAPABILITY_CATALOG.md) | Ferramentas existentes e problemas; catálogo recomendado; resources e prompts; contrato | Parte VII, MCP_CAPABILITY_CATALOG |
| [CONNECTOR_HUB.md](CONNECTOR_HUB.md) | Inventário de conectores, catálogo central, gateway, Jira e Azure DevOps, bancos, cliente MCP | Parte III, CONNECTOR_HUB_ARCHITECTURE |
| [ENTERPRISE_INTELLIGENCE.md](ENTERPRISE_INTELLIGENCE.md) | Modelo de gestão, catálogo de métricas, assistente na tela e na IDE, permissões | Partes IV e IX, MANAGEMENT_* |
| [CANONICAL_HARNESS_MODEL.md](CANONICAL_HARNESS_MODEL.md) | Harness independente de IDE, projeções, agentes e orquestração | Parte V |
| [DELIVERY_VERIFICATION.md](DELIVERY_VERIFICATION.md) | Pacote de tarefa, verificador, pendências, estado para a Gestão | DELIVERY_VERIFICATION |
| [ACCESS_POLICIES_AND_SECURITY.md](ACCESS_POLICIES_AND_SECURITY.md) | Camadas de controle, política por destino, segredos, conteúdo não confiável | Parte X |
| [DEPLOYMENT_MODELS.md](DEPLOYMENT_MODELS.md) | Modelos A, B e C comparados; recomendação | Parte XI |
| [QUALITY_AND_OBSERVABILITY.md](QUALITY_AND_OBSERVABILITY.md) | Diagnóstico da integração, avaliação, testes negativos | Parte XII |
| [ROADMAP.md](ROADMAP.md) | Iniciativas P0 a P3, reconciliação com a auditoria, 12 cenários de aceite | Partes XV e XVI |

ADRs propostas (status "proposta", aguardam o dono): [0002 Context Engine](../architecture/adr/0002-context-engine.md),
[0003 Persistência de contexto](../architecture/adr/0003-persistencia-de-contexto.md),
[0004 Adaptadores multi-IDE](../architecture/adr/0004-adaptadores-multi-ide.md),
[0005 MCP e Connector Hub](../architecture/adr/0005-mcp-connector-hub.md),
[0006 Contexto de gestão](../architecture/adr/0006-contexto-de-gestao.md),
[0007 Políticas de acesso](../architecture/adr/0007-politicas-de-acesso.md),
[0008 Modelo de distribuição](../architecture/adr/0008-modelo-de-distribuicao.md).

A estrutura sugerida no pedido (`docs/product`, `docs/integrations`, `docs/management`…) foi condensada numa pasta de
estudo, como as auditorias anteriores, para não criar documentos repetidos; as ADRs ficam em `docs/architecture/adr/`,
onde já está a 0001.

## Método e limites

- **Sem implementação.** Nenhum código, dependência, configuração de IDE, recurso remoto, Jira, GitHub ou nuvem foi
  alterado. Só os arquivos desta pasta e as ADRs 0002 a 0008 foram criados, sem commit.
- **Código:** dois inventários de leitura (MCP, Harness e contexto; gestão, conectores, RAG e os P0), com os achados mais
  graves conferidos diretamente: escopo do MCP (`McpProjectResolver.cs:9`), composição do MCP sem política de edição
  (`McpEntry.cs:52-53` com `IacRuntimeService.cs:49`), teto de tokens (`engineering/graph.py:80`,
  `investigation/graph.py:82`), AssumeRole (`IacOperationService.cs:492`) e ambiente dos filhos (`SystemProcessRunner.cs:28`).
- **IDEs:** documentação oficial consultada em 2026-11-16, com versões e fontes na matriz. A versão atual da especificação
  MCP (2026-07-28) foi conferida diretamente.
- **Não executado:** testes, lint, build, qualquer IDE. Nada com modelo real. Telas não abertas.
- **Não verificado:** suporte a `structuredContent` em Claude Code, Codex, Gemini e Copilot; prompts MCP no Codex;
  algumas páginas do Copilot e do Codex só pelo resumo da busca; latência real de um hook que inicia o motor .NET.

## Conclusão executiva

### As 14 perguntas

**1. O SPOCK já tem os elementos fundamentais para ser um Engineering Intelligence Hub?**
Tem a metade difícil: conhecimento do projeto com proveniência, governança de escrita (plano, diff, aprovação, lock,
reversão), gestão calculada com fonte e data, conectores com conexão, vínculo, recorte e política separados, cofre, e um
servidor MCP funcionando. Falta a metade de integração: identidade estável do projeto, contexto composto com orçamento,
saída estruturada, adaptadores para mais de uma IDE e verificação de entrega.

**2. O que está implementado e pode ser reaproveitado?**
O mecanismo de plano do Harness (é a base da sincronização de arquivos de IDE), o servidor MCP, o índice FTS5, o
`ManagementService` com `MetricSource`, o `HarnessInventoryReader` (já lê `AGENTS.md`, `.agents/skills` e o MCP do
Codex), a política de contextualização, o cofre, a auditoria e o broker de modelo. Detalhe em
[CURRENT_STATE_REASSESSMENT.md](CURRENT_STATE_REASSESSMENT.md) §2.

**3. O que precisa ser corrigido antes da expansão?**
MCP sem vínculo expõe todos os projetos (R-11); a composição do MCP diverge do motor, inclusive sem a política de edição
(MCP-2, achado novo); teto de tokens cortado em silêncio no Python (metade corrigida); ambiente herdado pelos processos
filhos (R-10); AssumeRole ignorado na IaC (R-01). Nenhum P0 da auditoria foi fechado desde 15/11.

**4. Como garantir que cada IDE reconheça o projeto e use informação atual?**
Três peças: um bloco gerenciado de bootstrap no arquivo de instrução que cada IDE carrega (`CLAUDE.md`, `AGENTS.md`,
`.cursor/rules`), o MCP do SPOCK configurado no projeto com vínculo obrigatório pelo id estável, e um hook de início de
sessão que pede ao motor um resumo atual. O dado atual vem sempre da ferramenta MCP, nunca do arquivo.

**5. Como fazer o contexto persistir entre sessões e prompts?**
Entre sessões: o arquivo de instrução e o hook de sessão (as cinco IDEs têm). Entre prompts: hook por prompt leve onde
existe (Claude Code, Codex, Gemini CLI); no Cursor e no Copilot, a instrução e as descrições das ferramentas, com a
limitação dita. O estado do projeto fica no SPOCK, não na conversa. Não existe garantia de chamada MCP em todo prompt em
nenhuma IDE, e o produto não deve prometer isso.

**6. Como integrar MCPs e conectores sem duplicar cadastros?**
O catálogo central que já existe (conexão, vínculo, recorte, política) é a única fonte; a IDE se liga só ao SPOCK.
MCPs de terceiros entram depois pelo cliente MCP do SPOCK, com cada ferramenta classificada e permitida por projeto, e
expostas como ferramentas do SPOCK. Nada de proxy de `tools/list`.

**7. Como fornecer dados de gestão diretamente nas IDEs?**
Ferramentas MCP de gestão (`project_status`, `project_work_items`, `project_risks`, `project_milestones`,
`project_changes`, `work_item_context`) sobre o `ManagementService` atual, com números prontos, id da métrica, fórmula
pelo catálogo, fonte, período e data da sincronização. O modelo da IDE redige; o motor calcula.

**8. Como compartilhar o conhecimento entre Gestão e Engenharia?**
Não há duas bases. Itens, execuções, decisões e entregas verificadas são um só registro; as duas experiências são
projeções. A verificação de entrega é a ponte: o que a IDE entregou vira evidência e pendência que a Gestão lê.

**9. Como evitar contexto desatualizado?**
Nada dinâmico em arquivo; `freshness` e `observedAt` em todo item; prazo de frescor por tipo de fonte; `contextVersion`
para os arquivos projetados com plano de sincronização; dado de sincronização falha sempre com a data da última completa.

**10. Como garantir isolamento entre projetos e organizações?**
Vínculo obrigatório e falha segura no MCP; id estável mais pasta autorizada; política por destino; resposta igual para
"fora do escopo" e "inexistente"; credenciais pessoais que herdam o recorte da fonte. Entre organizações, o Modelo A já
isola por máquina; o Modelo C mantém isso.

**11. Como evoluir sem depender de uma IDE específica?**
Harness e contexto canônicos no SPOCK; adaptadores finos por IDE como tabelas de projeção; a matriz de capacidades como
dado versionado; MCP como contrato comum; teste de conformidade por adaptador.

**12. Qual é a arquitetura mínima e robusta?**
Motor atual + Context Engine (só compõe) + MCP v2 com vínculo obrigatório e saída estruturada + adaptadores Claude Code,
Cursor e Codex sobre o plano existente com modo bloco + comando `spock context` para hooks. Sem servidor, sem login, sem
runtime novo. Ver [ARCHITECTURE.md](ARCHITECTURE.md) §2.

**13. Qual sequência entrega mais valor?**
H0-1 e H0-2 (vínculo e composição do MCP), H0-3/H0-5; depois F1 (id estável), F2 e F3 (engine e MCP v2) e C2 (Claude
Code); E1 (gestão na IDE com dados que já existem); C3 (Cursor e Codex); D1 a D3 (entrega verificada). Detalhe em
[ROADMAP.md](ROADMAP.md).

**14. Quais decisões ainda dependem do dono?** Tabela abaixo.

### Decisões recomendadas

1. O SPOCK é a fonte de contexto e governança; a IDE é interface e executora. Não reconstruir recursos nativos das IDEs.
2. Contexto persistente por arquivo e hook; contexto vivo só por MCP (ADR-0003).
3. Harness canônico com adaptadores; Claude Code primeiro, Cursor e Codex em seguida (ADR-0004).
4. MCP como gateway de capacidades, nunca proxy; vínculo obrigatório (ADR-0005, ADR-0007).
5. Gestão na IDE com números do motor e catálogo único de métricas (ADR-0006).
6. Manter FTS5 e melhorar recall com medidas baratas antes de embeddings.
7. Modelo A como base; Modelo C desenhado depois; B fora sem decisão explícita (ADR-0008).
8. Verificação de entrega determinística no .NET como ponte entre Engenharia e Gestão.

### Decisões pendentes do dono

| Id | Decisão | Recomendação do estudo |
| --- | --- | --- |
| DD-1 | Modelo de distribuição alvo | A agora, C depois |
| DD-2 | Identidade de pessoa | Nenhuma no P1; decidir com DD-1 |
| DD-3 | Portfólio entre pessoas | Cada um com o seu até o Modelo C |
| DD-4 | Comercialização (individual ou organização) | Junto com D-07 (EULA) |
| DD-5 | Servidor MCP próprio ou SDK oficial C# (spec mudou para 2026-07-28) | Avaliar o SDK com a auditoria de licenças; manter o próprio até lá |
| DD-6 | Lançador `spock` no PATH e versionar os arquivos de IDE no repositório | **Decidida pelo dono em 2026-11-17:** sim para os dois; sem lançador, gravar no escopo local |
| DD-7 | A IDE pode disparar sincronização incremental com teto | Sim, com teto e auditoria |
| DD-8 | Nível padrão do destino `ide` | `summary` |
| DD-9 | Ler comentários do Jira | Opcional, sob política, como dado não confiável |
| DD-10 | Escopo de banco | Níveis 1 e 2; 3 só com decisão; 4 fora |
| — | Pendências anteriores que continuam: D5 (agente próprio de implementação), D3 (embeddings), D-02, D-05, D-07, D-09 a D-12, nome exibido e ícone, automação de processos fora do escopo | Análise de lacunas da auditoria de 15/11, §6 (no histórico do git) |

### Alternativas rejeitadas

| Alternativa | Por quê |
| --- | --- |
| `CLAUDE.md` como fonte da verdade | Prende a uma IDE e congela dado dinâmico |
| Hook por prompt com busca no índice ou modelo | Custo e latência em todo prompt, inclusive nos sem relação |
| Proxy de ferramentas de MCPs externos | Expõe operações não classificadas |
| Reserva "todos os projetos" no MCP | É o vazamento que o pedido proíbe |
| Roots para escolher o projeto | Descontinuado na spec atual |
| Backend hospedado agora | Inverte local-first, sem servidor e sem login |
| Banco vetorial antes de medir | Sem evidência de ganho |
| Agente próprio de implementação agora | A IDE já executa; o diferencial é verificar |
| Gerar todos os formatos para todas as IDEs | Arquivos inúteis e incompatíveis |
| Marketplace de plugins | Sem necessidade comprovada |

### Riscos novos

| Id | Risco | Mitigação |
| --- | --- | --- |
| HR-1 | Formatos e mecanismos das IDEs mudam a cada poucas semanas | Matriz como dado, teste de conformidade por adaptador, revisão a cada entrega |
| HR-2 | Expectativa de "contexto em todo prompt" que nenhuma IDE garante | Limitações por IDE na tela e na documentação |
| HR-3 | Injeção de prompt via issue chega a um agente com ferramentas na IDE | Campo `untrusted`, política `ide: summary` por padrão, nenhuma ferramenta muda política |
| HR-4 | Hook que inicia o motor .NET a cada prompt fica lento | Medir; caminho rápido que lê um resumo pré-calculado; teto de 300 ms com saída silenciosa |
| HR-5 | Aprovação de servidor e projeto confiável impedem a ativação | Explicar no diagnóstico; nunca contornar |
| HR-6 | Expansão de conectores antes de validar os atuais | Regra "comprovar antes de ampliar": Jira de ponta a ponta antes do Azure DevOps |
| HR-7 | Spec MCP mudou para stateless | Compatibilidade com revisões de handshake; plano de versão (DD-5) |

### Benefícios

A IDE passa a trabalhar com o contexto certo do projeto certo em qualquer sessão; o gestor e o desenvolvedor leem os
mesmos números; o SPOCK deixa de depender de uma IDE; e a entrega volta como evidência, que é o que falta para ele
coordenar engenharia em vez de só planejar.

### Dependências externas

Chaves de modelo do dono (validação real); credencial e projeto de teste do Jira; as IDEs instaladas para a homologação
multi-IDE; parecer jurídico para as pendências de licença e marca.

### Próximas entregas recomendadas

1. Aprovar ou ajustar as ADRs 0002 a 0008 e responder DD-1 a DD-10.
2. H0-1 e H0-2 num PR pequeno (vínculo obrigatório e composição única do MCP).
3. H0-3 e H0-5 (teto de tokens e filtro de ambiente).
4. F1 + F2 (bootstrap) + F3 + C2: o primeiro ciclo completo no Claude Code, com homologação dos cenários 1, 2 e 3.
