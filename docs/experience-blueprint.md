# Mariliz AI Engineering — Experience Blueprint

**Fase:** 0. **Data:** 2026-10-25. Rótulos **[FATO]**, **[INFERÊNCIA]**, **[HIPÓTESE]** e **[DECISÃO PENDENTE]** conforme [`product-discovery.md`](product-discovery.md).

---

## 1. Perfis de usuário

| Perfil | O que quer | O que não sabe (e não precisa saber) | Jornada típica |
| --- | --- | --- | --- |
| Gestor / product owner | Transformar requisitos em backlog e roadmap revisáveis | Harness, MCP, agentes | Importar requisitos |
| Arquiteto / tech lead | Fixar padrões do projeto e garantir que agentes os respeitem | Detalhes de empacotamento | Configurar projeto |
| Desenvolvedor | Ter o harness funcionando no repositório sem montá-lo à mão | Tudo que o discovery detecta sozinho | Configurar projeto, Conectar ferramentas |
| Equipe de modernização | Entender o legado antes de reescrever | — | Modernizar sistema |
| Usuário avançado / CI | Automatizar as mesmas operações | — | CLI (`mariliz discover/plan/apply/validate`) |

---

## 2. Jornadas principais

Cada jornada tem fluxo, validações e critério de conclusão próprios. Compartilham o motor, os contratos e a tela de execução.

| Jornada | Exige pasta de projeto? | Quando pede a pasta | MVP |
| --- | --- | --- | --- |
| **Configurar projeto** | Sim | Ao clicar no cartão, se nenhuma estiver selecionada | **Sim** |
| **Importar requisitos** | Não para analisar | Antes de persistir artefatos | Fase 3 |
| **Conectar ferramentas** | Não para autenticar | Antes de gravar configuração no projeto | Fase 4 |
| **Modernizar sistema** | Sim (legado); destino opcional | Ao clicar | Fase 6 |
| Migrar informações | Não | — | Fase 6, aberta a partir de "Conectar ferramentas" |
| Acompanhar qualidade | Sim | — | Fase 7 |

---

## 3. Navegação proposta

```
Início (janela inicial)
 ├── Configurar projeto ── Contexto → Análise → Configuração → Revisão → Aplicar → Resultado
 ├── Importar requisitos ─ Fontes → Extração → Revisão → Destino → Aplicar → Resultado
 ├── Conectar ferramentas  Serviço → Autenticação → Verificação → Permissões → Salvar → Resultado
 ├── Modernizar sistema ── Fontes do legado → Inventário → Lacunas → Plano → Resultado
 └── Configurações (menu discreto: tema, diretório de dados, sobre, diagnóstico)
```

- Sem menu lateral. A navegação é linear dentro da jornada, com **Voltar** sempre disponível até "Aplicar".
- Rotas internas (React Router, modo SPA): `/`, `/projeto/configurar/:etapa`, `/requisitos/:etapa`, `/conexoes/:etapa`, `/modernizacao/:etapa`, `/execucao/:runId`, `/configuracoes`.
- Um rascunho de jornada pode ser salvo no motor e retomado; voltar ao Início não perde o rascunho.

---

## 4. Fluxo de onboarding (jornada Configurar projeto)

| Etapa | O que o motor faz | O que o usuário vê e decide | Origem da informação |
| --- | --- | --- | --- |
| **Contexto** | — | Objetivo ("configurar harness", "revisar harness existente"); pasta | Declarado |
| **Análise** | Discovery determinístico: manifests, solução, `package.json`, estrutura, CI, `.claude/`, `.mcp.json`, `mariliz.yaml` existente | Lista do que foi detectado, com a evidência (arquivo) de cada item e as ambiguidades | Descoberta automática |
| **Configuração** | Recomenda profile(s), agentes, skills e gates | Só as perguntas que o discovery não respondeu (ex.: "o comando de testes é `dotnet test` na solução X?"); capacidades opcionais com explicação curta | Declarado + inferido (rotulado) |
| **Revisão** | Gera o plano: arquivos a criar/alterar, diff, conflitos, permissões pedidas, riscos, validações previstas | Diff por arquivo; conflitos com escolha explícita (manter o meu / aceitar o proposto / mesclar manualmente); **Gerar plano**, **Salvar rascunho**, **Aplicar configuração** | — |
| **Aplicar** | Executa o plano aprovado, de forma idempotente, com backup do que foi alterado | Progresso real por etapa | — |
| **Resultado** | Valida (lint Tier 0, gates do profile se o usuário permitir executá-los) | Relatório: o que foi feito, evidência, pendências, limitações | — |

Regras:

- Perguntas condicionais: sem perguntas sobre integrações ou stacks fora do objetivo escolhido.
- Repetir o fluxo no mesmo projeto não duplica nada: o motor compara com `mariliz.lock` e mostra "nada a fazer" quando não há diferença.
- Uma inferência (ex.: "parece Clean Architecture") nunca altera permissões nem instala integrações; aparece como sugestão a confirmar.

---

## 5. Estados e transições

Os estados são emitidos pelo motor (evento `run.state`); a UI não inventa estado.

```
Aguardando configuração ──(dados mínimos)──▶ Analisando ──▶ Aguardando aprovação
        ▲                                         │                  │
        └──────────────(usuário edita)────────────┴──────────────────┤ aprovar
                                                                     ▼
          Requer intervenção ◀──(conflito/credencial)── Executando ──▶ Validando
                 │                                          │            │
                 └──(usuário resolve)──▶ Executando         ▼            ▼
                                                         Falhou    Concluído | Concluído com limitações
```

| Estado | Ações disponíveis |
| --- | --- |
| Aguardando configuração | Editar, Voltar |
| Analisando | Cancelar |
| Aguardando aprovação | Voltar, Salvar rascunho, Aplicar |
| Executando | Cancelar (o motor conclui o passo atômico corrente e para) |
| Validando | — |
| Concluído | Abrir pasta, Ver relatório, Início |
| Concluído com limitações | Ver limitações, Ver relatório |
| Falhou | Tentar novamente (retoma do último checkpoint), Ver detalhes, Desfazer (quando houve backup) |
| Requer intervenção | A ação pedida pelo motor (ex.: resolver conflito) |

---

## 6. Importação de documentos (Fase 3)

- Seleção por diálogo nativo ou arrastar e soltar na janela. O host entrega ao motor só os caminhos escolhidos pelo usuário.
- O motor copia para um diretório temporário controlado, processa e apaga conforme a retenção. O original nunca é alterado nem copiado para o repositório.
- A revisão mostra cada item com rótulo **Extraído** (com referência à fonte e à página/trecho), **Inferido** ou **Proposto pela IA**. O usuário aprova, edita ou rejeita item a item.
- Antes de qualquer envio a um provedor de modelo, um aviso diz o que será enviado e para onde; sem autorização, a etapa não roda.
- Formatos: só os que a Fase 3 implementar e testar. A lista aparece no diálogo; nenhum formato é anunciado antes de ter teste.

---

## 7. Origens e destinos (Fase 6)

Fluxo de migração: origem → autenticação → entidades → destino → mapeamento → incompatibilidades → prévia → simulação → aprovação → execução com checkpoints → reconciliação → relatório de divergências. A tela de mapeamento usa `DataTable` da Synthra com uma linha por campo (origem, destino, transformação, status). Escritas exigem aprovação explícita por execução.

---

## 8. Configuração de integrações (Fase 4)

Cartão por serviço com: finalidade, recursos, método de autenticação, permissões necessárias, operações suportadas, limitações e um **estado de verificação em quatro níveis**, cada um exibido separadamente:

1. Configuração válida (formato)
2. Autenticado (identidade confirmada)
3. Conectado (alcançou o serviço)
4. Operações verificadas (lista do que foi testado de fato)

Uma integração só aparece como "pronta" no nível 4. Credenciais vão direto para o cofre do sistema operacional; a UI nunca as exibe de volta. Bancos de dados começam somente leitura e o modo de escrita não existe no MVP.

---

## 9. Aprovação de alterações

A tela de Revisão é obrigatória antes de qualquer escrita no projeto ou em serviço externo. Ela mostra:

- Resumo em uma frase.
- Arquivos afetados (novo, alterado, removido) com diff.
- Conflitos com arquivos existentes e a escolha do usuário para cada um.
- Permissões que o harness gerado concede a agentes (ex.: `Edit`, `Bash(dotnet test:*)`).
- Riscos e o que é irreversível.
- Validações que rodarão depois.

O botão **Aplicar configuração** só fica habilitado quando todos os conflitos têm escolha.

---

## 10. Apresentação de resultados

Relatório final com: estado final, etapas com duração, arquivos alterados, validações executadas e seus resultados reais, pendências, limitações, e o caminho do relatório completo (salvo no histórico operacional, fora do repositório). Nenhum "sucesso" aparece sem a validação correspondente ter rodado.

---

## 11. Janela inicial

### 11.1 Wireframe (seção 5.4 do briefing)

```
┌──────────────────────────────────────────────────────────────────┐
│ [Marca] Mariliz AI Engineering                         [—] [□] [×]│
│        Engenharia de software assistida por IA                    │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Como podemos ajudar?                                            │
│  Escolha uma atividade para começar. Vamos configurar o ambiente │
│  conforme as necessidades do seu projeto.                         │
│                                                                  │
│  ┌─────────────────────────┐  ┌────────────────────────────────┐  │
│  │ [ícone] Configurar      │  │ [ícone] Importar requisitos   │  │
│  │ projeto                 │  │                                │  │
│  │ Analisar o repositório  │  │ Gerar especificações, backlog │  │
│  │ e configurar o Harness.│  │ e roadmap.                    │  │
│  └─────────────────────────┘  └────────────────────────────────┘  │
│                                                                  │
│  ┌─────────────────────────┐  ┌────────────────────────────────┐  │
│  │ [ícone] Conectar        │  │ [ícone] Modernizar sistema    │  │
│  │ ferramentas             │  │                                │  │
│  │ Jira, Azure DevOps,     │  │ Analisar um legado e planejar │  │
│  │ GitHub, MCP e bancos.  │  │ sua modernização.             │  │
│  └─────────────────────────┘  └────────────────────────────────┘  │
│                                                                  │
│  PROJETO                                                         │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ [pasta] Selecionar pasta do projeto                 [  > ]  │  │
│  │         Os arquivos permanecem no computador.               │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

Textos dos cartões (completos, sem truncar):

| Ordem | Título | Descrição |
| --- | --- | --- |
| 1 | Configurar projeto | Analisar o repositório e configurar o Harness. |
| 2 | Importar requisitos | Gerar especificações, backlog e roadmap a partir de documentos ou ferramentas existentes. |
| 3 | Conectar ferramentas | Configurar Jira, Azure DevOps, GitHub, MCPs e bancos de dados. |
| 4 | Modernizar sistema | Analisar um legado e planejar sua modernização. |

Janela: 900 × 650 inicial, mínimo 760 × 560, redimensionável, não maximizada, barra de título nativa (`decorations: true` no `tauri.conf.json`).

### 11.2 Mapeamento para componentes reais

Fonte: `@synthra.io/ui-kit` 0.5.0, `dist/index.d.ts` inspecionado em 2026-10-25. **[FATO]** A biblioteca exporta: `Provider`, `initializeTheme`, tokens (`light`, `dark`, `spacing`, `borderRadius`, `fontSizes`, `fontWeights`, `fonts`, `lineHeights`, `letterSpacing`, `opacity`, `typography`), `Alert`, `Autocomplete`, `AutocompleteField`, `Avatar`, `Breadcrumb`, `Button`, `CardData`, `Checkbox`, `CheckboxFormField`, `DataTable`, `Divider`, `Drawer`, `FormProvider`, `LoadingBar`, `Menu`, `MenuItem`, `Modal`, `SelectField`, `SelectFormField`, `Switch`, `TabBar`, `TabItem`, `TableComponent`, `TextField`, `TextFormField`, `Typography`, `useFormContext` e 426 ícones.

**[FATO]** A biblioteca **não** exporta `Card` genérico, `Stepper`, `Chip`, `Tooltip` nem ícones de pasta, código, documento ou plug. `CardData` é um cartão de pares rótulo/valor e não serve como cartão de ação.

| Elemento | Componente | Origem | Observação |
| --- | --- | --- | --- |
| Tema e reset | `Provider` + `initializeTheme({ mode, overrides })` | Synthra | Mesmo padrão do Mariliz Web (`src/shared/components/providers/theme.ts`): declarar **todos** os tons nos overrides, senão sobra o azul padrão da biblioteca |
| Marca + título + subtítulo | `Typography` (variantes `h5`/`body2`) + SVG da marca | Synthra + asset próprio | |
| "Como podemos ajudar?" | `Typography` `h4` | Synthra | |
| Texto de apoio | `Typography` `body1`, cor `text.secondary` | Synthra | |
| Grade 2 × 2 | CSS Grid em SCSS Module | Próprio | Uma coluna abaixo de ~620 px de largura de conteúdo |
| Cartão de ação | `Card` + `CardActionArea` | **MUI** (peer da Synthra, já instalado) | Wrapper `ActionCard` em `packages/ui`; `CardActionArea` renderiza `<button>` |
| Ícone "Importar requisitos" | `BookUploadIcon` | Synthra | |
| Ícone "Conectar ferramentas" | `UserConnectionsIcon` (alternativa: `ShareIcon`) | Synthra | Semântica aproximada |
| Ícone "Modernizar sistema" | `UpdateIcon` | Synthra | |
| Ícone "Configurar projeto" | **Inexistente** (provisório: `ListViewRectangleIcon`) | — | **[DECISÃO PENDENTE]** contribuir `FolderCodeIcon` na Synthra |
| Rótulo "PROJETO" | `Typography` `overline` | Synthra | |
| Linha do seletor | `Card` variant outlined + `CardActionArea` | MUI | Mesmo wrapper `ActionCard`, variante `row` |
| Ícone de pasta | **Inexistente** | — | **[DECISÃO PENDENTE]** contribuir `FolderIcon` na Synthra |
| Indicador ">" | `ArrowRightIcon` | Synthra | Já usado no Mariliz Web |
| Carregamento | `LoadingBar` | Synthra | Durante a abertura do diálogo e o discovery inicial |
| Mensagens de erro e aviso | `Alert` | Synthra | |
| Wizard (progresso) | `Stepper` | MUI | Wrapper `WizardSteps` em `packages/ui` |
| Formulários de configuração | `FormProvider`, `TextFormField`, `SelectFormField`, `CheckboxFormField` + Zod | Synthra + `react-hook-form` + `zod` | Mesmo padrão do formulário de contato do Mariliz Web |
| Confirmações | `Modal` | Synthra | |
| Detalhes da execução | `Drawer` | Synthra | |
| Tabelas (arquivos afetados, mapeamentos) | `DataTable` | Synthra (usa `@mui/x-data-grid`) | Carregar só nas telas que usam |
| Diff de arquivo | **Inexistente** | — | Componente próprio na Fase 1b; avaliar biblioteca leve |

### 11.3 Estados e interações da tela inicial

| Elemento | Estados | Comportamento |
| --- | --- | --- |
| Cartão de jornada | normal, hover (borda e fundo `action.hover`), foco visível (anel com `primary.main`, contraste ≥ 3:1), pressionado, carregando (`LoadingBar` no topo do cartão), desabilitado | Clique ou Enter/Espaço inicia a jornada. No MVP, os cartões 2 a 4 ficam desabilitados com o selo "Em breve" visível no próprio cartão (não em tooltip) |
| Seletor de projeto | vazio, abrindo diálogo, selecionado, erro (pasta inacessível) | Abre o diálogo nativo via comando Tauri; ao selecionar, mostra o nome da pasta e o caminho abreviado (ex.: `~\…\MyApps\backend\EmpregaNetAPI`), com ação "Trocar" |
| Cartão "Configurar projeto" sem pasta | — | Abre o diálogo de pasta e, após a escolha, segue para a jornada |

**Validações:** a pasta precisa existir, ser legível e não ser uma raiz de sistema (`C:\`, `/`). O motor faz essa validação; a UI só exibe o resultado.

**Acessibilidade:**

- Ordem de tabulação: cartões 1→4, depois o seletor de projeto.
- Cada cartão é um único `<button>` com nome acessível = título e `aria-describedby` = descrição.
- Cartões desabilitados usam `aria-disabled` e continuam focáveis, para que o leitor de tela anuncie "Em breve".
- Ícones decorativos com `aria-hidden`.
- Contraste de texto ≥ 4,5:1 e de bordas de controles ≥ 3:1, verificado nos dois temas (o Mariliz Web já documenta esse padrão em `docs/DESIGN-SYSTEM.md`, seção "Contraste").
- Respeita `prefers-reduced-motion`; nenhuma animação além de transições de estado.
- Testes: `eslint-plugin-jsx-a11y` (como no Mariliz Web) + testes de componente com Testing Library verificando papel, nome e navegação por teclado.

### 11.4 Identidade visual

**[FATO]** O Mariliz Web define a paleta da marca (`brand-palette.ts`: teal `#33C1BA`, tinta `#050C0C`, neutros esverdeados) sobre a Synthra e aplica a fonte Geist variante a variante. Recomendação: reaproveitar a mesma paleta e a mesma abordagem de overrides, publicadas como pacote interno ou copiadas com origem anotada, para que site e aplicativo tenham a mesma identidade. **[DECISÃO PENDENTE]** tema padrão (claro, escuro ou seguir o sistema).

---

## 12. Telas de revisão e execução

**Revisão** (layout `WizardLayout`): `Stepper` no topo; à esquerda a lista de arquivos (`DataTable` compacta com tipo da mudança e conflito); à direita o diff do arquivo selecionado; abaixo, blocos `Alert` para riscos e permissões; rodapé fixo com **Voltar**, **Salvar rascunho**, **Gerar plano** ou **Aplicar configuração**, conforme a etapa.

**Execução** (rota `/execucao/:runId`): lista de etapas emitidas pelo motor (`step.started`, `step.completed`, `step.failed`) com estado e duração; etapa atual com `LoadingBar` indeterminada (o motor não fornece porcentagem que não consiga medir); erros já sanitizados pelo motor; **Cancelar**, **Tentar novamente** e **Detalhes** (abre `Drawer` com o log da execução). A tela pode ser fechada e reaberta: o estado vem do motor, não da memória da UI.
