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

Hoje agentes e pessoas a leem diretamente. A recuperação seletiva por tecnologia, tipo de projeto e responsabilidade é a
Onda 1 do [roadmap](../ai-engineering-platform/SPOCK_EVOLUTION_ROADMAP.md): esta pasta passa a ser indexada como
`@reference/`, separada do conhecimento de cada projeto, e cada trecho chega ao agente marcado como referência, não
como convenção do projeto analisado.
