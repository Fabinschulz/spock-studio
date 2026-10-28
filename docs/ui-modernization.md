# Mariliz Studio — Modernização da interface

Registro do que foi construído, do que foi validado e do que **não** foi feito. Complementa o [blueprint de experiência](experience-blueprint.md) e a [Fase 1B](phase-1b-configure-project.md).

## 1. Marca e ícone

O produto passou a se chamar **Mariliz Studio**. A marca vem do projeto Mariliz (site), sem reinterpretação:

| Elemento | Origem |
| --- | --- |
| Marca: dois arcos de "m" em `#f1f7f6` sobre o tile ink `#050c0c`, com o nó teal `#33c1ba` | `favicon.svg` do site |
| Wordmark "mariliz" monoline, pingos dos "i" em teal | `Logo` do site |
| Paleta | `brandPalette` do site (teal `#33c1ba`, ink `#050c0c`, neutros tingidos de turquesa) |
| Tipografia | Geist (já usada pelo site) |

O ícone do aplicativo usa a geometria exata do favicon em 1024 px (`apps/desktop-host/src-tauri/icons/source/mariliz-studio.svg`); o conjunto (`.ico`, `.icns`, PNGs do Windows) é gerado por `pnpm tauri icon`. O PNG-fonte é renderizado localmente, sem ferramentas externas. O nó teal funciona como o "nó" que a Mariliz já usa nos "i" do wordmark.

O nome mudou em: título da janela e `productName` do Tauri (o instalador passa a se chamar "Mariliz Studio"), `index.html`, configuração da UI, Cargo, os textos que o motor grava nos projetos ("Managed by Mariliz Studio") e os títulos dos documentos. **Não mudaram:** o identificador do pacote (`dev.mariliz.ai-engineering`, para não deslocar dados locais), os nomes de projeto e namespaces (`Mariliz.*`) e a CLI (`mariliz`).

## 2. Tokens

Mesma estrutura em camadas do site (primitivos → semânticos → componentes), estendida com superfícies em níveis (`surface`, `surface-raised`, `surface-sunken`), estados do botão primário (`primary-hover/active`), `hairline`, halo de destaque, alturas de controle (`--control-height` 40 px, `--control-height-lg` 44 px, `--target-min` 24 px), raios e sombras mínimas. O tema segue o sistema operacional (claro e escuro).

**Contraste validado, não escolhido pela aparência.** `src/shared/styles/tokens.contrast.test.ts` lê o próprio `_tokens.scss`, resolve as referências, compõe transparências sobre a superfície adjacente e confere 54 pares nos dois temas: texto principal ≥ 7:1; texto secundário, texto na cor da marca, texto sobre o botão primário (normal, hover e ativo), selos (5 tons) e erro/sucesso ≥ 4,5:1; borda de controle, anel de foco e indicador da etapa atual ≥ 3:1. O teste encontrou um defeito que já existia: no tema claro, a borda dos controles sobre a superfície dava 2,90:1; foi corrigida (`neutral-500`).

Além do teste de tokens, um auditor rodado no navegador calculou o contraste **efetivo** (cor computada, com transparência, contra o fundo real) de cada texto visível em todas as telas e etapas, nos dois temas, a 900×650 e 760×560: nenhum texto abaixo de 4,5:1 (3:1 para texto grande).

## 3. Componentes

**Reutilizados da Synthra:** `Provider`/tema, `Button` (todas as ações), `Alert` (só para erro bloqueante e para o resultado da aplicação), `Modal` (confirmação de reversão) e ícones (`ArrowRightIcon`, `CopyIcon`, `Home01Icon`, `LibraryIcon`, `SettingIcon`, `ListViewRectangleIcon`, `BookUploadIcon`, `UserConnectionsIcon`, `UpdateIcon`).

**A Synthra não tem** tooltip, accordion/disclosure, stepper, card, badge, ícone de pasta nem ícones com `currentColor` para avisos (os de severidade trazem cor fixa e destoavam dos tons do sistema). Esses foram compostos sobre os tokens:

| Componente | Função |
| --- | --- |
| `AppShell` (+ `SideNav`, `EngineStatus`) | Menu lateral com a marca, navegação e o estado **real** do motor (comando e evento `engine_status` do host); encolhe para ícones em janela estreita; atalho "Ir para o conteúdo" |
| `PageHeader` | h1, trilha de navegação e texto de apoio |
| `SectionCard` | Bloco de informação com título próprio (variante `accent` para o cartão principal) |
| `WizardSteps` | Stepper com estados concluída/atual/pendente/erro/bloqueada, em texto e forma; modo compacto por *container query* |
| `ActionCard` | Atividade, com variante primária e estado indisponível |
| `InlineNotice` | Aviso proporcional (info, aviso, erro, sucesso); só o erro é anunciado |
| `CommandDisplay` | Comando técnico com cópia; detectar não executa |
| `EvidenceDisclosure` | `<details>` nativo com contagem; abre por teclado e mouse |
| `PrimaryActionFooter` | Ações de navegação da etapa, `sticky` no rodapé |
| `ProjectPicker` / `RecentProjects` | Seletor de pasta com todos os estados e a lista de recentes |
| `Checklist` | Lista com marca (ok, atenção, informativo); o estado também é dito em texto |

`EvidenceList` virou `EvidenceDisclosure`; os alertas não bloqueantes (plano pelas regras-base, limitações, conflito, ambiente sem suporte, aviso de Git) viraram `InlineNotice`.

## 4. Conformidade com o modelo (mockup)

A interface foi conferida contra o modelo, tela a tela, a 900×650. O que está igual, o que mudou de forma e o que **não** foi feito:

| Elemento do modelo | Situação |
| --- | --- |
| Menu lateral estreito (Início, Projetos, Catálogo, Configurações) e "Sistema local · Pronto para usar" | Igual. O estado vem do host; fora do aplicativo diz "Prévia no navegador" |
| Ícones coloridos por atividade (teal, índigo, azul, laranja) | Igual, com tons nos tokens e contraste glifo/fundo ≥ 3:1 testado |
| Atividade principal com borda e halo | Igual. As três indisponíveis ganharam **borda tracejada** e o selo "Em breve" (o modelo as mostra como ativas; o requisito de estado indisponível evidente prevaleceu) |
| Bloco à direita do título ("Mais que ferramentas, uma engenharia inteligente.") com chevrons | Incluído. O texto é real e os chevrons são ornamento escondido do leitor de tela; só aparece quando sobra largura |
| **Projetos recentes** (cartões com nome e data) | **Implementado de verdade**, no host (ver abaixo) |
| Selo "TLC (padrão)" no cabeçalho do assistente | Selo **somente leitura** (sem seta): não há outro kit para escolher |
| Trilha "Projetos › Configurar projeto" | "Início › Configurar projeto": não existe uma página "Projetos" |
| Stepper com os seis rótulos | Igual em 900 px; abaixo de ~640 px de largura útil vira marcadores + "Etapa N de 6" |
| Etapa Projeto: contexto da pasta + "O que vamos analisar?" | Incluído (pasta selecionada com "Trocar pasta" e a lista do que o motor lê) |
| Etapa Método: cartão do TLC (recursos, versão, dependências), ambientes e resumo | Mesmo conteúdo e ordem, com **dados reais**: versão detectada e se foi validada, Node/Python/Git encontrados, ambientes detectados. "Compatível" só aparece se a versão foi validada por esta fase. **Diferença:** o "Resumo da configuração" fica **abaixo** do cartão (coluna única), não em coluna à direita como no modelo |
| Ambiente "Outros" | Não incluído (não existe) |
| Tools/MCPs na lista "O que será configurado" | Dito como **"nenhum é instalado nesta jornada"** (o modelo sugere que são configurados) |
| "Ver todos" nos recentes, "Ver detalhes" no TLC, seletor de pasta como campo de texto | Não incluídos: sem destino, ou substituídos pelo diálogo nativo |
| Barra de título personalizada | Mantida a **nativa** do sistema, como pedido |

### Projetos recentes (host)

A UI não pode autorizar um caminho (só o diálogo nativo do host pode), então a lista mora no **host Rust**, em `recent-projects.json` no diretório de dados do aplicativo:

- Só entram pastas escolhidas pelo diálogo nativo e autorizadas pelo motor; no máximo 6, do mais recente ao mais antigo, sem duplicar.
- A UI recebe **id opaco, nome e caminho abreviado**, nunca o caminho completo. Reabrir é o comando `open_recent_project(id)`: o host só aceita ids da própria lista, confere que a pasta existe e pede ao motor que autorize de novo, com as mesmas regras de uma escolha nova.
- Pasta que sumiu aparece como "indisponível", com o motivo dito, e não pode ser aberta.
- Arquivo ausente ou corrompido vira lista vazia; a gravação é atômica (arquivo temporário + renomeação).
- Os comandos novos (`list_recent_projects`, `open_recent_project`) entram no manifesto e na capability da janela; a allowlist de métodos do motor não mudou.
- Escolher um recente só **seleciona** a pasta, como o diálogo: "Analisar projeto" é uma ação à parte.

## 5. Estados e acessibilidade

- **Seleção de pasta:** nenhuma, escolhida, cancelada (dita em texto, sem alarme), inválida/inacessível (erro anunciado com a causa do host), ocupada (diálogo aberto; segundo clique ignorado) e indisponível (fora do aplicativo). Escolher a pasta não é analisá-la: "Analisar projeto" é uma ação à parte, e a nota "análise somente de leitura" fica visível.
- **Caminho:** a UI recebe só o caminho abreviado pelo host (por privacidade). Ele é cortado visualmente com reticências, mas o texto inteiro está no documento (leitor de tela) e em `title`.
- **Análise:** indicador indeterminado (nenhum percentual inventado), erro com "Tentar de novo", avisos proporcionais (a ausência de Git é informação com a consequência prática, não erro).
- **Stepper:** `aria-current="step"`, estado de cada etapa dito em texto ("concluída", "etapa atual", "pendente", "com erro", "bloqueada") e por forma; etapas futuras **não são controles**. O erro/bloqueio vem do estado real da jornada (`stepIssue`).
- **Teclado:** ordem de foco conferida no navegador (atalho → marca → menu → cartões), itens indisponíveis focáveis com o motivo, anel de foco de 2 px em todos os controles, `<details>` e botões nativos.
- **Movimento:** transições curtas e `prefers-reduced-motion` respeitado.
- **Alvos:** controles de 40–44 px; mínimo técnico de 24 px nos secundários.

## 6. Responsividade

Verificada a 900×650 (referência) e 760×560 (mínimo do Tauri), nos dois temas, sem rolagem horizontal. O menu lateral (152 px) vira uma faixa de ícones abaixo de 864 px; os rótulos continuam acessíveis. O stepper usa a largura **do próprio componente** (container query): em 900 px sobram ~685 px e ele mostra os seis rótulos; abaixo de ~640 px mostra os marcadores e "Etapa N de 6 · nome". O ornamento do título também só aparece quando sobra largura. O resumo lateral da etapa de método só fica ao lado do conteúdo em janelas largas; nas estreitas empilha. O rodapé de ações é `sticky` e não cobre o conteúdo.

## 7. Validação executada

| Verificação | Resultado |
| --- | --- |
| Testes da UI (Vitest + Testing Library) | 167 passam (inclui axe-core, tokens, shell, componentes, recentes, jornada, tela inicial) |
| `tsc --noEmit`, ESLint (com `jsx-a11y`), Prettier | sem erros |
| Testes .NET / Rust | 232 + 2 / 16 passam (8 novos, dos recentes) |
| Contraste dos tokens (62 pares, incluindo os ícones coloridos) | passam |
| Contraste efetivo no navegador (todas as telas e etapas, 2 temas, 2 tamanhos) | 0 falhas |
| Navegação por teclado (ordem e foco) | conferida no navegador |
| Estado do motor | testado com a ponte do host simulada (pronto, iniciando, falha, valor desconhecido) |

**O axe-core roda em jsdom**: confere ARIA, nomes, rótulos e semântica, mas não contraste nem foco visual (por isso o auditor no navegador e a conferência manual). Uma execução sem violações **não** prova que a interface é acessível.

## 8. Limitações conhecidas

1. **Leitor de tela real** (NVDA, JAWS, Narrator) **não foi testado**; a semântica foi conferida por teste automatizado e inspeção do DOM.
2. **Zoom do sistema e alto contraste do Windows** não foram verificados.
3. Algum ícone da Synthra usado no menu lateral emite aviso do React no console (`fill-rule` em vez de `fillRule`); não identifiquei qual. É da biblioteca e não afeta o funcionamento.
4. Catálogo e Configurações existem só como itens indisponíveis ("Em breve").
5. Os recentes foram testados no host (Rust) e na UI com o host simulado; o diálogo nativo e a reabertura de um recente **no aplicativo instalado** são conferidos à mão (ver o relatório da rodada).
