# Organização

Conhecimento que a organização consolida a partir de outros projetos para orientar o Spock: referências arquiteturais,
padrões que valem repetir, o que não copiar e critérios de decisão. É diferente do resto de `docs/`, que descreve o
próprio Spock. O Spock consulta esta pasta primeiro e só volta às fontes quando precisa conferir uma atualização ou
cobrir uma dúvida que ela não responde.

## O que fica aqui

| Arquivo | Conteúdo |
| --- | --- |
| [ARCHITECTURE_REFERENCE_CATALOG.md](ARCHITECTURE_REFERENCE_CATALOG.md) | Padrões de React, `@mariliz/ui-kit`, Angular, .NET, outras stacks, monorepo e Harness de IA, com ids `REF-*` e diretrizes de decisão |

## Regras

1. **Nenhum nome de organização, cliente, fornecedor, pessoa, host, URL interna, chave de projeto ou segredo.** Uma
   fonte privada entra com um id neutro (tecnologia e papel, como `NET-INT`) e uma descrição genérica
   ("Repositório privado: integrador .NET multi-tenant"). Nomes de sistema de terceiros viram marcadores
   (`<Fornecedor>`, `<ERP>`), inclusive em caminhos de arquivo.
2. **Fonte pública** entra com nome e URL.
3. **Toda fonte** registra branch, commit e data da análise, para saber quando reler.
4. **Conhecimento, não cópia.** Cada padrão diz o que é, onde foi visto, o problema que resolve, quando usar, quando
   evitar e como adaptar. Código só em trecho curto e estrutural.
5. **Ids estáveis.** Um `REF-*` nunca é reaproveitado; padrão abandonado fica marcado como retirado, com o motivo.
6. **Quando um arquivo crescer demais**, ele se divide por assunto (por exemplo, um por tecnologia), mantendo os ids.

## Como acrescentar ou atualizar uma referência

1. Leia a fonte sem executar nada dela e anote branch, commit e data.
2. Escreva os padrões no catálogo (ou num arquivo novo desta pasta), seguindo as regras acima.
3. Atualize a tabela de fontes (§1 do catálogo) e o histórico no fim dele.
4. Antes do commit, procure por nomes que não deveriam estar aqui: a busca por nome de organização, cliente e
   fornecedor precisa voltar vazia.

## Como o Spock usa esta pasta

O motor embute os arquivos desta pasta (menos este README) e cada seção `## ` vira um documento `@reference/` no
índice de todo projeto. A busca dos agentes e a ferramenta `search_project` da IDE trazem essas seções junto com os
arquivos do projeto, e cada trecho chega marcado como referência da Organização, com o título e a data da análise: é
argumento de engenharia, nunca convenção do projeto analisado. Um arquivo do projeto chamado `@reference/...` é
bloqueado, para ninguém se passar por referência.

Os padrões das tabelas (as linhas `| REF-… |`) também têm caminhos próprios:

- a intenção `architecture` do Context Engine entrega ao agente a lista dos padrões que valem para a stack do projeto, um
  por linha;
- a ferramenta `spock_architecture_reference` da IDE traz um padrão inteiro pelo id (`REF-R4`), com cada coluna da
  tabela, ou os padrões de uma tecnologia e de um assunto.

Por isso, cada tabela de padrões precisa de cabeçalho: o nome de cada coluna ("Onde", "Evitar quando") vira o rótulo do
detalhe.

Por isso, todo arquivo desta pasta começa com um frontmatter com `titulo` e `analisadoEm`; sem ele, o arquivo não entra
no índice:

```yaml
---
titulo: Catálogo de referências arquiteturais
analisadoEm: 2026-11-17
---
```

A linha `Escopo:` no começo de cada seção diz a que tecnologias ela vale, e a busca filtra por isso: um projeto React não
recebe a seção Angular. Escreva os ids das stacks entre crases, no trecho que começa com "tecnologia" ou "tecnologias",
com os mesmos ids que o Spock detecta (`react`, `nextjs`, `angular`, `dotnet`, `node`, `python`, `java`, `go`...):

```text
Escopo: tecnologias `react`, `nextjs` · tipo `frontend` · responsabilidade interface.
```

Uma seção sem esse trecho, ou com "qualquer tecnologia", vale para todos os projetos. Projeto sem stack detectada recebe
todas as seções.

Mudou um arquivo daqui? A versão nova do motor leva o texto novo, e o índice de cada projeto se atualiza na próxima
execução, pelo hash.

O eval de referências (`intelligence/evaluations/references.py`) mede se a busca acha o padrão certo para cada stack sem
trazer seção de outra. Ele roda sobre uma cópia dos documentos que o motor gera: depois de mudar esta pasta, regenere a cópia
(`MARILIZ_UPDATE_GOLDEN=1`, teste `ReferenceCorpusTests`) e confira o eval; um padrão novo que valha perguntar entra no
dataset `intelligence/evaluations/datasets/references.json`.
