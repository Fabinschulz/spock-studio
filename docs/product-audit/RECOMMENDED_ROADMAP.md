# Roadmap recomendado

> Parte da auditoria estratégica do SPOCK (2026-11-15). Navegação e método em [README.md](README.md).
> Recomendação, não plano aprovado. Nada aqui foi implementado. Cada iniciativa precisa de aprovação do dono, uma por vez,
> como nas fases anteriores.

Ordem de leitura: P0 protege a integridade do produto; P1 fecha a distância entre visão e entrega; P2 melhora a
arquitetura; P3 abre novas possibilidades. Dentro de cada nível, a ordem é a sugerida.

## P0: integridade do produto

### P0-1 AssumeRole na IaC
- **Problema:** com conexão AWS "assumir papel", plan e apply usam a identidade do perfil de origem.
- **Origem:** entrega 7 da IaC (perfis do `aws login`, #108); conectores cloud (#94).
- **Estado atual:** `IacOperationService.cs:449-452` define só `AWS_PROFILE`; `roleArn` é ignorado (R-01).
- **Resultado esperado:** OpenTofu e Terraform executam com o papel configurado.
- **Solução recomendada:** gerar credenciais temporárias pelo STS no motor e passá-las ao processo, ou montar um perfil
  efêmero com `role_arn` e `source_profile`. Recusar a operação se o papel não puder ser assumido.
- **Impacto:** alto. **Complexidade:** baixa a média. **Dependências:** nenhuma. **Riscos:** vazar credencial temporária
  em log (redação já existe).
- **Critérios de aceite:** teste unitário que falha hoje; validação em conta real em que `aws sts get-caller-identity`
  executado pelo processo da IaC devolve o ARN do papel.

### P0-2 Teto de tokens com uma fonte da verdade
- **Problema:** a tela aceita até 64.000 e 120.000 tokens; o Python corta em 20.000 e 40.000.
- **Origem:** teto por finalidade (#89).
- **Estado atual:** H (RT-35, R-05).
- **Resultado esperado:** o teto configurado é o teto aplicado, nas duas pontas.
- **Solução recomendada:** o motor envia o teto no pedido ao Python e o Python usa só esse valor; remover as constantes.
- **Impacto:** médio. **Complexidade:** baixa. **Dependências:** nenhuma. **Riscos:** nenhum relevante.
- **Critérios de aceite:** teste de contrato com teto acima de 20.000 que hoje falha; fixture golden atualizada.

### P0-3 Decisões do dono sobre identidade e licenças pendentes tratadas como aprovadas
- **Problema:** D-02 tratada como liberada no build padrão; D-05 registrada como adotada; ícone e textos associados ao
  personagem; nome exibido diferente do nome oficial.
- **Origem:** decisões D-01 a D-12 (15/11) e regra de identidade de 15/11.
- **Estado atual:** H e I (RT-70, RT-71, RT-86, RT-87).
- **Resultado esperado:** cada pendência aparece como pendente no código e nos docs, ou é decidida pelo dono.
- **Solução recomendada:** apresentar ao dono quatro decisões curtas: (1) D-02 e a edição padrão do CI de release;
  (2) texto correto da D-05; (3) ícone, comentários dos SVG e `evolucao-engineering-management.md:465-466`;
  (4) nome canônico exibido e onde a expansão da sigla aparece. Implementar só depois.
- **Impacto:** alto (jurídico e comercial). **Complexidade:** baixa, exceto o ícone. **Dependências:** parecer jurídico
  para D-02, D-05 e D-12. **Riscos:** mudar identidade sem aprovação, o que é vedado.
- **Critérios de aceite:** `LICENSE_DECISIONS.md` coerente com as decisões; build de release falha sem `SpockEdition`;
  nenhum arquivo versionado descreve a origem da marca no personagem.

### P0-4 Validação real dos fluxos com modelo
- **Problema:** nada que usa modelo foi exercitado com chave válida.
- **Origem:** validação real de 14/11 (bloqueada por falta de chave).
- **Estado atual:** D (RT-38).
- **Resultado esperado:** evidência de execução real de plano, arquitetura, juiz, investigação, Ideias e geração de IaC.
- **Solução recomendada:** usar `scripts/live-validation.py` e os testes `*LiveTests` com orçamento pequeno e credenciais
  só no terminal do dono; registrar resultados e falhas.
- **Impacto:** alto. **Complexidade:** baixa. **Dependências:** chave do dono. **Riscos:** custo pequeno de tokens.
- **Critérios de aceite:** relatório com cada fluxo, tokens gastos, latência e falhas; prazos ajustados se estourarem.

## P1: capacidades essenciais

### P1-1 ADR da fronteira de execução e da fronteira de IA
- **Problema:** o princípio "não executa comandos" mudou sem registro; IA espalhada em quatro caminhos (S-06, S-17).
- **Solução recomendada:** dois ADRs curtos. Execução: o que a pessoa executa (terminal), o que o SPOCK executa com
  aprovação (IaC, instaladores), o que agentes nunca executam. IA: "várias etapas, interrupção ou RAG ficam no Python;
  pergunta e resposta verificada ficam no .NET", com teto comum aplicado no `ModelGateway`.
- **Impacto:** médio. **Complexidade:** baixa. **Dependências:** P0-2.
- **Critérios de aceite:** ADRs aprovados; teste que prova que Ideias, IaC e assistentes respeitam o teto.

### P1-2 Elo de entrega: delegação com contrato e verificação do retorno
- **Problema:** o plano não vira entrega verificada; não há detecção de pendências (RT-24, RT-27).
- **Origem:** visão de Harness Engineering; discovery ("não existe motor de execução, estado persistido").
- **Solução recomendada:** começar sem agente próprio de implementação. (1) O plano gera um pacote de tarefa estruturado
  (objetivo, critérios de aceite, arquivos prováveis, testes esperados). (2) A pessoa ou a CLI executa. (3) O SPOCK lê o
  resultado de forma tipada: diff do Git, `.specs/` e `validation.md` do agente `verifier`, comandos de teste detectados.
  (4) Um verificador determinístico compara critérios contra evidência e lista pendências. Só depois decidir D5.
- **Impacto:** muito alto: é o que transforma planejamento em coordenação. **Complexidade:** média.
  **Dependências:** P1-3 (Git), P1-4 (classificação e critérios).
- **Riscos:** criar um segundo motor; mitigação: o verificador é determinístico e roda no .NET.
- **Critérios de aceite:** em um projeto real, uma entrega com critério não atendido aparece como pendência com a
  evidência que falta; uma entrega completa fecha sem pendência.

### P1-3 Leitura tipada de Git e GitHub para engenharia
- **Problema:** Git só `status`; GitHub sem branches, commits, reviews e checks (RT-11, RT-52).
- **Solução recomendada:** operações de leitura tipadas no motor (diff, log, branch atual; PR com arquivos, reviews e
  checks), nunca shell livre para agentes.
- **Critérios de aceite:** testes com repositório real; ferramentas MCP correspondentes só leitura.

### P1-4 Demanda estruturada: origem, classificação e critérios
- **Problema:** texto livre, sem tipo, sem critérios do Jira (RT-21 a RT-23, RT-51).
- **Solução recomendada:** iniciar o pedido a partir de um item sincronizado; ler `description` e comentários do Jira;
  classificar por regra a partir do `issuetype` e, sem ele, pelo modelo (finalidade `classify`, já no contrato).
- **Critérios de aceite:** pedido com origem gravada; classificação visível; critérios entram no pacote de tarefa (P1-2).

### P1-5 Arquitetura do projeto existente
- **Problema:** o SPOCK não reconhece a arquitetura nem as convenções do código (RT-03).
- **Solução recomendada:** detector determinístico com evidência (camadas por projeto .NET, direção de referências,
  estrutura `features/` no frontend), gravado no perfil tecnológico e usado como regra do Harness.
- **Critérios de aceite:** reconhece corretamente o próprio repositório e duas referências de stacks diferentes.

### P1-6 Segurança de processos e do MCP
- **Problema:** ambiente herdado pelos filhos; MCP sem projeto expõe todos (R-10, R-11).
- **Solução recomendada:** filtro de segredos padrão no `SystemProcessRunner`; MCP recusa iniciar sem projeto.
- **Critérios de aceite:** testes que falham hoje.

### P1-7 Supply chain do instalador e validações de nuvem
- **Problema:** CPython do Windows sem hash (SC-1); Azure e GCP sem validação; apply, drift e state só com recursos locais.
- **Solução recomendada:** fixar hash; validação em contas descartáveis com recursos temporários padronizados.
- **Critérios de aceite:** build falha com hash divergente; relatório de validação de Azure, GCP e apply em nuvem.

### P1-8 Primeiro uso e gestão de projetos
- **Problema:** sem onboarding; não dá para renomear ou remover projetos (RT-89).
- **Critérios de aceite:** checagem de primeiro uso (modelo, ambiente de IA, Git) e ações de projeto testadas.

## P2: evolução arquitetural

| Iniciativa | Problema | Solução | Critério de aceite |
|---|---|---|---|
| P2-1 Atualizar documentos de decisão | Blueprint, ADR-0001, docs de backend e frontend, estado atual e docs de infraestrutura desatualizados (GAP §5) | Marcar o que foi superado com link para o atual; remover as notas repetidas do discovery | Nenhum conflito H aberto da lista do GAP §5 |
| P2-2 Seleção de modelo por etapa | Três finalidades grossas | Finalidades por etapa (plano, classificação, reformulação, julgamento) e metadados de capacidade, custo e latência | Plano e classificação podem usar modelos diferentes |
| P2-3 Qualidade do RAG | Recall 0,25 | Dicionário de termos pt/en, reformulação também no fluxo de engenharia, arquivos longos inteiros; depois decidir D3 com sqlite-vec | Recall@5 medido acima da linha atual em dataset ampliado |
| P2-4 Política de contexto por destino | Um booleano por vínculo | Política separada para índice, workflow, investigação e assistente | Testes por destino |
| P2-5 Contratos | Lista de métodos em três lugares | Teste .NET contra `protocol.rs`; prazos anunciados pelo motor no `hello` | Host sem tabela de prazos própria |
| P2-6 Regras e skills por stack | Regras só .NET e JS genérico | Regras Angular (com `.html`), React, Python; detecção de workspace pnpm/Nx | Perfis gerados para as três stacks de referência |
| P2-7 Base de referências externas | Conhecimento de referência fora do índice | Fonte `@reference/` com Markdown versionado e data de captura | Busca devolve trecho de referência com origem e data |
| P2-8 Retomada após queda | Só a pausa sobrevive | Durabilidade por passo onde o custo for aceitável, ou corrigir a promessa | Teste de queda no meio do grafo |
| P2-9 IaC integrada às execuções | Só geração e bootstrap viram execuções | Validate, plan e apply como execuções do SPOCK | Central de Execuções mostra a operação completa |
| P2-10 Composição única | `Program.cs` e `McpEntry.cs` duplicam a montagem | Fábrica comum de serviços | Um só ponto de composição por contexto |

## P3: evolução de produto

| Iniciativa | Observação |
|---|---|
| P3-1 Posicionamento e landing page | Depende de P0-3 (nome e identidade) |
| P3-2 Modernização de legado | Retomar a jornada original depois de P1-5; até lá, retirar o cartão "Em breve" ou explicar o prazo |
| P3-3 Escrita controlada em Jira e GitHub | Contrato, permissão por operação, aprovação e desfazer; só depois de P1-2 |
| P3-4 Agentes especializados e paralelismo | Depois do elo de entrega; decidir D5 com base em evidência |
| P3-5 Cliente MCP e plugins | Decidir primeiro se são requisitos |
| P3-6 Custo em dinheiro | Tabela de preços por provedor |
| P3-7 Atualização automática e assinatura | Antes de usuários externos |
| P3-8 Automação empresarial (K, L, M) | Depende da decisão de escopo (S-19) |

## Sequência recomendada

1. P0-1 e P0-2 (correções pequenas, alto valor).
2. P0-3 (decisões do dono, sem código até a resposta).
3. P0-4 (validação real), em paralelo com P1-1 (ADRs).
4. P1-3, P1-4 e P1-5, que alimentam P1-2.
5. P1-2 (elo de entrega), a iniciativa de maior valor para a visão.
6. P1-6 a P1-8.
7. P2 conforme a dor real; P3 conforme as decisões de produto.
