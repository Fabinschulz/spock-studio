# Compatibilidade multiversão e inteligência de versões

O Spock trabalha com projetos de gerações diferentes: um serviço .NET 6 com React 17, um monorepo com .NET 10 e Python 3.15, um projeto criado hoje. Esta pasta reúne a auditoria de 2026-11-18 e o plano para duas capacidades:

1. **Compatibilidade do projeto.** O Spock reconhece as versões de cada pasta do projeto e trabalha dentro delas.
2. **Inteligência de versões.** O Spock sabe quais versões existem, em que canal estão e até quando têm suporte, a partir de fontes oficiais. O próprio Spock segue essa mesma regra.

| Documento | Conteúdo |
|---|---|
| [DIAGNOSTICO.md](DIAGNOSTICO.md) | Inventário de versões do Spock, comparação com as versões estáveis de hoje, análise do .NET 11, do Microsoft Agent Framework e das demais stacks, riscos e lacunas |
| [MATRIZ.md](MATRIZ.md) | Matriz de compatibilidade: o que a descoberta lê por ecossistema, o que cada classificação significa e o que está validado por teste |
| [POLITICA.md](POLITICA.md) | Política de seleção de versões (projeto existente, projeto novo, modernização) e de atualização das dependências do Spock |
| [ROADMAP.md](ROADMAP.md) | Entregas MV-1 a MV-10, prioridades, dependências e decisões que dependem do dono |

## Regra

- **Projeto existente:** o Spock respeita o ambiente que encontrou.
- **Projeto novo:** o Spock recomenda versões estáveis e atuais, escolhidas em conjunto e não uma por uma.
- **Modernização:** é um fluxo à parte, explícito, com plano, aprovação e validação. Nunca entra junto com uma tarefa comum.
- **O próprio Spock:** as dependências evoluem de forma contínua. Cada atualização é rastreável e precisa passar pelos mesmos testes e pela conformidade de licenças.

Descobrir versões é uma operação diferente de instalar versões. Uma atualização do catálogo nunca roda comando nem muda arquivo no projeto do usuário.

## Arquitetura

A arquitetura reaproveita o que já existe e só acrescenta o que faltava.

```
fontes oficiais ──► scripts/refresh-release-catalog.py ──► Assets/versions/release-catalog.json (embutido, revisado em PR)
                                                                   │
projeto ──► ProjectDiscoverer (stacks, frameworks, runtimes) ──► ProjectModules (fato por pasta, gravado no perfil)
                                                                   │
                                            CompatibilityAssessor (situação calculada na leitura: data + catálogo)
                                                   │                    │                     │
                                       ContextEngine "versions"   engineering.profile    mariliz versions
                                       (o que o agente recebe)    (tela de perfil)       (CLI)
```

| Conceito | Onde mora | Já existia? |
|---|---|---|
| Descoberta tecnológica do projeto | `Discovery/*Detector.cs`, `RuntimeDetector.cs` | Sim. O MV-1 acrescentou o runtime fixado (`.nvmrc`, `.node-version`, `engines.node`, `global.json`) e o alvo por projeto quando a solução mistura versões |
| Perfil do projeto | `Engineering/TechnologyProfile` + `Modules` | Sim. O MV-1 acrescentou o fato por pasta (`ProjectModule`) |
| Catálogo de versões | `Compatibility/ReleaseCatalog` + `Assets/versions/release-catalog.json` | Não: novo |
| Descoberta de versões nas fontes | `scripts/refresh-release-catalog.py` | Não: novo. O OpenTofu já consultava a API oficial (`OpenTofuToolchain`) e serviu de precedente |
| Classificação de compatibilidade | `Compatibility/CompatibilityAssessor` | Não: novo |
| Restrição na execução dos agentes | `Context/ContextEngine`, seção "Restrições de versão" | O motor de contexto existia; a seção é nova |
| Política de versões, scaffolding, modernização | [ROADMAP.md](ROADMAP.md) (MV-4 a MV-7) | Não |

A situação de suporte não é gravada no perfil. Ela é calculada a cada leitura, porque a mesma versão muda de situação quando o suporte acaba ou quando o catálogo é atualizado. O perfil grava só o fato: pasta, tecnologia, versão, origem e evidência.

## Entrega MV-1 (esta branch)

| Item pedido | Estado | Evidência |
|---|---|---|
| Identificar versões dos projetos | Feito, por pasta | `CompatibilityTests`: projeto legado (.NET 6, SDK 6.0.428, React 17, Node 18 pelo `.nvmrc`, Python 3.10) e monorepo com `net6.0`, `net10.0` e `netstandard2.0;net8.0` |
| Perfil de compatibilidade | Feito | `engineering.profile` e `engineering.analyze` devolvem `compatibility`, e a tela mostra "Versões e suporte" |
| Catálogo central de versões | Feito, 11 tecnologias | `release-catalog.json`, gerado por fontes oficiais em 2026-11-18 |
| Descoberta periódica | Parcial | O gerador já tem cache (o último catálogo), timeout, retry com backoff, validação, detecção de regressão e fallback. Ainda falta rodar periodicamente: MV-2 |
| Restrições na execução dos agentes | Feito | O pacote `bootstrap` (hook de início de sessão da IDE) e o pacote `architecture` trazem uma restrição por pasta. Depois da tarefa, a ferramenta MCP `spock_version_check` aponta versão trocada e combinação incompatível (MV-3) |
| Criação de projetos pelo catálogo | Não existe criação de projeto no Spock | MV-6 |
| Atualização automática do Spock | Política escrita; automação no MV-8 | [POLITICA.md](POLITICA.md) |
| Testes de compatibilidade | Feito para descoberta, classificação e contexto | 50 testes no motor, 12 no gerador, 1 na interface |

Para conferir no seu projeto: `mariliz versions <pasta>` (só leitura, sem rede).
