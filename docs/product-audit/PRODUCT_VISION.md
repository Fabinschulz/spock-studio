# Visão consolidada do SPOCK

> Parte da auditoria estratégica do SPOCK (2026-11-15). Navegação e método em [README.md](README.md).
> Este documento reúne a intenção do produto a partir das fontes. Não descreve o que está implementado; isso está em
> [REQUIREMENTS_TRACEABILITY.md](REQUIREMENTS_TRACEABILITY.md).

## 1. Identidade

**SPOCK: System of Programming, Organization, Code and Knowledge.** Plataforma desktop de engenharia de software apoiada
por IA. Não é editor, gerador de código nem interface de chat para modelos de linguagem.

O nome mudou ao longo do caminho (Mariliz AI Engineering em 25/10, Mariliz Studio em 27/10, Spock Studio em 11/11, SPOCK nos
pedidos do dono desde 15/11; ver [HISTORICAL_EVOLUTION.md](HISTORICAL_EVOLUTION.md) §D). A essência declarada pelo dono não
mudou: "A mudança de nome não representa uma mudança na essência do produto" (pedido de 2026-11-16).

Regras de identidade vigentes, por decisão do dono em 2026-11-15:
- o significado oficial único da sigla é o acima;
- o nome, a identidade visual e a comunicação não se associam ao personagem nem à franquia de ficção de mesmo nome;
- a identidade não muda sem aprovação do dono; a registrabilidade da marca aguarda parecer (D-12).

**Ponto aberto:** o produto se apresenta como "Spock Studio" (`desktop/host/src-tauri/tauri.conf.json:3`), e nenhuma tela
mostra "SPOCK" nem a expansão da sigla ([UX_AND_PRODUCT_AUDIT.md](UX_AND_PRODUCT_AUDIT.md) §4.1). Qual é o nome canônico
exibido é uma decisão que ainda não foi registrada.

## 2. Problema de origem

O registro mais antigo é `docs/product-discovery.md` (discovery de 2026-10-25, commit `a488ee9`). Ele descreve o problema
como fato observado nos projetos do dono:
- os Harnesses de engenharia com IA (agentes, skills, regras, hooks, evals) "continuam evoluindo por cópia", sem núcleo comum;
- a orquestração é "prosa seguida pela sessão do Claude Code", sem motor de execução, estado persistido, idempotência nem
  cancelamento;
- os gates de entrega (Jira, PR, sem atribuição de IA) são reforçados por instrução, não por código.

O briefing original e uma auditoria de 2026-10-22 citados no discovery não estão no repositório.

## 3. Propósito

Um produto que **entende o projeto antes de agir, organiza o processo de engenharia e impõe política por código**, para que
pessoas e agentes de IA entreguem com controle de escopo, qualidade, rastreabilidade e custo.

Princípios fundadores, todos do discovery e mantidos até hoje como invariantes:
1. "O motor é a autoridade." Permissões, aprovação, idempotência e validação são impostas pelo motor, nunca pela UI nem por
   instrução a um agente (`product-discovery.md:228`).
2. "Projeto do cliente é a fonte da verdade" (`:229`). Configuração versionada no próprio projeto (`mariliz.yaml`,
   `mariliz.lock`, `.mariliz/`), sem estado oculto.
3. "Regras só no .NET" (`:227`). A IA propõe; o motor decide; a pessoa aprova o plano exato
   (`docs/plataforma/SPOCK_PRODUCT_EVOLUTION.md:38`).
4. Local-first: segredos no cofre do sistema operacional, nunca no projeto; sem telemetria; sem servidor do SPOCK.

## 4. Públicos

Do blueprint de experiência (`docs/blueprints/experience-blueprint.md:11-14`), ampliado em 11/11 com o ambiente de Gestão:
- gestor ou PO: requisitos em backlog e roadmap revisáveis; indicadores e previsões determinísticos;
- arquiteto ou tech lead: fixar padrões e garantir que agentes os respeitem;
- desenvolvedor: ter o Harness funcionando no repositório sem montá-lo à mão;
- equipe de modernização: entender o legado antes de reescrever;
- e, por pedido de 14/11, "todos os públicos, de seniores a leigos", na linguagem da interface.

## 5. Pilares de capacidade

O prompt desta auditoria define três domínios; as fontes históricas mostram um quarto pilar (Gestão) aprovado em 11/11.

### 5.1 Software Engineering
Criar e analisar projetos (novos e legados), reconhecer stack, arquitetura e convenções, planejar e executar
funcionalidades, corrigir bugs, analisar impacto, testar, revisar, integrar com Git e pull requests, produzir evidência e
documentação, e modernizar sistemas. Jornadas originais: Configurar projeto, Importar requisitos, Conectar ferramentas,
Modernizar sistema (`experience-blueprint.md:190-193`).

### 5.2 AI Engineering
Uma camada de engenharia de IA, não só um cliente de LLM: orquestração de agentes especializados, planejamento e execução,
seleção de modelo por tarefa sem dependência de fornecedor, skills, MCP, plugins, workflows, engenharia de contexto,
memória, RAG com fontes rastreáveis, avaliação, observabilidade, controle de custo, tratamento de falhas, idempotência,
prevenção de alucinação e execução controlada de ferramentas.

### 5.3 Infrastructure Management
Conectores AWS, Azure e GCP; descoberta e consulta de recursos com escopo; IaC com OpenTofu como padrão e Terraform opcional
(D-01); ferramentas baixadas de fontes oficiais com verificação (D-03); geração, validação, plano, aprovação humana ligada
ao plano, aplicação, verificação, state, drift, custo e auditoria; credenciais só como referência.

### 5.4 Gestão (Engineering Management)
Aprovada em 11/11: ambientes Engenharia e Gestão, Integration Hub com Jira e GitHub, portfólio, indicadores calculados sem
modelo, previsão com semente, riscos por regra e assistente cujas afirmações citam fatos (`docs/plataforma/evolucao-engineering-management.md`).

## 6. Como o produto deve trabalhar (o ciclo)

O prompt desta auditoria descreve o ciclo de Harness Engineering desejado: demanda, contexto, requisitos, classificação,
planejamento, decomposição, critérios de aceite, delegação, implementação, testes, revisão, evidências, documentação,
atualização de estado e homologação, ligado a Jira e GitHub. O requisito mais sensível é **detectar o que uma entrega feita
por agentes deixou de fora**: acelerar não pode significar perder controle do escopo.

A pergunta central: o SPOCK coordena um processo de engenharia, ou dispara agentes e espera código? A visão exige o
primeiro: controle centralizado, rastreabilidade, validação e responsabilidade sobre as entregas.

## 7. Autonomia controlada

- Operações tipadas e contratadas em vez de shell arbitrário para dar capacidades aos agentes.
- Operações internas do SPOCK separadas de operações externas (Jira, GitHub, nuvens).
- Escrita externa só com contrato, permissão e aprovação explícitos.
- Conexão existente não concede acesso automático a todos os projetos e agentes.

A fronteira mudou de "o SPOCK não executa comandos" (implícito no início) para "executa só com autorização explícita e
verificação" (terminal da pessoa, CLIs de IA em modo leitura, `tofu`/`terraform` com aprovação ligada ao plano). Essa
mudança não tem registro formal; ver [DECISION_SUPERSESSION_REGISTER.md](DECISION_SUPERSESSION_REGISTER.md), S-06.

## 8. Não-objetivos e limites declarados

Do discovery e dos documentos de produto: sem aplicativo web, sem conta ou login nesta etapa, sem telemetria, sem
"memória aprendida", sem indicador por pessoa, sem macOS na primeira versão, sem editor livre de agentes, e sem dados
fictícios ou atividade de IA simulada. Os não-objetivos do MVP (integrações, RAG, modernização, evals com LLM) foram
superados de forma deliberada pelas fases posteriores.

## 9. Tensões de visão sem reconciliação

| Tema | Fonte A | Fonte B | Estado |
| --- | --- | --- | --- |
| Automação | "O foco do Spock é ser um Ecosistema de automação empresarial" (pedido de 14/11) | "O Spock não é plataforma de automação" (`SPOCK_PRODUCT_EVOLUTION.md:21,322`, 12/11) | I: pendente |
| Identidade visual | Pedido de 11/11 de ícone com referência ao personagem | Proibição de associação em 15/11; ícone e textos ainda descrevem a origem | I: pendente com D-12 |
| Nome exibido | "SPOCK" nos pedidos recentes | "Spock Studio" no produto | I: pendente |
| Terraform | "liberado, sem trava" (`docs/compliance/LICENSE_DECISIONS.md:60`) | D-02 "aguarda aprovação" (prompt desta auditoria) | I: pendente |
| Critério de continuidade | Um usuário externo antes das Fases 3+ (`implementation-roadmap.md:219`) | Produto avançou muito além sem registro | F: abandonado em silêncio |
