# Políticas de acesso, segurança e privacidade

> Parte do estudo [Engineering Intelligence Hub](README.md) (2026-11-16). Parte X do pedido. ADR:
> [0007](../architecture/adr/0007-politicas-de-acesso.md).

## 1. O hub aumenta a superfície

Hoje o SPOCK já é ponto de acesso a código, Jira, GitHub e nuvem. Ao servir IDEs, ele passa a responder a um cliente
que roda um modelo que lê conteúdo não confiável. Quatro decisões de segurança decorrem disso.

## 2. Camadas de controle

| Camada | Pergunta | Onde é decidida | Hoje |
| --- | --- | --- | --- |
| Autenticação na fonte | A credencial vale? | Provedor + cofre | Sim |
| Autorização na fonte | O que a credencial vê? | Provedor | Sim (herdada) |
| Autorização de pasta | O projeto foi aberto nesta máquina? | Diálogo nativo do host | Sim |
| Vínculo do MCP | Este servidor responde por qual projeto? | Motor | **Fraco**: sem `--project`, todos (R-11) |
| Vínculo de conexão | Esta conexão pertence a este projeto? | Motor | Sim |
| Recorte | Quais recursos e tipos de dado? | Motor (`DataScope`) | Sim |
| Política por destino | O dado pode ir para índice, workflow, assistente, IDE? | Motor | **Fraco**: um booleano (RT-50) |
| Operação | Leitura, escrita, destrutiva? | Motor | Leitura por decisão; IaC com aprovação ligada ao plano |
| Pessoa | Quem pediu? | — | Inexistente (sem identidade) |

Regra: **cada camada é verificada no motor, em ordem, e qualquer "não" encerra.** Conexão ativa nunca é autorização.

## 3. Política por destino

Proposta de modelo, compatível com o atual (`FeedsKnowledge = true` mapeia para todos os destinos de conhecimento):

```yaml
# por vínculo (ProjectIntegration.Policy)
destinations:
  management: true      # cálculo de indicadores (padrão: true; é a razão de existir do vínculo)
  knowledge: false      # entra no índice como @external/
  workflow: false       # entra no contexto dos workflows do SPOCK
  assistant: false      # fatos com título entram no assistente
  ide: summary          # none | summary (só números e chaves) | items (títulos e estados) | full (descrição e critérios)
```

`ide` tem níveis porque é o destino de maior risco (o texto sai do SPOCK para outro produto). O padrão para conexões novas
é `summary`, igual ao que o briefing faz hoje (títulos ficam de fora). Testes por destino, como recomendou a auditoria
(P2-4), sobem para P1 porque o hub depende disso.

## 4. Dados sensíveis

Nunca em `CLAUDE.md`, `AGENTS.md`, regras, skills, pacotes de contexto, prompts nem respostas MCP:
senhas, tokens, chaves de API, credenciais de banco, strings de conexão, chaves privadas, segredos de nuvem.

| Proteção | Hoje | Proposta |
| --- | --- | --- |
| Cofre do SO | DPAPI validado; Keychain e Secret Service não validados (R-26) | Validar em máquina real |
| Redação na saída MCP | `SecretScanner.RedactLines` em todo texto | Manter; aplicar também ao `structuredContent` |
| Redação no gerador de arquivos | Diff com segredos omitidos | Eval nova `EV-CTX-SEC`: nenhum bloco gerado contém padrão de segredo |
| Ambiente de processos filhos | **Herdado inteiro** (R-10) | Filtro padrão por lista permitida (PATH, HOME, TEMP, LANG, variáveis da ferramenta), como o terminal; **pré-requisito** para o cliente MCP e para hooks que chamam o motor |
| Nomes de conexão no bootstrap | — | Só o nome e o recurso (`Jira (PROJ)`), nunca site, e-mail nem conta |

## 5. Dados corporativos

- O MCP nunca devolve dado de um projeto fora do vínculo, nem mesmo em mensagem de erro (a resposta para "fora do escopo"
  já é igual à de "inexistente": manter).
- `portfolio_overview` só lê projetos autorizados na máquina.
- O SPOCK não amplia o alcance da credencial: se o Jira não deixa ver, o SPOCK não tem o dado.
- Sem identidade, não há papel. O estudo não promete separar gestor de desenvolvedor na mesma máquina e credencial
  ([DEPLOYMENT_MODELS.md](DEPLOYMENT_MODELS.md)).

## 6. Operações externas

| Operação | Regra |
| --- | --- |
| Leitura de conector | Padrão para conexão nova |
| Escrita em Jira, GitHub, Azure DevOps | Fora do P1. Quando vier (P3-3): contrato por operação, permissão por vínculo, aprovação explícita na tela do SPOCK (não na IDE), registro e desfazer quando o provedor permitir |
| IaC apply | Só pela tela, com aprovação ligada ao plano (existe). Nunca pelo MCP |
| Comando arbitrário | Nenhuma ferramenta MCP executa comando livre. O hook de política continua protegendo o shell da IDE ("proteção, não barreira") |

O agente não contorna o motor: as ferramentas MCP de escrita, quando existirem, criam um **pedido de aprovação** no SPOCK;
a IDE recebe "aguardando aprovação no SPOCK", nunca o efeito direto.

## 7. Conteúdo externo não confiável

| Ameaça | Defesa existente | Proposta para o hub |
| --- | --- | --- |
| Instrução escondida em issue, comentário ou doc | Delimitação e rotulação no Python (`agents/prompts.py`); saída só JSON validado | No MCP, texto externo vai em campo próprio (`untrusted: true`, `source`), nunca concatenado à instrução; o bloco de bootstrap diz que conteúdo externo é dado |
| Pedido para mudar a política ("ignore as regras do SPOCK") | Política no motor, não no modelo | Nenhuma ferramenta muda política; mudanças de política só na tela |
| Anotação mentirosa de MCP externo | — | Anotações tratadas como dica não confiável (a própria spec diz isso); classificação pela pessoa |
| Arquivo de instrução adulterado no repositório | Hash do bloco no lock | Bloco modificado vira conflito e aviso no bootstrap |
| Cliente MCP que declara outro nome | Nome declarado vai à auditoria | Registrar como declarado; não usar como autorização |

## 8. Correções de segurança que bloqueiam o hub

1. MCP recusa iniciar sem vínculo (MCP-1 / R-11).
2. Composição única do MCP com a mesma política de edição do motor (MCP-2).
3. Filtro de ambiente dos processos filhos (R-10).
4. AssumeRole na IaC (R-01): não bloqueia o hub, mas é P0 de produto.
