# Visão de produto: SPOCK como hub de inteligência de projetos

> Parte do estudo [Engineering Intelligence Hub](README.md) (2026-11-16). Proposta para decisão do dono; nada aqui foi
> implementado. A visão consolidada até 15/11 estava na auditoria estratégica daquela data (removida do repositório em 2026-11-24; está no histórico do git);
> este documento descreve o que muda com a nova direção e o que continua valendo.

## 1. Tese

**O SPOCK é a fonte de inteligência, contexto e governança dos projetos de uma organização, seja qual for a IDE ou o
agente usado para trabalhar neles.**

Hoje o SPOCK já entende o projeto (perfil, Harness, conhecimento indexado, integrações, gestão calculada) e já fala com a
IDE por um servidor MCP local. A nova direção não pede outra plataforma: pede que esse conhecimento **saia do aplicativo
e chegue, com escopo e proveniência, a qualquer ferramenta que a pessoa já usa**, e que volte de lá como evidência de
entrega.

Três movimentos, nesta ordem:

1. **Contexto confiável na IDE.** Toda sessão nova de Claude Code, Cursor ou Codex reconhece o SPOCK, identifica o
   projeto certo e sabe onde buscar o que muda.
2. **Gestão na IDE.** A pergunta "como está o projeto?" feita na IDE recebe os mesmos números, calculados pelas mesmas
   regras, que o gestor vê na tela de Gestão.
3. **Entrega verificada.** O que a IDE produziu volta ao SPOCK como diff, testes e critérios conferidos, e o estado
   chega à Gestão.

## 2. Duas experiências, um núcleo

| Experiência | Para quem | O que é | Onde já existe |
| --- | --- | --- | --- |
| **Plataforma própria** (desktop) | Quem configura, administra e acompanha | Configurar o projeto, o Harness, as conexões, a política; acompanhar execuções, gestão e infraestrutura | Workbench com Engenharia, Gestão, Integrações, Cloud, Execuções, Auditoria |
| **Plataforma integrada** (MCP + arquivos de instrução) | Quem trabalha na IDE: desenvolvedor, arquiteto, e o gestor que prefere perguntar no agente | O SPOCK como servidor de contexto e de capacidades; a IDE como interface de conversa e de execução | Servidor MCP por stdio com briefing, busca, decisões, cloud e IaC |

As duas leem o mesmo núcleo: o mesmo projeto (id), o mesmo perfil, o mesmo índice, os mesmos dados sincronizados, os
mesmos indicadores do motor. **Não existe uma base de Gestão e outra de Engenharia.** O que muda entre elas é a forma de
apresentar e o recorte autorizado.

## 3. Públicos e o que cada um recebe

| Público | Na IDE (via SPOCK) | No desktop |
| --- | --- | --- |
| Desenvolvedor | Perfil técnico, regras, decisões, trechos do código com origem, critérios de aceite do item em que trabalha, o que falta para entregar | Configurar o Harness, ver execuções e evidências |
| Arquiteto / tech lead | Arquitetura reconhecida, convenções, divergências entre regra e código | Fixar regras, aprovar o Harness, revisar decisões |
| Gestor, PO, PM | Status, indicadores, riscos, bloqueios, marcos, com fonte e data | Portfólio, previsão, relatórios, decisões gerenciais |
| Plataforma / infraestrutura | Conexões cloud liberadas, estado da IaC, último plano | Conexões cloud, IaC com aprovação ligada ao plano |

Um indicador por pessoa continua fora (decisão registrada em
[../plataforma/SPOCK_PRODUCT_EVOLUTION.md](../plataforma/SPOCK_PRODUCT_EVOLUTION.md) §9). Capacidade de execução por
equipe só entra com fonte confiável e decisão do dono.

## 4. Posicionamento: o SPOCK não substitui a IDE

| Responsabilidade | SPOCK | IDE externa |
| --- | --- | --- |
| Conhecer arquitetura, regras, decisões | Autoridade do conhecimento, com proveniência | Consome por instrução persistente e MCP |
| Harness (regras, agentes, skills, hooks) | Modelo canônico, projeção por IDE, diff, aprovação, reversão | Carrega e aplica no trabalho |
| MCP | Servidor e, depois, gateway de conectores | Cliente |
| Jira, GitHub, Azure DevOps | Conecta, sincroniza, contextualiza, calcula | Consulta o que foi autorizado |
| RAG | Indexa e recupera com origem e data | Recebe trechos |
| Agentes e workflows | Workflows próprios (planejamento, juiz, investigação), contratos de tarefa | Agentes nativos executam o código |
| Gestão | Consolida e calcula, de forma determinística | Interface de pergunta |
| Infraestrutura | Conecta, planeja, aplica só com aprovação | Consulta estado |
| Escrever código | Verifica e governa | Ambiente principal de implementação |

O SPOCK não reconstrói o que as IDEs fazem bem (editar, executar, conversar, subagentes). O diferencial é o que nenhuma
IDE tem: **conhecimento transversal do projeto e da organização, persistido fora da conversa, com governança e
proveniência**.

## 5. O que continua valendo (invariantes)

Estes princípios do discovery e das fases seguintes não mudam com a nova direção. Onde a direção nova os pressiona, o
estudo diz como reconciliar.

| Invariante | Pressão da nova direção | Reconciliação proposta |
| --- | --- | --- |
| O motor é a autoridade | IDEs externas viram o principal consumidor | Toda resposta ao MCP sai do motor; a IDE nunca recebe credencial nem decide permissão |
| Projeto do cliente é a fonte da verdade da configuração | Arquivos de várias IDEs no repositório | Modelo canônico no `mariliz.yaml`/`.mariliz/`; arquivos de IDE são **projeções** geradas, com bloco gerenciado e lock |
| Local-first, sem telemetria, sem servidor do SPOCK | Gestão empresarial com vários gestores | Modelo A (local por pessoa) como base; B e C só com decisão do dono ([DEPLOYMENT_MODELS.md](DEPLOYMENT_MODELS.md)) |
| Conectar não é autorizar | O SPOCK como gateway de tudo | Catálogo central com alcance, vínculo, recorte e política por destino; o MCP expõe só o autorizado |
| Não usar IA onde há regra | Perguntas de gestão na IDE | Os números vêm de ferramentas determinísticas; o modelo da IDE só redige, citando |
| Um só modelo para cada conceito | Adaptadores por IDE, conectores novos | Núcleo canônico, adaptadores finos sem regra de negócio |
| Escrita externa só com contrato e aprovação | Pedidos de "atualizar o Jira" pela IDE | Leitura como padrão; escrita é iniciativa P3 separada |

## 6. Tensões com decisões anteriores

| Decisão anterior | Nova direção | Proposta |
| --- | --- | --- |
| "Database Studio fora" (`SPOCK_PRODUCT_EVOLUTION.md:326`) | Fornecer contexto de banco às IDEs | Não conflita se o escopo for **metadados de schema e migrações** (contexto), não cliente de consulta. Consulta a dados exige decisão própria |
| "Catálogo amplo de conectores: dois comprovados antes" (`:329`) | Azure DevOps, bancos, MCP externos | Mantém: Jira precisa ser validado de ponta a ponta antes de Azure DevOps; ver [CONNECTOR_HUB.md](CONNECTOR_HUB.md) |
| "O SPOCK não é plataforma de automação" contra "ecossistema de automação empresarial" (S-19) | "Gestão empresarial como capacidade central" | A direção nova fala de **inteligência** e gestão de portfólio, não de automação de processos. Recomenda-se registrar que automação de processos segue fora |
| Sem login, sem conta, sem multiusuário | Gestor e desenvolvedor com acessos diferentes | Sem identidade, o recorte é por projeto autorizado na máquina. Papéis exigem decisão do modelo de distribuição |
| Ambientes de IA: só Claude Code recebe Harness (Cursor e Codex bloqueiam o plano) | Multi-IDE | Adaptadores por IDE sobre o modelo canônico ([CONTEXT_PERSISTENCE_AND_ADAPTERS.md](CONTEXT_PERSISTENCE_AND_ADAPTERS.md)) |

## 7. O que o produto promete e o que não promete

Promete, depois das entregas P1:
- toda sessão nova na IDE carrega a instrução persistente do SPOCK e sabe como pedir o contexto vivo;
- onde a IDE tiver hook de sessão ou de prompt, o SPOCK injeta um resumo compacto e atual de forma determinística;
- os dados de gestão vistos na IDE são os mesmos da tela, com fonte, período e data da última sincronização;
- sem vínculo de projeto, o MCP não responde sobre projeto nenhum.

Não promete:
- que a IDE chame uma ferramenta MCP em todo prompt: isso depende do modelo e do cliente, e só é garantido onde existe
  hook ([IDE_COMPATIBILITY_MATRIX.md](IDE_COMPATIBILITY_MATRIX.md));
- números de gestão quando a sincronização falhou ou está velha: o SPOCK diz a data e a limitação;
- permissões mais amplas que as da credencial conectada: o SPOCK nunca contorna o Jira, o GitHub ou a nuvem.
