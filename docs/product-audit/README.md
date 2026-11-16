# Auditoria estratégica do SPOCK

**SPOCK: System of Programming, Organization, Code and Knowledge.** Auditoria de evolução, arquitetura e fidelidade ao
produto, de Mariliz a SPOCK. Data: 2026-11-15. Branch observada: `feat/conformidade-licencas` (HEAD `06443a5`).

## Navegação

| Documento | Conteúdo |
|---|---|
| [PRODUCT_VISION.md](PRODUCT_VISION.md) | Visão consolidada, pilares, princípios e tensões de visão |
| [HISTORICAL_EVOLUTION.md](HISTORICAL_EVOLUTION.md) | Linha do tempo, catálogo de 118 decisões, propostas e explorações (H-01 a H-118), renome |
| [REQUIREMENTS_TRACEABILITY.md](REQUIREMENTS_TRACEABILITY.md) | Matriz consolidada visão, decisão e implementação (RT-01 a RT-91) |
| [ARCHITECTURE_AUDIT.md](ARCHITECTURE_AUDIT.md) | Arquitetura real, camadas, segurança local, testes, code smells |
| [AI_ENGINEERING_AUDIT.md](AI_ENGINEERING_AUDIT.md) | Agentes, orquestração, modelos, skills, MCP, plugins, RAG, avaliação (AI-01 a AI-51) |
| [SOFTWARE_ENGINEERING_AUDIT.md](SOFTWARE_ENGINEERING_AUDIT.md) | Capacidades de engenharia, ciclo do Harness, stacks (SE, HC, ST) |
| [INTEGRATIONS_AUDIT.md](INTEGRATIONS_AUDIT.md) | Jira, GitHub, Git, IDE, CLIs de IA, permissões (INT-01 a INT-25) |
| [INFRASTRUCTURE_MANAGEMENT_AUDIT.md](INFRASTRUCTURE_MANAGEMENT_AUDIT.md) | Conectores cloud, IaC, OpenTofu e Terraform, D-01 a D-12 (INF, LIC) |
| [UX_AND_PRODUCT_AUDIT.md](UX_AND_PRODUCT_AUDIT.md) | Navegação, telas, identidade, posicionamento (UX-01 a UX-33) |
| [DECISION_SUPERSESSION_REGISTER.md](DECISION_SUPERSESSION_REGISTER.md) | Substituições S-01 a S-22, com análise detalhada das relevantes |
| [GAP_ANALYSIS.md](GAP_ANALYSIS.md) | Lacunas, regressões, conflitos e pendências |
| [RISK_REGISTER.md](RISK_REGISTER.md) | Riscos R-01 a R-26 |
| [RECOMMENDED_ROADMAP.md](RECOMMENDED_ROADMAP.md) | Iniciativas P0 a P3 com critérios de aceite |

## Método e limites

- **Somente leitura.** Nenhum código, dependência, configuração ou recurso remoto foi alterado. Nenhuma chamada a Jira,
  GitHub, AWS, Azure ou GCP. Os únicos arquivos criados são os desta pasta.
- **Fontes:** código-fonte; 378 commits e PRs #1 a #108; documentos de `docs/`; 419 mensagens escritas pelo dono em 35
  sessões anteriores do Claude Code (29/10 a 16/11), usadas como registro de intenção. Esse histórico fica fora do
  repositório; as citações aparecem como `P <data UTC> [sessão]`.
- **Lacunas de fonte:** o briefing original e uma auditoria de 22/10 citados no discovery não estão disponíveis; não há
  pedido registrado para as etapas de 26 a 28/10 e de 31/10 (separação do Python, RAG lexical, chave do modelo, CPython).
  As referências externas (repositórios de exemplo em React, Angular e .NET) não foram consultadas nesta auditoria.
- **Testes executados (2026-11-15, Windows, local):** .NET 1.467 aprovados, 25 ignorados (exigem credenciais), 0 falhas;
  Rust 45 de 45; Python 192 de 192 numa execução isolada (1 falha intermitente sob carga); UI com 8 estouros de prazo sob
  carga, que passaram 98 de 98 quando rodados sozinhos. Recall do RAG medido no próprio repositório: recall@5 0,25.
  Lint, typecheck, build e empacotamento não foram executados.
- **Conferência própria:** os achados de maior gravidade foram conferidos diretamente no código pelo auditor principal
  (AssumeRole, edição padrão do build, tetos de tokens, ambiente dos processos, terminal no host, associação do ícone).
- **Regra de classificação:** documentação é evidência de intenção; código é evidência de implementação; teste é
  evidência de comportamento. "Validado" exige exercício contra o ambiente real.

## Relatório executivo

### A. Fidelidade à visão original

O SPOCK preserva a espinha da visão de 25/10: o motor é a autoridade, o projeto do cliente é a fonte da verdade, a política
é imposta por código, a configuração do Harness é aplicada com diff, aprovação, idempotência e reversão, e nada sensível
sai da máquina sem decisão explícita. **O renome de Mariliz para Spock não causou perda de funcionalidade.**

O produto ampliou a visão de forma deliberada: Gestão (portfólio, indicadores determinísticos, assistente verificado),
Integration Hub com Jira e GitHub, servidor MCP para IDEs, conectores cloud e IaC completa com OpenTofu.

O que se perdeu ou nunca chegou: os agentes especializados de implementação e teste do discovery; a jornada de
modernização; o reconhecimento da arquitetura existente; e, sobretudo, a passagem do plano para a entrega verificada.
**Na pergunta central, o SPOCK hoje planeja sob governança e entrega a execução para fora; ele não coordena o processo de
engenharia.**

### B. Maturidade atual

| Nível | Exemplos |
|---|---|
| Capacidade validada em condições reais | Jornada Configurar com Harness; descoberta de projetos; conector AWS (SSO e `aws login`); GitHub para gestão; servidor MCP com IDE |
| Implementação funcional, validada só com dublês ou recursos locais | Workflows LangGraph, juiz, investigação, Ideias e geração de IaC (sem modelo real); apply, drift e state (só recursos locais); Azure e GCP |
| Implementação parcial | Seleção de modelo, skills, inventário MCP, RAG (recall baixo), integrações de engenharia, suporte por stack |
| Só documentação | Clean Architecture, DDD e SOLID como regras-texto; revisão e testes delegados ao agente instalado; modernização |
| Ausente | Delegação com retorno, detecção de pendências, classificação da demanda, plugins, onboarding, atualização automática, EULA |

### C. Evolução arquitetural

Substituições que são melhorias válidas e devem ser mantidas: Python e LangGraph com o .NET como autoridade (S-01);
transporte local com stdio e HTTP em loopback com token (S-02); SQLite FTS5 no lugar de Chroma (S-03); modelo de
implementação próprio no lugar de metodologias de terceiros na UI (S-04); provedores múltiplos por finalidade (S-05);
Workbench no lugar de interface de conversa (S-08); OpenTofu como padrão (S-10); chave no cofre do SO (S-13).

Substituições que precisam ser revistas: a fronteira de execução deslocada sem ADR (S-06, S-15); os agentes de profile
abandonados sem registro (S-07); o ambiente dos processos filhos, que regrediu (S-16); os quatro caminhos de IA sem
governança comum (S-17).

### D. Problemas encontrados

1. **Segurança:** a IaC ignora o papel em conexões AWS "assumir papel" e executa com a identidade do perfil de origem
   (R-01); variáveis de ambiente com segredos chegam a ferramentas externas (R-10); o MCP sem projeto expõe todos (R-11).
2. **Pendências tratadas como aprovadas:** D-02 (build padrão libera o Terraform gerenciado), D-05 (registrada como
   adotada), D-10 (política que não bloquearia pendências).
3. **Identidade:** nenhuma tela mostra "SPOCK"; o ícone e três textos versionados descrevem a origem da marca no
   personagem de ficção, contra a regra de 15/11.
4. **Coerência interna:** tetos de tokens divergentes entre tela e Python (R-05); 16 conflitos entre documentos e código
   (GAP §5); visão de automação contraditória.
5. **Validação:** nenhum fluxo com modelo rodou com chave real; CI sem orçamento, sem gate efetivo.

### E. Qualidade das decisões

A arquitetura é coerente, testada e sustentável no recorte que cobre: dependências .NET corretas e sem ciclos, sem
classes gigantes, contratos protegidos por cerca de 200 fixtures em três linguagens, governança de ações por hash, defesa
contra injeção de prompt e saída de modelo validada por contrato. Três runtimes são justificáveis enquanto LangGraph for
requisito e o .NET concentrar o domínio. O ponto fraco não é a pilha; é a falta de decisões registradas nas fronteiras
(execução, IA) e a distância entre o que a camada de IA promete e o que entrega.

### F. Melhor caminho

- **Manter:** a autoridade do motor, a pilha atual, FTS5 como base do RAG, OpenTofu como padrão, o Workbench, a
  separação de conexão, vínculo, recorte e política.
- **Revisar:** a fronteira de execução e a de IA (ADRs), a política de modelos por etapa, os documentos de decisão.
- **Recuperar:** agentes especializados (como decisão explícita), reconhecimento da arquitetura existente, jornada de
  modernização (ou retirada honesta da promessa).
- **Vale o investimento:** o elo de entrega (pacote de tarefa, leitura tipada do resultado, verificação de critérios e
  detecção de pendências). É a capacidade que transforma o SPOCK de planejador governado em coordenador de engenharia.
- **Não vale agora:** trocar de tecnologia de RAG antes de medir alternativas baratas; reescrever o .NET ou o Python;
  plugins antes de decidir se são requisito.

### G. Próximas etapas

Sequência detalhada em [RECOMMENDED_ROADMAP.md](RECOMMENDED_ROADMAP.md):
1. P0-1 AssumeRole e P0-2 teto de tokens (correções pequenas, alto valor).
2. P0-3 decisões do dono sobre D-02, D-05, ícone e nome exibido.
3. P0-4 validação real com modelo, junto com P1-1 (ADRs de execução e de IA).
4. P1-3 Git e GitHub tipados, P1-4 demanda estruturada e P1-5 arquitetura existente.
5. P1-2 elo de entrega com detecção de pendências.
