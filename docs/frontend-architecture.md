# Arquitetura do frontend (`apps/desktop-ui`)

Espelha a organização do frontend do EmpregaNet: camadas `app → features → shared`, uma pasta por feature com
responsabilidades separadas e nada de lógica no JSX. O transporte aqui é o motor .NET via host Tauri, e não HTTP; por isso
`shared/api` faz o papel do cliente axios.

## Linguagem do produto

A interface fala só do **modelo de implementação Mariliz** e da **estrutura de especificação**. Nomes de kits ou
metodologias de terceiros não aparecem como escolha, rótulo ou título; o teste da etapa "Modelo e ambiente" falha se
aparecerem. Os ids das etapas são `project`, `model`, `plan`, `review`, `run` e `result`.

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
| `shared/schema/` | Contratos Zod do motor, conferidos contra as fixtures de `packages/contracts` |
| `shared/hooks/` | Hooks sem domínio (`useCopyToClipboard`, `useScrollToTop`, `useEngineStatus`) |
| `shared/components/ui/` | Atomic design: `atoms/` ← `molecules/` |
| `shared/utils/` | Utilitários puros (`cn`) |

## Regras de código

- TypeScript `strict`, sem `any`; `import type` para tipos.
- Um literal ou uma lista usada em dois lugares vira constante no `domain/` (ex.: `AI_ENVIRONMENTS`, `ConflictResolution`).
- Um componente só sobe para `shared/` com três consumidores; antes disso vive na feature dona.
- Comentário explica um **porquê** que o código não mostra (acessibilidade, contrato com o host, segurança). Não repete o
  nome nem o tipo, e não descreve o que o JSX já diz.

## Validação

```bash
pnpm --dir apps/desktop-ui check
```

Roda typecheck, lint, formatação e testes.
