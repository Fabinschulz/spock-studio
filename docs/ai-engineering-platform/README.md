# Plataforma de engenharia com IA

Estudo de 2026-11-17 sobre a branch `docs/c2-homologacao-cenario-1` @ `cd0103c`. Valida os MCPs disponíveis (só
leitura), extrai referências arquiteturais de oito repositórios e diagnostica o que falta ao Spock para atuar como
plataforma de engenharia de software assistida por IA, em projetos existentes e novos.

Nenhum serviço externo foi alterado. Nenhum código do Spock foi alterado. Os repositórios de referência foram lidos sem
compilar nem executar nada; os testes do Spock rodaram localmente, sem rede.

## Documentos

| Documento | Conteúdo |
| --- | --- |
| [MCP_QUALITY_ASSESSMENT.md](MCP_QUALITY_ASSESSMENT.md) | Inventário dos 24 servidores MCP, leituras reais, qualidade de contrato, segurança, erros e uso por agentes, incluindo o MCP do próprio Spock |
| [ARCHITECTURE_REFERENCE_CATALOG.md](../organizacao/ARCHITECTURE_REFERENCE_CATALOG.md) | **Base permanente**, na pasta [Organização](../organizacao/README.md): padrões React, `@mariliz/ui-kit`, Angular, .NET, outras stacks, monorepo e Harness, com ids `REF-*`, e diretrizes de decisão |
| [SPOCK_CURRENT_STATE.md](SPOCK_CURRENT_STATE.md) | Estrutura, capacidades verificadas, MCP, conectores, testes e documentos desatualizados |
| [SPOCK_AI_ENGINEERING_CAPABILITIES.md](SPOCK_AI_ENGINEERING_CAPABILITIES.md) | Agentes, skills, rules, hooks, RAG, evals, memória, Harness por stack e disponibilização de conhecimento |
| [SPOCK_ARCHITECTURE_RECOMMENDATIONS.md](SPOCK_ARCHITECTURE_RECOMMENDATIONS.md) | R1 a R10, com justificativa, impacto, dependências e trade-offs |
| [SPOCK_EVOLUTION_ROADMAP.md](SPOCK_EVOLUTION_ROADMAP.md) | Ondas 0 a 4 com critérios de aceite e decisões do dono |

## Relação com os estudos anteriores

- [engineering-intelligence-hub](../engineering-intelligence-hub/README.md) (2026-11-16): Context Engine, MCP v2,
  Harness multi-IDE, conectores. Este estudo não o substitui; acrescenta referências arquiteturais, análise de projeto
  existente e Harness por framework, e aponta onde ele ficou desatualizado.
- Auditoria de produto de 2026-11-15 (removida do repositório em 2026-11-24; está no histórico do git). Os itens AI-09 e AI-18 foram revistos
  aqui com o código atual.

## Síntese

**O que funciona.** Jira (leitura), Datadog, Postman, Miro e demais conectores claude.ai respondem a leituras reais;
GitHub por `gh`. No Spock: governança de escrita (plano, lock, aprovação, reversão), IaC, conectores de leitura,
escopo e redação do MCP, aprovação humana nos workflows. Testes: MCP 100/100, Python 197/197, .NET 1.598 aprovados na
segunda rodada.

**O que não funciona.** AWS MCP sem autenticação (`run_script`) e `aws-mcp` local fechando a conexão; Confluence e
Compass inexistentes no site Atlassian; nove plugins aguardando login. No Spock: o agente recebe ~160 caracteres por
trecho do RAG; recall@5 de 0,25; nada rodou com modelo real; testes intermitentes sem CI.

**MCPs que precisam de melhoria.** Atlassian (JQL inválido devolve vazio, payload inchado, escopo de escrita), AWS
(leitura e escrita na mesma ferramenta, versão não fixada), Postman e Miro (contratos inconsistentes), e o do próprio
Spock (1 de 12 ferramentas tipada, `iac_validate` declarando só leitura).

**Padrões que devem orientar o Spock.** Fronteira verificada por teste ou lint; contrato validado na fronteira; um
formato de erro; efeitos depois do commit; configuração que falha no boot; idempotência em camadas; migração fora da
réplica; decisões registradas; YAGNI por custo assimétrico; skill curta separada de agentes agnósticos.

**Capacidades de IA que faltam.** Referências técnicas; análise da arquitetura existente; Harness por framework;
recuperação seletiva por tecnologia; agentes por papel e com ferramentas; workflows de avaliação, modernização e
criação; cliente MCP; validação com modelo real; telemetria.

**Prioridades.** Contexto do RAG íntegro (W0-1) → fonte única de tecnologias (W0-2) → catálogo recuperável com filtros
(Onda 1) → análise de arquitetura (W2-2) → Harness por framework (Onda 3) → workflows (Onda 4).

## Fontes privadas

Três das referências são repositórios privados. Elas aparecem no catálogo só por id neutro (`ANG-CRM`, `NET-INT`,
`NET-DOC`), estrutura e caminho, sem nome de organização, cliente, fornecedor, host, segredo ou regra de negócio; as
regras estão no [README da Organização](../organizacao/README.md).
