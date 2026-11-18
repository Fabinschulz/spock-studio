# Catálogo de referências arquiteturais do Spock

> Base oficial de referências arquiteturais do Spock. Parte do estudo [Plataforma de engenharia com IA](README.md).
> Análise feita em 2026-11-17, por leitura estática dos repositórios (nada foi compilado nem executado). Os commits
> analisados estão na §1; quando a referência mudar, o catálogo é revisto pela §12.
>
> **Este documento não é regra universal.** É conhecimento extraído de projetos reais, com o que vale repetir, o que
> não vale e em que contexto. Cada padrão tem um identificador estável (`REF-R3`, `REF-N7`...) para que agentes e
> decisões possam citá-lo.

## 0. Como o Spock usa este catálogo

1. **Consultar só a seção da tecnologia em jogo.** Cada seção começa com uma linha `Escopo:` (tecnologia, tipo de
   projeto, responsabilidade). A recuperação seletiva está descrita em
   [SPOCK_ARCHITECTURE_RECOMMENDATIONS.md](SPOCK_ARCHITECTURE_RECOMMENDATIONS.md#r2-conhecimento-arquitetural-recuperável).

   | Situação | Seções |
   | --- | --- |
   | Projeto React | §3, §4 e §2 |
   | Projeto Angular | §5 e §2 |
   | Backend .NET | §6 e §2; §6.9 se houver integração externa |
   | Backend em outra tecnologia | §7, mais os princípios de §2 e §6 |
   | Fullstack | §8 e as seções de cada lado |
   | Modernização | Seção da tecnologia atual, seção da tecnologia de destino e §10.8 |
   | Preparar o Harness de IA | §9 |

2. **Distinguir quatro coisas em toda recomendação**, e escrever qual é qual:

   | Categoria | Exemplo | Peso |
   | --- | --- | --- |
   | Convenção oficial do projeto analisado | "Este repositório usa mediator próprio (decisão AD-012)" | Respeitar; só mudar com decisão do dono |
   | Referência arquitetural (este catálogo) | "REF-N3: transação condicionada por interface-marcador" | Argumento, não ordem |
   | Recomendação do Spock | "Sugerimos adotar REF-N3 porque os handlers repetem `BeginTransaction`" | Proposta com justificativa e trade-off |
   | Decisão do projeto atual | "Aprovado em 2026-11-20: adotar REF-N3" | Vira convenção, com registro |

3. **Não propor reescrita** por divergência de estilo. Divergir de uma referência só vira recomendação quando há um
   problema concreto (bug, risco, custo) que o padrão resolve. Ver §10.8.
4. **Voltar à fonte só quando necessário**: para conferir se a referência mudou (commit novo), ou quando o catálogo não
   cobre a dúvida. Nesse caso, a atualização entra no catálogo (§12).

## 1. Repositórios de referência

| Id | Repositório | Tecnologia | Responsabilidade | Branch @ commit | Data do commit | Maturidade |
| --- | --- | --- | --- | --- | --- | --- |
| EMP-FE | [Fabinschulz/EmpregaNetAPI `frontend/`](https://github.com/Fabinschulz/EmpregaNetAPI/tree/master/frontend) | Next.js 16.3, React 19.2, TanStack Query 5, Zod 4, RHF | App web autenticado (vagas, candidaturas, admin) | master @ e0b0a47 | 2026-10-21 | Alta |
| EMP-BE | [Fabinschulz/EmpregaNetAPI `backend/`](https://github.com/Fabinschulz/EmpregaNetAPI/tree/master/backend) | .NET 10, EF Core 10 + Npgsql, Identity, Redis | API do mesmo produto | master @ e0b0a47 | 2026-10-21 | Alta, com desvios de camada |
| EMP-MONO | [Fabinschulz/EmpregaNet](https://github.com/Fabinschulz/EmpregaNet) (redireciona para EmpregaNetAPI) | Monorepo .NET + Next + Terraform | Organização fullstack | master @ e0b0a47 | 2026-10-21 | Média |
| MLZ-WEB | [Fabinschulz/mariliz](https://github.com/Fabinschulz/mariliz) | React Router 8 (framework mode, pré-renderizado), Vite 8, MUI via kit | Site institucional estático, sem API | main @ d85abe1 | 2026-10-13 | Média |
| MLZ-UI | [Fabinschulz/mariliz-ui-kit](https://github.com/Fabinschulz/mariliz-ui-kit) | `@mariliz/ui-kit` 0.6.1: MUI 9 + Emotion, ESM | Design system React | master @ 2fc2760 | 2026-11-05 | Média |
| EZM-CRM | EasymartTecnologia/Easymart.CRM (privado) | Angular 19 standalone, Material 19, RxJS 7.8 | SPA de CRM; backend em outro repositório | master @ 9f49168 | 2026-10-23 | Média-baixa no código, alta no Harness |
| EZM-ANY | EasymartTecnologia/Easymart.IntegracaoAnymarket (privado) | .NET 10, EF Core SqlServer, SQS, Polly v8 | Conector multi-tenant ERP ↔ hub de marketplaces | main @ 35bce76 | 2026-11-17 | Alta |
| EZM-DOC | EasymartTecnologia/Easymart.EmissaoDocumentos (privado) | .NET 10, QuestPDF/PDFsharp/RDLC, Npgsql | Geração de PDFs e webhook de assinatura | main @ 3c5cc29 | 2026-10-23 | Baixa (contraexemplo útil) |

Os repositórios privados da empresa são citados por estrutura e caminho; nenhum segredo, host interno, nome de cliente
ou regra de negócio foi copiado.

## 2. Princípios transversais

Escopo: qualquer tecnologia · qualquer tipo de projeto.

Estes princípios aparecem em mais de uma referência e independem de stack. São a base das recomendações das seções
seguintes.

| Id | Princípio | Evidência | Por que importa para um agente de IA |
| --- | --- | --- | --- |
| REF-P1 | **Fronteira de arquitetura é um teste ou uma regra de lint, não uma convenção** | ESLint de camadas em EMP-FE (`eslint.config.mjs`); testes que leem `.csproj` em EZM-ANY (`tests/Shared/Architecture/`); teste de autorização por reflexão em EMP-BE | Agentes quebram convenções que não são verificadas; um gate falha antes do review |
| REF-P2 | **Contrato validado na fronteira, nas duas direções** | Zod na ida e na volta em EMP-FE (ADR 0009); testes de forma JSON em EMP-BE | Drift silencioso entre front e back é o bug mais comum que o ADR 0009 documenta |
| REF-P3 | **Um único formato de erro, com código estável** | `DomainError` em EMP-BE e EZM-ANY; `parseApiError` em EMP-FE | O cliente e o agente tratam um formato só; o código estável permite decidir sem casar texto |
| REF-P4 | **Efeitos externos só depois do commit** | Fila de eventos por requisição (EMP-BE, ADR 0012); `DomainEventPublishingUnitOfWork` (EZM-ANY) | Evita e-mail ou chamada externa sobre operação desfeita |
| REF-P5 | **Configuração crítica falha no boot** | Chave JWT curta derruba a subida (EMP-BE); chave obrigatória ausente derruba com o nome dela (EZM-ANY) | Erro de configuração aparece no deploy, não em produção |
| REF-P6 | **Idempotência em camadas** | Busca por chave local, lock com expiração, índice único e content hash (EZM-ANY) | Retries e reentregas são normais em integrações; sem idempotência viram duplicatas |
| REF-P7 | **Migração é etapa do deploy, nunca da réplica que serve tráfego** | `--migrate-only` em EMP-BE e EZM-ANY; contraexemplo em EZM-DOC | Corrida de migração entre réplicas e startup lento |
| REF-P8 | **Decisões registradas com consequências negativas e gatilho de revisão** | ADRs de EMP-BE; `.specs/STATE.md` (AD-NNN) em EZM-ANY e EZM-CRM | O agente sabe por que algo é assim e quando pode mudar |
| REF-P9 | **YAGNI com custos assimétricos**: decidir já o que é caro desfazer (contrato HTTP, migração destrutiva, captura de dados, autorização); adiar o resto | `backend-skill` e `frontend-skill` de EMP; EZM-ANY sem outbox por decisão | Evita tanto a arquitetura presumida quanto a dívida irreversível |
| REF-P10 | **Documentação que afirma o que o código não faz é um defeito** | EMP-BE cita OData e namespaces inexistentes; MLZ-WEB cita testes inexistentes | Agentes confiam na documentação; deriva vira código errado |

## 3. Frontend React

Escopo: tecnologia `react` · tipo `frontend`, `fullstack` · responsabilidades estrutura, dados, formulários, rotas,
autenticação, erros.

Fontes: EMP-FE (app com dados e autenticação) e MLZ-WEB (site de conteúdo sem API). O próprio MLZ-WEB declara seguir o
padrão de EMP-FE.

### 3.1 Estrutura recomendada

```text
src/
├── app/                      # rotas finas: só montam a feature (Next: file-system; RR: manifesto)
├── features/<domínio>/
│   ├── service/              # <x>-api.ts, <x>-request-schema.ts, <x>-response-schema.ts, <x>-keys.ts, <x>-queries.ts
│   ├── <tela>/index.tsx      # container: dados, mutations, navegação
│   ├── <tela>/<parte>.tsx    # apresentação: só props
│   ├── form/                 # <x>-form-schema.ts + mappers resposta→form e form→request
│   ├── domain/               # regras puras da feature
│   └── index.ts              # API pública nomeada (sem export *)
└── shared/
    ├── api/                  # cliente HTTP e interceptors
    ├── auth/                 # sessão, política de rota
    ├── components/{ui,form-fields,common}
    ├── hooks/
    ├── schema/               # paginação, erro de domínio
    └── utils/{errors,lib}
```

Dependências permitidas: `app → features → shared`. `shared` nunca importa `features`; uma feature não importa o
interior de outra (só o `index.ts`). Verificar por lint desde o primeiro commit (REF-R1).

### 3.2 Padrões

| Id | Padrão | Onde | Problema que resolve | Usar quando | Evitar quando | Como adaptar |
| --- | --- | --- | --- | --- | --- | --- |
| REF-R1 | Camadas `app → features → shared` verificadas por ESLint (`NO_FEATURES_FROM_SHARED`, `NO_DEEP_CROSS_SLICE`, `NO_EXPORT_STAR`) com mensagens didáticas | EMP-FE `eslint.config.mjs`; MLZ-WEB `eslint.config.js` (versão enxuta) | Acoplamento cíclico; `shared` virando depósito de domínio; barrel gigante | Mais de 2 features ou mais de uma pessoa/agente | Protótipo de uma tela | Começar com as 3 regras do MLZ-WEB; somar "sem axios em `.tsx`" quando entrar HTTP; `eslint-plugin-boundaries` se as regras crescerem |
| REF-R2 | Escada atomic design (`atoms → molecules → organisms → templates`) verificada por `no-restricted-imports` | EMP-FE `uiTierRule` | "Átomo" que importa componente | Biblioteca de UI própria | Kit externo (MUI, `@mariliz/ui-kit`): a camada local é fina | Proibir também importar o barrel de `ui/` dentro de `ui/` |
| REF-R3 | `service/` por feature com Zod na ida e na volta; schema de formulário separado, com mappers | EMP-FE `features/recrutamento/vagas/service/*`, ADR 0009 | Drift de contrato; regra de UX guardando o transporte | API que você não controla 100% | Cliente gerado de OpenAPI com validação em runtime: gere os schemas | Response schema "estrito na forma, permissivo na regra"; a API nunca importa schema de formulário; paginação genérica `createPaginatedResponseSchema(item)` |
| REF-R4 | Query keys em fábrica (`all → lists → list(params) → detail(id)`) e `QueryClient` sem retry em 4xx, `staleTime` 60 s | EMP-FE `*-keys.ts`, `shared/utils/lib/query-client.tsx` | Invalidação imprecisa; martelar a API com 403/404 | Qualquer app com TanStack Query | — | Documentar no hook a invalidação entre domínios |
| REF-R5 | Taxonomia única de erro (`parseApiError`, 10 tipos) e ponto único de reporte de mutation: erro no campo quando o servidor aponta campo conhecido, senão toast + alerta | EMP-FE `shared/utils/errors/*`, `shared/context/form-context.tsx` | Cada `onError` com sua política; erro de campo sumindo | Backend com erro estruturado | — | Mapear o formato do backend num só lugar; manter o filtro "só campos que o form conhece" |
| REF-R6 | Sessão em cookie httpOnly; a mesma `evaluateRouteAccess` no proxy (servidor) e no guard (cliente); permissões nomeadas (`can(roles,'job.delete')`); refresh em 401 com promessa única | EMP-FE `src/proxy.ts`, `shared/utils/lib/route-access-policy.ts`, `rbac.ts`, ADR 0001 | Roubo de sessão por XSS; duas implementações de autorização que divergem; tempestade de refresh | App autenticado com backend próprio | API em domínio sem cookie compartilhável e sem BFF possível | Deixar escrito que o proxy decodifica o JWT sem verificar (é gate de UX; a autorização real é da API); validar `?redirect=` contra open redirect |
| REF-R7 | Estado de filtro e lista na URL, com codec puro (`parse`/`serialize`, defaults omitidos) | MLZ-WEB `features/busca/core/url-state.ts`; EMP-FE `use-url-synced-params.ts` | Filtro perdido ao recarregar; URL não compartilhável | Listas com filtro | — | O codec do MLZ-WEB é o núcleo; o hook do EMP-FE só com formulário de filtro |
| REF-R8 | Manifesto único de rotas que gera rotas, navegação, breadcrumbs, SEO, sitemap e prerender | MLZ-WEB `shared/routing/route-manifest.ts` | Path duplicado; página fora do sitemap | React Router com rotas por configuração | Next App Router (o file-system já é o manifesto): usar `xRoutes` por entidade | — |
| REF-R9 | Porta/adaptador para capacidade trocável, carregada sob demanda e cancelável | MLZ-WEB `features/busca/core/types.ts` (`SearchProvider`), `telemetry.ts` | Trocar o fornecedor mexe na UI; bundle inicial pesado | Busca, telemetria, pagamentos | Capacidade com uma só implementação previsível | Com TanStack Query, `useQuery({ queryFn: ({signal}) => provider.search(q, signal) })` |
| REF-R10 | Vocabulário fechado de estados: skeletons canônicos, `LoadingState` com atraso de ~250 ms, botão de envio com `aria-busy` | EMP-FE `shared/components/common/loading/*`; MLZ-WEB `StateMessage` | Telas inconsistentes e inacessíveis | Sempre | — | Com `@mariliz/ui-kit`, usar o `skeleton` dos componentes (§4) |
| REF-R11 | Hidratação sem mismatch com `useSyncExternalStore` | MLZ-WEB `use-hydrated.ts`; EMP-FE `use-has-mounted.ts` | Warning de hidratação e tela piscando | SSR ou pré-renderização | SPA pura | — |
| REF-R12 | Skill de frontend com stack fechada, anti-padrões, checklist e "teste de decisão" YAGNI; ADRs curtas | `EmpregaNetAPI/.claude/skills/frontend-skill/SKILL.md`; `mariliz/docs/ARCHITECTURE.md` | Agente sem critério para decidir | Todo projeto com IA no fluxo | — | Ver §9 |

### 3.3 Não copiar

| Problema | Onde | O que fazer |
| --- | --- | --- |
| `ApiContractError` existe e nunca é lançado; os services lançam `ZodError` cru | EMP-FE `api-contract-error.ts` × `*-api.ts` | Helper `parseResponse(schema, data, endpoint)` com `safeParse` |
| Error boundary sem reset: depois de erro de render, "tentar de novo" não sai da tela | EMP-FE `error-boundary/index.tsx` | `resetKeys` ou `QueryErrorResetBoundary`; separar erro de query de erro de render |
| Hooks de mutation com toast, router e `useState` dentro de `service/` | EMP-FE `jobs-queries.ts` | Mutation pura no service; efeitos de UI no container |
| `FormProvider` próprio que reexpõe a API do RHF com `as never` | EMP-FE `shared/context/form-context.tsx` | `FormProvider`/`useFormContext` nativos + só o que é seu |
| Cucumber para lógica pura; sem teste de componente | EMP-FE `cucumber.mjs` | Vitest + Testing Library + MSW; Gherkin só onde o produto lê |
| Documentação que cita testes inexistentes; nenhum CI rodando `check` | MLZ-WEB `README.md`, `docs/ARCHITECTURE.md` | REF-P10; CI mínimo com typecheck, lint e testes |
| Duas fontes de cor (tokens SCSS × tema) | MLZ-WEB `brand-palette.ts` × `_tokens.scss` | Gerar uma da outra |
| Non-null assertion | MLZ-WEB | `no-non-null-assertion` no lint (EMP-FE já tem) |

### 3.4 Escolha de framework React

| Produto | Escolha | Motivo |
| --- | --- | --- |
| App autenticado com partes públicas indexáveis | Next.js App Router (EMP-FE) | SSR/RSC e proxy na borda para política de rota |
| Site de conteúdo, documentação, marketing | React Router 8 framework mode com `prerender` + Vite (MLZ-WEB) | Sem servidor em runtime, menos superfície |
| SPA interna atrás de login, ou app desktop (Tauri) | Vite + React Router (data mode), sem SSR | Nada a ganhar com SSR; build simples |

Lint recomendado = soma dos dois: camadas e níveis do EMP-FE, mais `jsx-a11y`, `consistent-type-imports` e
`noUncheckedIndexedAccess` do MLZ-WEB.

## 4. Design system: `@mariliz/ui-kit`

Escopo: tecnologia `react` · tipo `frontend` · responsabilidade interface, componentes, tema.

### 4.1 Identidade

| Item | Valor |
| --- | --- |
| Pacote | `@mariliz/ui-kit` 0.6.1, MIT, público no npm (antes `@synthra.io/ui-kit`) |
| Base | Invólucro do MUI 9 + Emotion. Não usa Radix, Tailwind nem CSS Modules |
| Peers | React `^18.2 \|\| ^19`; `@mui/material` 9; `@mui/x-data-grid` 9; `@emotion/react` e `@emotion/styled` 11.14+; `react-hook-form` 7.55+; `next >=15` opcional (entry `/next`) |
| Build | Só ESM, um arquivo por módulo, `sideEffects: false`, `'use client'` preservado. Não há CSS para importar |
| Entradas | `@mariliz/ui-kit` e `@mariliz/ui-kit/next` |
| Release | Changesets + Trusted Publishing OIDC com provenance; versão 0.x quebra API em minors |

### 4.2 Inventário

| Grupo | Componentes |
| --- | --- |
| Átomos | `Button` (props do MUI + `skeleton`), `TextField`, `SelectField` (`items` ou `options`), `Autocomplete` (`endIconType`, textos pt-BR sobrescrevíveis), `Checkbox`, `Switch`, `Typography` (variantes extras `xg`, `xxxl`, `xxl`), `Avatar` (iniciais e cor pelo nome), `Divider`, `LoadingBar`, `MenuItem` (único com `forwardRef`), `TabItem` |
| Moléculas | `Alert` (`title`, `description`), `Breadcrumb` (`aria-current` no último), `CardData` (pares chave/valor com cor), `TabBar` (aba ativa pela rota) |
| Organismos | `DataTable` (MUI X DataGrid, paginação client/server, seleção), `Modal` (3 larguras), `Drawer`, `Menu` (sidebar recolhível com submenus) |
| Templates | `TableComponent` (DataTable com paginação local) |
| Formulários | `FormProvider` (RHF + `resolver`, `submitting`, `readOnly`), `TextFormField`, `SelectFormField`, `CheckboxFormField`, `AutocompleteField` |
| Ícones | 430 componentes `SvgIcon` com `currentColor`, sufixo `Icon` |
| Next | `GlobalLayoutContainer` (`<html lang="pt-BR">` + tema), `ThemeRegistry` (cache Emotion para SSR), `nextLinkComponents`, `TabBar` com `usePathname` |
| Tema | `initializeTheme({ mode, locales, overrides })`; temas `light` e `dark` prontos; tokens TS (`spacing`, `borderRadius`, `fontSizes`, `fontWeights`, `lineHeights`, `opacity`); paleta com `neutral`, `brand` e `custom` por module augmentation; fonte Lato (carregada pela aplicação) |

### 4.3 Como adotar

Pré-requisitos: React 18.2+ (19 de preferência, por causa das refs), bundler ESM (Vite, Next 15+, webpack 5), MUI 9,
X Data Grid 9, Emotion 11, RHF 7.55+.

1. Instalar o kit e os peers (`@mui/material @mui/x-data-grid @emotion/react @emotion/styled react-hook-form`; para
   validação, `@hookform/resolvers zod`).
2. Carregar a fonte Lato.
3. Next App Router: `GlobalLayoutContainer` no `app/layout.tsx`, ou `ThemeRegistry` se o layout já tiver `<html>`.
   Vite/React Router: `<Provider theme={...}>` e o link do roteador configurado no tema.
4. Dark mode ou white-label: `initializeTheme({ mode, overrides })` dentro de um client component; trocar o objeto
   `theme` para alternar em runtime (trocar `mode` depois da montagem não surte efeito).
5. Formulários com `FormProvider` + `*FormField` + `resolver`.
6. Importar sempre pela raiz nomeada; deep imports não estão no `exports`.

### 4.4 Quando não usar

- O projeto já usa outro motor de estilo (Tailwind/shadcn/Radix, Chakra, Ant, styled-components): dois resets e dois
  sistemas de tokens.
- O projeto está preso a MUI ≤ 7, React < 18.2, Next < 15 ou precisa de CJS (Jest legado, `require`).
- Server Components puros ou CSS sem runtime: todo componente é `'use client'` e o estilo depende do Emotion.
- Mobile-first ou navegação responsiva: o `Menu` é sidebar permanente e o `DataTable` não tem layout de cartões.
- i18n multilíngue: há textos pt-BR fixos em vários componentes.
- Exigência formal de qualidade (WCAG auditado, cobertura, regressão visual): o kit não tem testes.

### 4.5 Lacunas conhecidas

Zero testes (o addon de acessibilidade só aparece no Storybook, sem runner); `CodeField` existe e não é exportado; só
`MenuItem` encaminha `ref` (no React 18 as refs dos outros se perdem); tokens de espaçamento e raio não estão ligados ao
tema (`theme.spacing` continua 8 px); sem CSS variables nem `prefers-color-scheme`; `lineHeight` de h1/h2 errado;
organismos sem `sx`/`className` no root; `DataTable` com page sizes e filtros fixos; faltam toast, date picker,
tooltip, popover, radio, slider, chip, stepper, accordion, upload, spinner e layout.

### 4.6 Regras para agentes que constroem UI com o kit

1. Procurar no inventário antes de criar componente. Existe no kit: usar. Não existe: usar o MUI 9 direto (herda o
   tema). Nunca criar um componente paralelo nem reestilizar do zero.
2. Uma única árvore de tema (`GlobalLayoutContainer`, `ThemeRegistry` ou `Provider`); nada de `ThemeProvider` ou
   `CssBaseline` em paralelo.
3. Cores só pelo tema (`sx={{ color: 'neutral.dark' }}`); hex literal é proibido. Marca do cliente via `overrides`.
4. Espaçamento com a unidade do MUI (`p: 2` = 16 px); a escala de tokens só quando o design exigir, sem misturar as
   duas no mesmo componente. Sem system props (o MUI 9 removeu): usar `sx`.
5. `skeleton` para carga inicial, `loading` para ação em andamento.
6. Acessibilidade obrigatória: passar `closeLabel`, `labels`, `endIconLabel`, `searchLabel`/`clearLabel`; `aria-label`
   em todo `IconButton`; validar com axe, porque o kit não testa.
7. React 18: não confiar em `ref` nos componentes do kit; usar `inputRef` nos campos.
8. Lacuna recorrente (toast, date picker) vira proposta de contribuição ao kit, não cópia em cada projeto.

## 5. Frontend Angular

Escopo: tecnologia `angular` · tipo `frontend` · responsabilidades estrutura, DI, rotas, formulários, estado, HTTP.

Fonte: EZM-CRM (Angular 19, 100% standalone, Material 19, Jasmine/Karma + Playwright). O código tem qualidade média e
dívidas claras; o Harness de IA do repositório é de alta qualidade. Não misturar com padrões React: as convenções
abaixo são idiomas do Angular.

### 5.1 Estrutura recomendada (modelo-alvo, Angular 19+)

```text
src/app/
├── app.config.ts             # provideRouter, provideHttpClient(withInterceptors), ErrorHandler, APP_CONFIG
├── app.routes.ts             # shell + loadChildren por feature
├── core/                     # singletons técnicos; nunca importa features
│   ├── http/                 # interceptors funcionais: auth, erro
│   ├── guards/               # CanActivateFn retornando UrlTree
│   ├── config/               # InjectionToken APP_CONFIG + provideAppInitializer
│   └── error-handling/       # ErrorHandler global
├── shared/
│   ├── ui/                   # componentes de apresentação: input()/output(), OnPush
│   ├── pipes/
│   └── forms/                # validators puros e ControlValueAccessors
└── features/<feature>/
    ├── <feature>.routes.ts
    ├── data-access/          # api service por recurso + store com signals
    ├── pages/                # componentes de rota
    └── ui/                   # componentes privados da feature
```

### 5.2 Padrões

| Id | Padrão | Onde (EZM-CRM) | Problema que resolve | Usar quando | Evitar quando | Como adaptar |
| --- | --- | --- | --- | --- | --- | --- |
| REF-A1 | App standalone com `app.config.ts` e rotas `loadComponent` aninhadas espelhando as pastas | `app.config.ts`, `app.routes.ts` | Bundle único; achar o código de uma URL | Todo app Angular ≥ 15 novo | Aninhamento > 3 níveis sem input binding | `withComponentInputBinding()`, `paramsInheritanceStrategy: 'always'`, `loadChildren` por feature |
| REF-A2 | Interceptor funcional de autenticação com refresh deduplicado (`shareReplay(1)` + `finalize`) | `core/interceptors/auth.interceptor.ts`, `core/services/token-refresh.service.ts` | N requisições com token vencido disparando N refreshes | JWT com refresh token | Sessão por cookie httpOnly (o refresh é do servidor) | URLs públicas por `HttpContextToken` (`SKIP_AUTH`), não `url.includes` |
| REF-A3 | `ErrorHandler` global estreito: recarrega uma vez quando falha o chunk (deploy novo), com trava em `sessionStorage` | `core/error-handlers/session-aware.error-handler.ts` | Tela quebrada depois de deploy | SPA com hash de chunks | Usá-lo para engolir erro | `inject()` de `DOCUMENT`/`Router`; telemetria no ramo final |
| REF-A4 | Tradutor único de erro da API | `core/services/api-handle-messages.service.ts` | Formatos de erro heterogêneos | Backend legado com erro inconsistente | — | Chamar uma vez, num interceptor de erro |
| REF-A5 | Validators como funções puras, testados sem `TestBed`; mensagens centralizadas | `shared/helpers/validators.ts` | Validação espalhada e difícil de testar | Sempre | — | Mensagens por pipe ou função pura; `NonNullableFormBuilder` |
| REF-A6 | `ControlValueAccessor` para máscara: exibe formatado, guarda só dígitos | `shared/directives/phone-mask.directive.ts` | API recebendo valor formatado | CPF/CNPJ, telefone, moeda | — | `host: { '(input)': ... }` em vez de `@HostListener` |
| REF-A7 | Busca com `Subject` + `switchMap` | `open-attendence/campaign/campaign.component.ts` | Resposta obsoleta sobrescrevendo a atual | Filtros e buscas | — | `debounceTime`, `distinctUntilChanged`, `takeUntilDestroyed()`; ou `resource`/`rxResource` |
| REF-A8 | Tema runtime por variáveis CSS aplicado antes do bootstrap | `main.ts`, `shared/helpers/theme-palette.ts`, `theme.scss` | White-label sem recompilar e sem flash | Multi-tenant visual | — | Material 3 `mat.theme()` com tokens `--mat-sys-*` |
| REF-A9 | Testes fora de `src/`, espelhando a árvore, com critério explícito: unit (tudo dublê), integration (≥ 1 real ou `HttpTestingController`), E2E (Playwright) | `tests/{unit,integration,e2e}` | Fronteira vaga entre tipos de teste | Times com vários tipos de teste | Projeto pequeno (spec ao lado é mais simples) | Angular ≥ 20: Vitest/Jest no lugar do Karma |
| REF-A10 | Configuração estrita: `strictTemplates`, `noPropertyAccessFromIndexSignature`, `explicit-function-return-type`, commitlint | `tsconfig.json`, `.eslintrc.json` | Erros de template em runtime | Sempre | — | Flat config `eslint.config.js` alinhado à versão |
| REF-A11 | Harness versionado: skill de convenções levantada do código (com "inconsistência conhecida" registrada), agents agnósticos de stack, specs por ticket | `.claude/`, `.specs/` | Agente sem conhecimento da stack | Todo projeto com IA | — | Ver §9 |

Estado: service por feature com `signal` privado e `computed` público; RxJS só para fluxo assíncrono, convertido com
`toSignal`; NgRx Signal Store quando muitas telas compartilham o mesmo agregado. DI: `inject()` em tudo ou construtor
em tudo, nunca os dois na mesma classe.

### 5.3 Não copiar

| Problema | Onde | O que fazer |
| --- | --- | --- |
| Token e `apiUrl` capturados no construtor de services singleton | Vários services em `core/services` | Config por `InjectionToken`; header só no interceptor |
| Header de token repetido 141 vezes; query string concatenada sem encoding | Services HTTP | `HttpParams`; `withCredentials` no interceptor; um api service por recurso |
| Credenciais em query string de GET | `user.service.ts` | POST com corpo |
| `localStorage` "criptografado" com chave embutida no bundle | `local-storage.service.ts` | Cookie httpOnly para token; `sessionStorage` honesto para o resto |
| `catchError(() => [])` completa sem emitir | Service de negociação | `of([])` |
| Erro engolido como Result em uns services e propagado em outros | `core/services` | Um modelo só: propagar erro normalizado e tratar na borda |
| God services (≈1.300 linhas) e god components (≈1.600 linhas, 44 `subscribe`) | `core/services`, `features/**` | Quebrar por caso de uso; store por feature |
| `core` importando componentes de `features` | `core/services/dialog.service.ts` | Fachada só para dialogs genéricos; dialog de feature ao lado da feature |
| Guards de classe dentro de `interceptors/` com `navigate()` + `false` | `core/interceptors/*guard*` | `CanActivateFn` em `core/guards` retornando `UrlTree` |
| 293 `subscribe` sem teardown; `takeUntilDestroyed` com `DestroyRef` raiz | Componentes; `form-error-message.service.ts` | `DestroyRef` do componente, `async`, `toSignal` |
| OnPush com estado mutável e `markForCheck` manual; `{} as Tipo` (48×) | Componentes | `signal`/`computed`; `input()`/`output()`; valores iniciais válidos |
| `route.parent?.parent?.paramMap` | Componentes netos | Input binding de rota ou resolver |
| Tabela genérica que formata pelo texto do cabeçalho | `shared/components/info-table` | Coluna `{ key, header, format }` + pipes puros |
| Dependências desalinhadas (`@angular-eslint` 18 com Angular 19) e Dockerfile sem `npm ci` | `package.json`, `Dockerfile` | Alinhar versões; `npm ci` com cache de camada |

## 6. Backend .NET

Escopo: tecnologia `dotnet` · tipo `backend`, `api`, `worker`, `integração` · responsabilidades camadas, domínio,
casos de uso, persistência, integrações, erros, segurança, testes.

Fontes: EMP-BE (API de produto), EZM-ANY (conector multi-tenant, a referência mais madura) e EZM-DOC (serviço pequeno,
usado sobretudo como contraexemplo).

### 6.1 Organização da solução

Duas formas válidas, escolhidas pelo tamanho do domínio:

| Forma | Quando | Referência |
| --- | --- | --- |
| **Camadas únicas**: `Domain`, `Application`, `Infrastructure`, `Api`, `Tests` | Um domínio coeso, uma equipe, até dezenas de casos de uso | EMP-BE |
| **Contextos delimitados**: `BuildingBlocks/*`, `Integrations/<Sistema>`, `BoundedContexts/<Contexto>/{Domain,Application,Infrastructure}`, `Hosts/{Api,Worker}` | Vários subdomínios com regras próprias, mais de um host (API + worker), integrações pesadas | EZM-ANY |

Comum às duas: `Directory.Build.props` (EZM-ANY usa para nomes curtos e namespaces), e **deveria** haver
`Directory.Packages.props` (gestão central de versões) e `global.json`: nenhuma das referências tem, e ambas repetem
versões de pacotes e precisam de pins manuais para fechar vulnerabilidades transitivas.

### 6.2 Limites das camadas

```text
Domain ← Application ← Infrastructure ← Hosts (Api, Worker)
            ↑                  ↑
      BuildingBlocks     Integrations/<Sistema>  (só SharedKernel)
```

- `Domain` não referencia pacote de framework. EMP-BE viola (ASP.NET Identity no `User`, registrado em ADR 0005; o
  mediator mora no Domain; `DomainError` com status HTTP). EZM-ANY cumpre (`Orders.Domain.csproj` sem pacote nenhum).
- `Application` não conhece HTTP. EMP-BE viola (`IHttpContextAccessor`, emissão de JWT na Application); a saída é uma
  porta `ICurrentUser` implementada na Api.
- `Application` nunca referencia `Infrastructure`. EZM-DOC inverte isso e é o contraexemplo.
- Um contexto nunca referencia a `Infrastructure` de outro; só a `Application` publicada (EZM-ANY fixa essa
  superfície em testes: 27 tipos no Catalog).
- **A regra só vale se for testada** (REF-P1): EZM-ANY lê os `.csproj` por regex em testes, sem compilar.

### 6.3 Domínio

- Agregados com estado privado, construtor privado para o ORM, coleções expostas como `IReadOnlyList`, métodos de
  intenção (`Close`, `FillPosition`, `ConfirmCreation`) e a invariante documentada no próprio membro (EMP-BE `Job.cs`;
  EZM-ANY `ReferenceMapping.cs`).
- Transições recebem `now` como parâmetro, nunca leem o relógio (EZM-ANY); `TimeProvider` injetado nos serviços (EMP-BE).
- Value objects com regra e igualdade por valor (`ContentHash`, `TenantId`, `AnymarketId` em EZM-ANY). O `Address`
  mutável de EMP-BE é o modelo a evitar.
- Eventos de domínio levantados pelo agregado (`AggregateRoot` com lista de eventos em EZM-ANY). Em EMP-BE quem
  enfileira é o handler: funciona, mas o evento deixa de ser do domínio.
- Domínio anêmico é aceitável em CRUD sem regra; não em máquinas de estado.

### 6.4 Casos de uso

- Pasta por caso de uso (slice) com `Command`, `Handler`, `Validator` (EZM-ANY `Intake/Application/AcceptClientEvent/`;
  EMP-BE `Application/<Feature>/Commands/<Ação>`).
- Mediator: as duas referências maduras usam **implementação própria** (proibição registrada de MediatR em EZM-ANY;
  licença). Se usar, mantê-lo num `BuildingBlocks/Mediator` com namespace, nunca no Domain. Rotinas em lote podem
  chamar o núcleo direto, sem mediator.
- Pipeline de behaviors (EMP-BE): `Performance → Validation → NotificationDispatch → Transaction`, com a ordem
  **testada a partir do registro real** (REF-N4).

### 6.5 Validação e erros

- FluentValidation no pipeline, com cultura fixada em `pt-BR` no host (sem isso as mensagens saem em inglês).
- Não injetar `IValidator` no handler quando o pipeline já valida (ruído em EMP-BE).
- **Modelo de erro de EZM-ANY** é o recomendado: catálogo `DomainErrorCode`; `Result`/`Result<T>` para ramo de negócio
  previsto; `DomainException` quando não dá para retornar ou quando seguir deixaria o agregado inconsistente;
  `WriteConflictException` traduzida de violação de índice único e de concorrência; um só corpo HTTP com mapeamento
  código → status; detalhes de falha externa só em Development.
- Não mapear exceções do BCL para o contrato HTTP: `InvalidOperationException` → 409 em EMP-BE e EZM-DOC faz bug de
  infraestrutura chegar ao cliente como "conflito". "Não encontrado" deve ser 404, não 400.
- `ProblemDetails` (RFC 9457) com extensões `code` e `errors` é alternativa válida e padronizada; o essencial é um
  contrato só (as duas referências maduras escolheram corpo próprio, por decisão registrada).

### 6.6 Persistência

- EF Core; um `DbContext` por contexto delimitado, cada um com tabela de histórico de migração própria (EZM-ANY).
- Unit of Work que salva, **publica eventos depois do commit** e traduz conflitos (EZM-ANY
  `DomainEventPublishingUnitOfWork`); com retry automático, limpar o `ChangeTracker` a cada nova tentativa (EMP-BE,
  ADR 0013).
- Uma única fronteira de `SaveChanges`: em EMP-BE o repositório genérico também salva, e a fronteira fica dupla.
- Soft delete sempre com `HasQueryFilter` (EMP-BE confere `IsDeleted` à mão em cada handler).
- Concorrência: `rowversion` (EZM-ANY), `SELECT ... FOR UPDATE` (EMP-BE), índice único parcial como defesa final.
- Migração como etapa do deploy (REF-P7): `--migrate-only` + advisory lock (EMP-BE) ou tarefa avulsa no ECS que precisa
  terminar com 0 antes de atualizar o serviço (EZM-ANY); script idempotente gerado no CI como artefato revisável.

### 6.7 Configuração, observabilidade e segurança

- Options pattern com `SectionName` e validação no startup (`ValidateOnStart`); só `appsettings.example.json`
  versionado; segredos de ambiente ou Secrets Manager.
- Logs estruturados com templates (sem interpolação, sem PII); um único building block conhece Serilog/Sentry/Datadog
  (EZM-ANY `BuildingBlocks/Observability`); correlação por middleware, propagada em filas e chamadas externas.
- Health separado em `live` e `ready` (EMP-BE). EZM-ANY só tem liveness: lacuna.
- Nenhuma referência usa OpenTelemetry; é a lacuna comum e a recomendação para projetos novos.
- Segurança: autorização segura por omissão (`[Authorize]` na classe base) **com teste por reflexão** (EMP-BE
  `ControllerAuthorizationTests`); comparação de segredo com `CryptographicOperations.FixedTimeEquals`; refresh token
  guardado como hash, com detecção de reuso; tokens de API guardados como hash; clientes HTTP sem redirect automático e
  com headers sensíveis redigidos.

### 6.8 Testes

- Teste existe para provar regra, não o encanamento do framework (política escrita em EZM-ANY).
- `WebApplicationFactory<Program>` com dublês; handler HTTP de gravação **no registro real do cliente** para afirmar
  headers e retries; `FakeTimeProvider` controlando também os atrasos do Polly; logger de gravação para afirmar
  propriedades estruturadas (EZM-ANY).
- Banco real condicional que aparece como *skipped* quando falta, nunca como aprovado (`[LocalDbFact]` em EZM-ANY,
  `[PostgresFact]` em EMP-BE); no CI, container de serviço. Testcontainers quando houver Docker local.
- Testes de convenção: forma JSON da resposta, ordem do pipeline, autorização por rota, fronteiras de projeto.
- Nomes `Metodo_Cenario_Resultado`.

### 6.9 Integrações externas

A seção mais forte de EZM-ANY; aplica-se a qualquer serviço que fale com sistemas de terceiros.

| Id | Padrão | Onde (EZM-ANY) | Problema que resolve | Evitar quando |
| --- | --- | --- | --- | --- |
| REF-N1 | Cliente único por sistema externo (camada anticorrupção): só ele monta autenticação, URL base, paginação e exceções tipadas; contratos de fio nunca vazam | `src/Integrations/Anymarket/AnymarketClient.cs` | Segredo e contrato do fornecedor espalhados | Uma chamada trivial: typed client direto |
| REF-N2 | Resiliência ciente do verbo: retry com jitter só em método idempotente; POST só em 429; timeout por tentativa; respeita `Retry-After`/`ratelimit-reset` com teto | `AnymarketServiceCollectionExtensions.cs` | Duplicata por retry de POST que já tinha sido gravado | Provedor com `Idempotency-Key`: usar a chave e repetir |
| REF-N3 | Rate limiter por tenant com teto de espera diferente por host (worker curto: devolve à fila; API longo) | `Integrations/Anymarket/RateLimit/TenantRateLimiter.cs` | Thread presa esperando cota | API externa sem limite por credencial |
| REF-N4 | Isolamento por tenant (circuit distribuído): tenant com dependência fora do ar não consome tentativas dos outros | `Integrations/Suite/Isolation/` | Um cliente derruba o processamento de todos | Um tenant só: circuit breaker simples |
| REF-N5 | Classificador de falha externa (`Transient`, `CredentialRefusal`, `Permanent`, `NotExternal`) | `SharedKernel/Errors/ExternalFailureClassifier.cs` | Decidir entre pular registro, parar lote ou adiar mensagem | — |
| REF-N6 | Consumidor de fila com envelope versionado (`messageType`, `messageVersion`, `tenantId`, `correlationId`), delete só após sucesso, DLQ, adiamento por visibilidade, extensão em handler longo, fila separada para lote | `BuildingBlocks/Messaging.Sqs/SqsFlowConsumer.cs` | Mensagem perdida ou repetida sem controle | Processamento síncrono simples |
| REF-N7 | Idempotência em camadas: busca pela chave local antes de criar, lock de criação com expiração e `rowversion`, índice único, content hash para pular envio sem mudança | `ReferenceMapping.cs`, `ContentHash.cs` | Duplicatas no sistema externo | — |
| REF-N8 | Segredos por tenant, buscados na hora da chamada, por sistema, com cache curto que não guarda falha | `Tenancy/Infrastructure/Secrets/` | Segredo quebrado de um sistema bloqueando tudo | Aplicação de tenant único com segredo fixo |
| REF-N9 | Webhook seguro: `EnableBuffering`, HMAC com `FixedTimeEquals`, parse seguro, sem logar corpo nem assinatura, tolerância de timestamp, deduplicação por id de evento | Contraexemplo: EZM-DOC `HmacValidationMiddleware.cs` | Falsificação, replay e reentrega duplicando efeito | — |

Outbox: nenhuma referência usa. EMP-BE e EZM-ANY aceitam, por decisão registrada, perder o evento entre o commit e a
publicação. Usar outbox quando o evento cruza processos e perdê-lo custa caro (pagamento, integração entre sistemas).

### 6.10 Padrões de aplicação

| Id | Padrão | Onde | Evitar quando |
| --- | --- | --- | --- |
| REF-N10 | Transação condicionada por interface-marcador (`TransactionBehavior where TRequest : ITransactional`) | EMP-BE `Infra/Behaviors/TransactionBehavior.cs` | Vários bancos ou sagas |
| REF-N11 | Eventos por requisição despachados depois do commit, com deduplicação | EMP-BE `DomainEventQueue.cs`, ADR 0012 | Evento cuja perda é cara: outbox |
| REF-N12 | Teste que fixa a ordem do pipeline a partir do registro de produção (`internal` + `InternalsVisibleTo`) | EMP-BE `PipelineBehaviorRegistrationTests.cs` | — |
| REF-N13 | Testes de convenção de segurança por reflexão sobre controllers | EMP-BE `ControllerAuthorizationTests.cs` | — |
| REF-N14 | Startup coordenado: espera do banco, advisory lock, etapas (`MigrationStep`, `SeedStep`) | EMP-BE `Infra/Persistence/Startup/*` | Serverless: migrar no pipeline |
| REF-N15 | Fronteiras de projeto como testes que leem `.csproj` e varrem o código (posse de header, `HttpClient` proibido fora do cliente) | EZM-ANY `tests/Shared/Architecture/` | — |
| REF-N16 | Porta de geração com estratégias trocáveis por biblioteca | EZM-DOC `Domain/Interfaces/IDanfeGenerator.cs` + `Infrastructure/Libs/<Lib>/` | Uma biblioteca só e estável |

### 6.11 Não copiar

| Problema | Onde |
| --- | --- |
| Mediator e `DomainError` com status HTTP no Domain; Application com JWT e `IHttpContextAccessor` | EMP-BE |
| `HttpUserContext.SetHeader` copiando claims para headers da requisição | EMP-BE |
| Repositório genérico com ordenação por `switch` de string e `SaveChanges` duplicado | EMP-BE, EZM-DOC |
| Projetos de capacidade presumida sem consumidor (`EmpregaNet.AI`, BFF vazio) | EMP-BE |
| Newtonsoft e System.Text.Json configurados juntos | EMP-BE, EZM-DOC |
| Connection string no console; `EnableSensitiveDataLogging` em todos os ambientes | EMP-BE, EZM-DOC |
| Application referenciando Infrastructure | EZM-DOC |
| Migração no startup de cada réplica com `BuildServiceProvider()` | EZM-DOC |
| `catch (Exception)` relançando `new Exception(...)` | EZM-DOC |
| `static HttpClient` baixando URL vinda do request (SSRF) | EZM-DOC |
| CORS `AllowAll` e endpoints sem autenticação; chaves AWS na configuração | EZM-DOC |
| Skill de conhecimento de ~50 mil tokens misturando conhecimento estável com changelog | EZM-ANY |
| Mediator por reflexão a cada chamada (aceitável, mas avaliar source generator) | EZM-ANY |

## 7. Outras tecnologias de backend

Escopo: tecnologias `node`, `python`, `java`, `go` · tipo `backend`.

**Esta seção é recomendação do Spock, não referência extraída**: nenhum repositório de referência usa essas stacks. Ela
aplica os princípios de §2 e §6 às convenções de cada ecossistema, sem reproduzir a estrutura de projetos .NET.

| Necessidade | .NET (referências) | Node/TS (NestJS) | Python (FastAPI) | Java (Spring) | Go |
| --- | --- | --- | --- | --- | --- |
| Organização | Projetos por camada ou por contexto | Módulos Nest por domínio; `domain/`, `application/`, `infrastructure/` dentro do módulo | Pacotes por domínio; `routers/`, `services/`, `repositories/` | Pacotes por domínio (package-by-feature) | Pacotes pequenos por domínio; `internal/` para fronteira |
| Pipeline de caso de uso | Behaviors do mediator | Interceptors e pipes | Dependências do FastAPI ou decorator de caso de uso | AOP, `@Transactional` | Wrapper de função (middleware) |
| Transação só em comando (REF-N10) | `ITransactional` | `AsyncLocalStorage` + decorator | Decorator com sessão do SQLAlchemy | `@Transactional` | Função `WithTx` |
| Efeito após commit (REF-N11) | Fila por requisição | Fila no contexto da requisição | Hook `after_commit` | `TransactionSynchronization.afterCommit` | Callback após `Commit` |
| Validação na fronteira | FluentValidation | `class-validator` ou Zod | Pydantic | Bean Validation | Validação explícita + struct tags |
| Configuração fail-fast (REF-P5) | `ValidateOnStart` | Schema Zod no env | `pydantic-settings` | `@Validated @ConfigurationProperties` | Parse do env na `main` |
| Fronteira testada (REF-P1) | Testes sobre `.csproj` | `eslint-plugin-boundaries`, dependency-cruiser | `import-linter` | ArchUnit | `go vet` + regras de `internal/` |
| Contrato com o front | OpenAPI (Swashbuckle) | OpenAPI do Nest → orval/openapi-zod-client | OpenAPI nativo → cliente gerado | springdoc → cliente gerado | oapi-codegen |

Critérios de escolha da stack de backend estão em §10.4.

## 8. Fullstack e monorepo

Escopo: tipo `fullstack`, `monorepo` · responsabilidades organização, contratos, desenvolvimento local, CI.

Fonte: EMP-MONO.

### 8.1 O que a referência faz

```text
backend/    .NET (sln próprio, docker-compose, Dockerfile, .env.example)
frontend/   Next.js + pnpm (package.json, lint e prettier próprios)
infra/      Terraform (bootstrap + módulos)
docs/       SDD, 20 ADRs, requisitos, features/<id>/{prd,design,spec,tasks}.md
.github/    workflows reutilizáveis, composite actions, scripts (detect-changes.sh)
.claude/    CLAUDE.md, agents, skills, evals
```

É um monorepo de **pastas lado a lado**: não tem ferramenta de workspace na raiz (pnpm workspace, Nx, Turbo), nem
versões, lint ou `.editorconfig` centralizados. Cada stack se gerencia sozinha.

### 8.2 Padrões que valem

| Id | Padrão | Onde | Por quê |
| --- | --- | --- | --- |
| REF-M1 | CI seletivo por diff de três pontos contra o merge-base, com base desconhecida contando como "tudo mudou" | `.github/scripts/detect-changes.sh`, `main-deploy.yml` | Workflows reutilizáveis não aceitam filtro `paths`; erra para o lado seguro |
| REF-M2 | Imagem "prebuilt" a partir do artefato que passou nos testes; script de migração idempotente como artefato | `backend/Dockerfile`, `ci-backend.yml` | Os mesmos bits testados vão para produção |
| REF-M3 | Ambiente local "infra no Docker, aplicações no host", com healthchecks e `depends_on: service_healthy` | `backend/docker-compose.yml`, README | Depuração nativa sem perder dependências reais |
| REF-M4 | Documentação de decisão junto do código (ADR, spec por feature) e um único Harness de IA que enxerga as três stacks | `docs/`, `.claude/` | Mudança atômica entre API, front e infra com o contexto completo |
| REF-M5 | `permissions: {}` por padrão nos workflows e `concurrency` cancelando só em PR | `.github/workflows/*` | Menor privilégio no CI |

### 8.3 Lacunas da referência

1. **Contrato duplicado à mão** (a maior): o backend expõe OpenAPI, e o frontend reescreve os schemas em Zod. O ADR
   0009 registra três bugs reais de deriva. O monorepo tem tudo para gerar e conferir o contrato no mesmo PR e não faz.
2. Sem `Directory.Packages.props`, `Directory.Build.props` com analyzers, `.editorconfig` e `global.json` na raiz.
3. BFF morto fora do CI; README de mais de 1.000 linhas; documentação em três lugares com deriva comprovada.

### 8.4 Estrutura recomendada para fullstack

```text
project/
├── apps/
│   ├── web/                  # React ou Angular (§3, §5)
│   └── api/                  # backend (§6 ou §7)
├── packages/
│   └── contracts/            # fonte única: OpenAPI gerado pela API, ou schemas compartilhados
├── infra/                    # IaC
├── docs/                     # ADRs e specs
├── scripts/
├── .github/workflows/        # CI seletivo (REF-M1)
└── README.md                 # curto; detalhe em docs/
```

Regras: o contrato é **gerado** a partir de uma fonte única e o CI falha se o gerado divergir do versionado; versões
centralizadas por ecossistema (`Directory.Packages.props`, `pnpm` catalog ou workspace); ferramenta de workspace só
quando há pacote compartilhado de verdade. O nome das pastas (`apps/`, `packages/`) é convenção, não exigência:
EMP-MONO usa `backend/` e `frontend/` e funciona.

### 8.5 Monorepo ou vários repositórios

| Use monorepo quando | Use repositórios separados quando |
| --- | --- |
| Front e back mudam juntos e são da mesma equipe | Ciclos de release independentes ou equipes diferentes |
| Há contrato a gerar e conferir no mesmo PR | A API serve vários clientes que não são do mesmo time |
| Um Harness de IA precisa enxergar as duas pontas | Requisitos de acesso diferentes (código de terceiros, compliance) |
| CI seletivo é viável | O repositório ficaria grande demais para clonar e indexar (EZM-ANY já tem ~1.400 arquivos sozinho) |

EZM-CRM e o seu backend estão em repositórios separados; EMP é monorepo. As duas escolhas são legítimas.

## 9. Harness de IA nas referências

Escopo: qualquer tecnologia · responsabilidade AI Engineering, agentes, skills, rules, hooks, evals.

As referências da empresa e o EMP já trazem Harness versionado. Esta seção é a base para o Spock gerar Harness
específico por stack.

| Elemento | Onde | O que vale | O que evitar |
| --- | --- | --- | --- |
| `CLAUDE.md` âncora, curto | EZM-ANY, EZM-CRM, EMP | Regras de camada, YAGNI, ponte entre ciclo e agentes | Afirmar o que o código não faz (EMP cita namespace e OData inexistentes) |
| Skill de conhecimento da stack | `integrador-backend-skill`, `easymart-frontend-skill`, `frontend-skill`, `backend-skill` | Convenções levantadas do código, anti-padrões, checklist, "inconsistência conhecida" registrada como fato | Skill de ~50 mil tokens com changelog de feature (EZM-ANY) |
| Agentes agnósticos de stack | `.claude/agents/` (architect só leitura, implementer, verifier, code-reviewer, test-engineer, debug, performance) | **Autor diferente do verificador**; arquiteto sem escrita | Agente que duplica conhecimento da skill |
| Regras por caminho | EZM-ANY `.claude/rules/backend-code-structure.md` (carregada quando um `.cs` é tocado) | Contexto só quando relevante | — |
| Specs por feature | `.specs/features/<id>/{spec,design,tasks,validation}.md`; `docs/features/<id>/` | Ciclo spec-driven com validação | — |
| Decisões numeradas | `.specs/STATE.md` (~91 AD-NNN em EZM-ANY), ADRs em EMP | Rastreabilidade de por quê | — |
| Vocabulário de evidência | EZM-ANY `harness-contract` (FACT/INFERENCE e confiança) | O agente diz o que viu e o que deduziu | — |
| Lint/eval do Harness no CI | EMP `.claude/evals/check-harness.mjs`; EZM-ANY `harness_lint/` | Harness quebrado falha o build | Eval que não confere afirmações factuais contra o código |
| MCP local por projeto | EZM-ANY `.mcp.json` (banco, sandbox do fornecedor) | Ferramentas no escopo do projeto | Credenciais de produção |
| Permissões | `settings.json` com allowlist; `deny Read` em appsettings reais (EZM-ANY) | Menor privilégio | — |

Lição central: **separar conhecimento da stack (skill curta, estável, verificada) de agentes agnósticos**, e manter o
detalhe de feature nas specs, não na skill.

## 10. Diretrizes de decisão

Cada diretriz é um ponto de partida; a decisão final depende dos requisitos e é registrada como decisão do projeto.

### 10.1 React ou Angular

| Escolha | Quando |
| --- | --- |
| React | Equipe com experiência React; produto que precisa de SSR/SEO (Next) ou site de conteúdo; reaproveitar `@mariliz/ui-kit`; ecossistema de bibliotecas amplo |
| Angular | Equipe com experiência Angular; aplicação corporativa grande com formulários pesados; preferência por framework completo e opinativo (DI, roteador, formulários, HTTP no núcleo); continuidade com projetos Angular da organização (EZM-CRM) |
| Manter o que existe | Projeto existente: não trocar de framework por preferência. Trocar só com problema concreto que o framework atual não resolve |

### 10.2 Quando usar o `@mariliz/ui-kit`

Usar quando: React 18.2+/19 e MUI 9 compatíveis; produto do ecossistema Mariliz ou que aceita a identidade dele;
aplicação desktop/web de formulários e tabelas. Não usar nos casos da §4.4. Em projeto existente com outro design
system, não migrar só para padronizar.

### 10.3 Camadas e contratos

- Frontend: `app → features → shared`, verificado por lint (REF-R1); contrato validado na fronteira (REF-R3).
- Backend: dependências para dentro, verificadas por teste (§6.2); casos de uso em slice; um modelo de erro.
- Fullstack: contrato gerado de fonte única (§8.4).
- Responsabilidade de cada camada: domínio guarda invariantes; aplicação orquestra casos de uso sem saber de HTTP;
  infraestrutura implementa portas; host compõe e traduz para o protocolo.

### 10.4 .NET ou outra tecnologia

| Fator | Favorece .NET | Favorece outra |
| --- | --- | --- |
| Conhecimento da equipe | Equipe .NET (caso da organização) | Equipe sem .NET |
| Domínio com regras e transações | EF Core, pipeline, tipagem forte | — |
| Integrações corporativas (ERP, filas, AWS) | Polly, SDKs maduros, referência EZM-ANY pronta | — |
| IA/ML, ciência de dados | — | Python (ecossistema de ML e LangGraph) |
| Front e back na mesma linguagem, BFF fino | — | Node/TS |
| Serviço muito pequeno, cold start crítico | Native AOT ajuda | Go |
| Custo operacional | Contêiner único e eficiente | Equivalente |

O Spock não deve recomendar .NET por padrão: deve listar os fatores do projeto e mostrar qual coluna eles favorecem.

### 10.5 Monorepo

Ver §8.5.

### 10.6 Como evitar acoplamento indevido

- Dependência só para dentro; contexto conversa com outro pela `Application` publicada, nunca pela infraestrutura.
- Contrato do fornecedor fica no cliente de integração (REF-N1).
- `shared` não conhece domínio (frontend); `core` não conhece `features` (Angular).
- Efeito externo depois do commit (REF-P4).
- Toda fronteira importante tem teste ou lint (REF-P1).

### 10.7 Escolher padrão pelo contexto

| Contexto | Leve | Completo |
| --- | --- | --- |
| CRUD sem regra | Modelo anêmico, validação, um serviço | — |
| Máquina de estados, capacidade, concorrência | — | Agregado rico, `rowversion`, eventos |
| Uma integração simples | Typed client | — |
| Integração multi-tenant com cota | — | REF-N1 a REF-N8 |
| Um banco | `ITransactional` | — |
| Vários bancos ou processos | — | Outbox, sagas |
| Um domínio coeso | Camadas únicas | — |
| Vários subdomínios e hosts | — | Contextos delimitados |

### 10.8 Modernização de projeto existente

1. Descrever o que existe com evidência (camadas reais, não as declaradas; testes; riscos).
2. Classificar cada divergência em: **risco** (segurança, perda de dado, corrida), **custo** (bug recorrente,
   lentidão de entrega) ou **estilo**. Só risco e custo viram recomendação.
3. Proteger antes de mudar: testes de caracterização e testes de fronteira (REF-P1) no estado atual.
4. Mudar em passos pequenos e reversíveis (strangler), um padrão por vez, cada um com decisão registrada.
5. Nunca propor reescrita completa sem: custo estimado, risco de regressão, plano de convivência e aprovação do dono.

## 11. Exemplos de uso pelo Spock

| Pedido | Seções consultadas | Saída esperada |
| --- | --- | --- |
| "Criar painel administrativo React com login e tabelas" | §3, §4, §2 | Next ou Vite conforme §3.4; estrutura §3.1; kit para tabelas e formulários; REF-R3/R5/R6; lacunas do kit tratadas com MUI direto |
| "Analisar este backend .NET legado" | §6, §2, §10.8 | Camadas reais × declaradas; riscos do tipo §6.11; plano incremental, sem reescrita |
| "Novo conector com marketplace" | §6.9, §6 | REF-N1 a REF-N9; idempotência e cota desde o início |
| "Preparar o Harness deste projeto Angular" | §9, §5 | `CLAUDE.md` curto; skill Angular com REF-A1..A11 e a lista "não copiar" como anti-padrões; agentes agnósticos |
| "Front React + API .NET no mesmo repositório" | §8, §3, §6 | Estrutura §8.4 com contrato gerado e CI seletivo |

## 12. Manutenção do catálogo

- **Quando revisar:** novo commit relevante numa referência (comparar com a coluna "commit" da §1); referência nova;
  decisão de projeto que contradiz um padrão (registrar a exceção, não apagar o padrão).
- **Como revisar:** atualizar a linha da §1 (branch, commit, data), refazer a leitura só das áreas que mudaram
  (`git diff <commit-antigo>..<novo> --stat`), e registrar no fim deste arquivo o que mudou.
- **Identificadores** (`REF-*`) nunca são reaproveitados; padrão abandonado fica marcado como "retirado" com o motivo.
- **Frescor:** um agente que usa um padrão deve citar o identificador e a data da análise; se a data tiver mais de 6
  meses, conferir a fonte antes de recomendar.

### Histórico

| Data | Mudança |
| --- | --- |
| 2026-11-17 | Primeira versão: 8 referências (EMP-FE, EMP-BE, EMP-MONO, MLZ-WEB, MLZ-UI, EZM-CRM, EZM-ANY, EZM-DOC) |
