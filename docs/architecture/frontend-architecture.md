# Arquitetura do desktop (`desktop/ui`)

A experiência desktop é `desktop/`: `ui` (esta página) e `host` (janela Tauri). O runtime está em `runtime/` e a intelligence em `intelligence/`.

A organização é em camadas `app → features → shared`, uma pasta por feature com responsabilidades separadas e nada de
lógica no JSX. O transporte é o motor .NET via host Tauri, e não HTTP; por isso `shared/api` faz o papel de um cliente
HTTP.

## Linguagem do produto

A interface fala só do **modelo de implementação Mariliz** e da **estrutura de especificação**. Nomes de kits ou
metodologias de terceiros não aparecem como escolha, rótulo ou título; o teste da etapa "Ambiente de IA" falha se
aparecerem. Os ids das etapas são `project`, `model`, `review`, `run` e `result`.

## Áreas

O shell (`src/app/layouts/app-shell`) tem uma barra de áreas fixa (**Início**, **Engenharia**, **Gestão**, **Atividade**,
**Conexões** e **Configurações**) e, ao lado, um painel com as telas da área aberta. As áreas, as telas de cada uma, o
nome apresentado e o nome técnico ficam num só lugar, `navigation.tsx`; a área atual vem da rota (`areaOf`), e a
trilha, as abas de página e a busca (Ctrl K) saem da mesma tabela. O desenho e as decisões estão em
[workspace-unificado.md](../redesign/workspace-unificado.md).

Engenharia e Gestão leem o mesmo projeto selecionado (`features/project-selection`); nenhuma tem cadastro próprio de
projeto. `management` só consome o que `integrations` expõe pelo `index.ts` e não chama métodos de engenharia. Uma tela
que depende do projeto é remontada com `key={projectId}`: dado de um projeto não aparece em outro.

A entrada de pedidos (`features/ask`) só encaminha: o texto segue no estado da navegação (`shared/routing/handoff.ts`)
até a tela que trata aquele tipo de pedido.

## Camadas

| Camada | Pasta | Regra |
| ------ | ----- | ----- |
| Rotas e moldura | `src/app/` | Monta features; não tem regra de negócio. |
| Features | `src/features/<feature>/` | Uma fatia vertical. Entre features, só a API pública (`index.ts`). |
| Compartilhado | `src/shared/` | Não depende de `features` nem de `app`. |

Os limites são executados por lint (`no-restricted-imports` em `eslint.config.js`), não por convenção.

## Dentro de uma feature

```
features/<feature>/
  index.ts          API pública (exports nomeados, sem `export *`)
  <feature>-page.tsx  Orquestra as etapas
  service/          Fronteira com o motor: chamadas, e o contrato validado com Zod
  domain/           Regra pura e testável: estado, reducers, labels, textos
  hooks/            Efeitos e estado que ligam o domínio ao service
  components/       UI da feature (sem regra de negócio)
  steps/<step>/     Uma etapa por pasta; cada parte da tela é um componente próprio
```

- **`service/`** é a única camada que fala com o motor. Componentes e hooks recebem o `HarnessApi` por prop, o que permite
  injetar um dublê nos testes.
- **`domain/`** não importa React nem `service/`. Rótulos e tons ficam em tabelas (`Record`) em vez de ternários aninhados.
- Um componente faz **uma** parcela da tela. Passou de uma responsabilidade, vira outro arquivo na mesma pasta.

## Compartilhado

| Pasta | Conteúdo |
| ----- | -------- |
| `shared/api/` | Cliente do motor (`EngineClient`, `EngineError`) e detecção do runtime Tauri |
| `shared/schema/` | Contratos Zod do runtime, conferidos contra as fixtures de `contracts` |
| `shared/hooks/` | Hooks sem domínio (`useCopyToClipboard`, `useScrollToTop`, `useEngineStatus`) |
| `shared/routing/` | Caminhos das rotas e o repasse do pedido entre telas |
| `shared/components/ui/` | Atomic design: `atoms/` ← `molecules/`. Os átomos genéricos (`Tooltip`, `Radio`, `StatusChip`, `CountBadge`) vêm do `@mariliz/ui-kit` e são repassados pelo barrel |
| `shared/utils/` | Utilitários puros (`cn`, datas, preferências de tela) |

## Regras de código

- TypeScript `strict`, sem `any`; `import type` para tipos.
- Um literal ou uma lista usada em dois lugares vira constante no `domain/` (ex.: `AI_ENVIRONMENTS`, `ConflictResolution`).
- Um componente só sobe para `shared/` com três consumidores; antes disso vive na feature dona.
- Comentário explica um **porquê** que o código não mostra (acessibilidade, contrato com o host, segurança). Não repete o
  nome nem o tipo, e não descreve o que o JSX já diz.

## Validação

```bash
pnpm --dir desktop/ui check
```

Roda typecheck, lint, formatação e testes.
