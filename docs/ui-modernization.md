# Mariliz Studio — Modernização da interface

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

Registro do que foi construído, do que foi validado e do que **não** foi feito. Complementa o [blueprint de experiência](experience-blueprint.md) e a [Fase 1B](phase-1b-configure-project.md).

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

## 1. Marca e ícone

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

O produto passou a se chamar **Mariliz Studio**. A marca vem do projeto Mariliz (site), sem reinterpretação:

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

| Elemento | Origem |
| --- | --- |
| Marca: dois arcos de "m" em `#f1f7f6` sobre o tile ink `#050c0c`, com o nó teal `#33c1ba` | `favicon.svg` do site |
| Wordmark "mariliz" monoline, pingos dos "i" em teal | `Logo` do site |
| Paleta (tema claro) | `brandPalette` do site (teal, ink, neutros tingidos de turquesa) |
| Paleta (tema escuro) | **Design system Mariliz AI Engineering** (`design_system_especifica_es_de_interface.md`, fonte de verdade): fundo `#030F17`, menu `#020B11`, cartões `#061824` (hover `#0A2233`, selecionado `#051C2B`), ciano `#00F2DA` (hover `#1AFFE6`, borda ativa/foco `#00DDBB`), item ativo do menu `#002D38`, ícones roxo `#4E44E2`, azul `#0066FF` e laranja `#E68A00`, ponto online `#00E676`, barra de informação `#041521` com borda `#0D3349`, borda padrão `#0E2A3C` |
| Tipografia | **Inter** como fonte única (arquivo variável, subset latin, empacotado com o aplicativo): a hierarquia vem dos pesos 400, 500, 600 e 700 |

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

O ícone do aplicativo usa a geometria exata do favicon em 1024 px (`apps/desktop-host/src-tauri/icons/source/mariliz-studio.svg`); o conjunto (`.ico`, `.icns`, PNGs do Windows) é gerado por `pnpm tauri icon`. O PNG-fonte é renderizado localmente, sem ferramentas externas. O nó teal funciona como o "nó" que a Mariliz já usa nos "i" do wordmark.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

O nome mudou em: título da janela e `productName` do Tauri (o instalador passa a se chamar "Mariliz Studio"), `index.html`, configuração da UI, Cargo, os textos que o motor grava nos projetos ("Managed by Mariliz Studio") e os títulos dos documentos. **Não mudaram:** o identificador do pacote (`dev.mariliz.ai-engineering`, para não deslocar dados locais), os nomes de projeto e namespaces (`Mariliz.*`) e a CLI (`mariliz`).

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

## 2. Tokens

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

Mesma estrutura em camadas do site (primitivos → semânticos → componentes), estendida com superfícies em níveis (`surface`, `surface-raised`, `surface-sunken`), estados do botão primário (`primary-hover/active`), `hairline`, halo de destaque, alturas de controle (`--control-height` 40 px, `--control-height-lg` 44 px, `--target-min` 24 px), raios e sombras mínimas. O tema segue o sistema operacional (claro e escuro).

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

**Contraste validado, não escolhido pela aparência.** `src/shared/styles/tokens.contrast.test.ts` lê o próprio `_tokens.scss`, resolve as referências, compõe transparências sobre a superfície adjacente e confere os pares nos dois temas, **em seis superfícies** (fundo, cartão, elevada, rebaixada, menu, aviso): texto principal ≥ 7:1; texto secundário e texto na cor da marca ≥ 4,5:1; texto sobre o botão primário (normal, hover e ativo), selos (5 tons), erro/sucesso e texto sobre o item ativo do menu ≥ 4,5:1; borda de controle, anel de foco, indicador da etapa atual e o glifo de cada ícone colorido ≥ 3:1. A paleta fornecida passou em todos os pares; o único ajuste foi a **borda de controles**, que a paleta não traz: a borda `#174252` (cartões e divisórias) tem ~1,5:1 e não serve a um campo, então os controles usam `#4F7F92` (≥ 3,27:1 em todas as superfícies). O teste já encontrou antes um defeito: no tema claro, a borda dos controles sobre a superfície dava 2,90:1 (corrigido, `neutral-500`).

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

Além do teste de tokens, um auditor rodado no navegador calculou o contraste **efetivo** (cor computada, com transparência, contra o fundo real) de cada texto visível em todas as telas e etapas, nos dois temas, a 900×650 e 760×560: nenhum texto abaixo de 4,5:1 (3:1 para texto grande).

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

### Tipografia

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

Inter como fonte única. Escala do documento, em tokens fixos: título principal 28 px Bold; título do card 16 px SemiBold; descrição 13 px Regular; título da barra superior 15 px SemiBold; menu 14 px (ativo Medium, inativo Regular); badge 10 px Bold em caixa alta; texto pequeno 12 px. Os botões da Synthra seguem 14 px SemiBold por override do tema.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

### Bordas, raios e efeitos (tema escuro)

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

| Elemento | Especificação |
| --- | --- |
| Card padrão | 1 px `#0E2A3C`, raio 12 px |
| Card ativo / selecionado | 1,5 px `#00F2DA` (1 px de borda + 0,5 px de anel interno, sem mexer no layout), fundo `#051C2B`, brilho `0 0 15px rgb(0 242 218 / .15)` |
| Card em hover | fundo `#0A2233`, borda `#00F2DA88` |
| Barra de informação | 1 px `#0D3349`, raio 8 px |
| Botão principal | raio 8 px, texto `#020B11`, sombra `0 4px 12px rgb(0 242 218 / .25)`, hover `#1AFFE6` |
| Item ativo do menu | fundo `#002D38`, texto e ícone ciano, raio 8 px; hover `#061B27` com texto branco |
| Badge | pílula, fundo `#0D2232` |

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

**Onde me afastei do documento, por acessibilidade:** (1) o texto do badge é `#7C98AD`, não `#5C7C93` (o do documento dá ~3,7:1 sobre o fundo do badge; o meu passa de 4,5:1, e isso é testado); (2) o contorno de **controles** (rádio, botões de contorno) usa `#4F7F92`, porque a borda `#0E2A3C` tem ~1,2:1 e só serve a cartões, que não são controles. O documento cita `#00F2DA` **ou** `#00DDBB` para o ciano: usei `#00F2DA` como cor principal e `#00DDBB` no anel de foco.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

### Medidas (documento)

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

Menu 224 px, padding `20 16`, itens `10 14` com gap de 8 px; conteúdo com padding `32 40`; grid de 2 colunas com gap de 16 px e 24 px entre o cabeçalho e os cards; cards com padding `20 24` e ícone circular de 44 px (glifo de 20 px); barra de informação com padding `12 20` e gap de 12 px.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

## 3. Componentes

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

**Reutilizados da Synthra:** `Provider`/tema, `Button` (todas as ações), `Alert` (só para erro bloqueante e para o resultado da aplicação), `Modal` (confirmação de reversão) e ícones (`ArrowRightIcon`, `CopyIcon`, `Home01Icon`, `LibraryIcon`, `SettingIcon`, `ListViewRectangleIcon`, `BookUploadIcon`, `UserConnectionsIcon`, `UpdateIcon`).

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

**A Synthra não tem** tooltip, accordion/disclosure, stepper, card, badge, ícone de pasta nem ícones com `currentColor` para avisos (os de severidade trazem cor fixa e destoavam dos tons do sistema). Esses foram compostos sobre os tokens:

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

| Componente | Função |
| --- | --- |
| `AppShell` (+ `SideNav`, `EngineStatus`) | Menu lateral estreito: só a marca no topo, Início, Projetos, Catálogo, Configurações e "Sistema local · Pronto para usar" | Igual, **sem** os chevrons do pé do menu (retirados a pedido). Item atual com preenchimento e texto/ícone na cor da marca (`aria-current` diz o mesmo ao leitor de tela). "Projetos" leva à primeira etapa, onde se escolhe a pasta ou reabre um recente. **Diferença:** Catálogo e Configurações dizem "Em breve" sob o nome (no modelo parecem ativos, mas não existem) |
| Cabeçalho da janela: marca, "Mariliz Studio" com destaque em teal, tagline, "Online" e versão | Igual, com o ponto de estado **com halo** (também em "Sistema local · Pronto para usar") e o divisor sutil entre "Online" e a versão. O ponto antes **não aparecia**: o nome da classe era montado como `dot-ok` e o CSS Modules entrega `dotOk`, então o ponto ficava sem cor (corrigido com um mapa de classes). Dados **reais**: "Online" só quando o host diz que o motor está pronto (senão "Iniciando…", "Indisponível" ou, fora do aplicativo, "Prévia") e a versão vem do `package.json` (hoje `v0.1.0`, não o `v1.0.0` do modelo, que seria um número inventado). O nome é **Mariliz Studio**, não "Mariliz AI Engineering" |
| Título grande "Como podemos *ajudar?*" e texto de apoio | Igual; o tamanho acompanha a largura do bloco, para caber numa linha quando há espaço. Sem "Bem-vindo(a)!" nem selo "Principal" (não estão neste modelo) |
| Arte do cabeçalho com a frase "Mais que ferramentas, uma engenharia *inteligente.*" | Usa a imagem fornecida (`src/assets/hero-banner.webp`, sem texto) no **tema escuro**, **encostada na borda direita da janela** (sem recuo, mesmo fora do contêiner centralizado do conteúdo), com esquerda, topo e base dissolvidos por máscara e a borda direita nítida, e a frase como **texto real** sobre o espaço livre à direita da arte, com "inteligente." em destaque na cor da marca (ciano). O tamanho é proporcional à área útil da janela (metade dela, até 46 rem). A imagem é recortada na faixa com desenho (~2,9:1), o cabeçalho tem exatamente essa altura e o título fica **centrado verticalmente na arte**, sem espaço sobrando entre o cabeçalho e os cartões; o texto do título fica na metade esquerda. Os cartões ficam por cima da arte. A imagem é decorativa e escondida do leitor de tela. No **tema claro** (a interface segue o sistema) a imagem escura não serve: entram a ilustração vetorial própria e a mesma frase. Aparece a partir de ~580 px de largura útil, então também na janela de 900 px. **Sem imagem de fundo da janela:** as imagens de fundo testadas antes foram retiradas a pedido |
| Quatro cartões com ícones **circulares** coloridos e "EM BREVE" no canto | Igual. Os três indisponíveis têm borda sutil, "Em breve" dito em texto e `aria-disabled`, e **não** têm a seta, que sugeriria que abrem algo. O texto do primeiro cartão é o do modelo ("Analisa o repositório e configura o Harness."), que está no presente enquanto os outros estão no imperativo; mantive como no modelo. Em ~900 px os títulos dos cartões quebram em duas linhas, porque o selo disputa a linha com eles |
| Barra "Selecionar pasta do projeto" com o botão "Selecionar pasta ›" e a faixa "Você pode trocar a pasta a qualquer momento. A análise inicial é somente de leitura e não altera seus arquivos." | Igual. Com pasta escolhida, a barra mostra o nome, o caminho abreviado, "Pasta selecionada" e "Trocar" (o modelo não mostra esse estado). A análise **não** começa aqui: começa pelo cartão "Configurar projeto", que já pede a pasta se faltar. Abaixo ficam os **atalhos dos projetos recentes** (nome, caminho abreviado e último acesso com a hora); escolher um só o seleciona |
| "TLC (padrão)" com ícone de escudo e seta para baixo | Seletor nativo, com ícone e seta. Tem **uma única opção**: nesta fase não existe outro método, e dizer isso é mais honesto que esconder o controle |
| Trilha "Projetos › Configurar projeto" | Igual ("Projetos" leva à primeira etapa, a mesma do item do menu) |
| Stepper acima do título, com os seis rótulos | Igual em 900 px e em 760 px; só abaixo de ~600 px de largura útil vira marcadores + "Etapa N de 6" |
| Etapa Projeto: card "Selecionar pasta do projeto" (campo com a pasta, botão **Analisar projeto**, recentes em linhas limpas), "O que vamos analisar?" e "Ambiente de IA" (seletor) | Igual. **A análise só começa ao clicar em "Analisar projeto"** (antes começava ao escolher a pasta). Sem análise não aparecem resultados nem texto de conclusão, e "Próximo" fica desabilitado. Os recentes são linhas sem borda (ícone, nome, caminho abreviado e data relativa: Hoje, Ontem ou dd/mm/aaaa). O "Ambiente de IA" é um seletor nativo com as três opções (as sem suporte dizem isso no texto) e **é o mesmo estado da etapa seguinte**. O campo da pasta **não é de texto**: só o diálogo nativo autoriza um caminho |
| Etapa Método: cartão do TLC (recursos, versão, dependências, sub-cartão "Buscar a versão mais recente"), "Ambiente de IA" com opções de marcador à direita e "Resumo da configuração" | Igual: **três colunas de mesma altura** em janelas largas (≥ ~990 px de área útil; duas e uma abaixo disso), notas de rodapé com divisória (cadeado no ambiente, "i" no resumo) e "Voltar" / "Gerar plano →". Dados **reais** (versão detectada, "Compatível" só se validada por esta fase, Node/Python/Git encontrados). Os ambientes sem suporte ficam em texto mais apagado, **sem** reduzir a opacidade (isso baixaria o contraste); **não há opção "Outros"** (não existe). O sub-cartão só aparece com o TLC instalado, porque só então a opção faz sentido |
| Rodapé "Voltar" / "Próximo ›" | "Próximo ›" na etapa Projeto. Na etapa Método o botão é **"Gerar plano"**, porque ele de fato pede o plano ao motor e tem estado de carregamento |
| Ambiente "Outros"; "Ver detalhes" do TLC; "Ver todos" nos recentes | Não incluídos: não existem ou não têm destino |
| Tools/MCPs na lista "O que será configurado" | Dito como **"nenhum é instalado nesta jornada"** (o modelo sugere que são configurados) |
| Barra de título personalizada; ícone "M" no tile teal e o nome "Mariliz AI Engineering" | **Barra nativa**, como pedido. A marca é a do projeto Mariliz (arcos "m" e nó teal) e o nome é **Mariliz Studio**, como definido |

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

### Projetos recentes (host)

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

A UI não pode autorizar um caminho (só o diálogo nativo do host pode), então a lista mora no **host Rust**, em `recent-projects.json` no diretório de dados do aplicativo:

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

- Só entram pastas escolhidas pelo diálogo nativo e autorizadas pelo motor; no máximo 6, do mais recente ao mais antigo, sem duplicar.
- A UI recebe **id opaco, nome e caminho abreviado**, nunca o caminho completo. Reabrir é o comando `open_recent_project(id)`: o host só aceita ids da própria lista, confere que a pasta existe e pede ao motor que autorize de novo, com as mesmas regras de uma escolha nova.
- Pasta que sumiu aparece como "indisponível", com o motivo dito, e não pode ser aberta.
- Arquivo ausente ou corrompido vira lista vazia; a gravação é atômica (arquivo temporário + renomeação).
- Os comandos novos (`list_recent_projects`, `open_recent_project`) entram no manifesto e na capability da janela; a allowlist de métodos do motor não mudou.
- Escolher um recente só **seleciona** a pasta, como o diálogo. Os recentes aparecem na **primeira etapa** da jornada (em lista, com nome, caminho abreviado e data), como no modelo, e é lá que se chega pelo item "Projetos" do menu.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

### Prints do aplicativo instalado

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

Os prints em `docs/screenshots/` (`studio-01` a `studio-07`) foram feitos no instalador da versão **anterior** do desenho, sem o cabeçalho da janela, o menu estreito e a ilustração. **Estão desatualizados** e serão refeitos. A conferência do desenho atual foi feita no aplicativo em modo de desenvolvimento e numa prévia no navegador com o host simulado, a 884, 1100 e 1300 px de largura. A conferência no aplicativo achou um defeito que os testes não pegam: o texto longo do ambiente sem suporte estourava o cartão e escondia os marcadores sob o resumo (corrigido). As etapas Aplicação e Resultado **não** foram refeitas no aplicativo, porque aplicar roda o instalador do TLC pela rede.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

## 5. Estados e acessibilidade

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

- **Seleção de pasta:** nenhuma, escolhida, cancelada (dita em texto, sem alarme), inválida/inacessível (erro anunciado com a causa do host), ocupada (diálogo aberto; segundo clique ignorado) e indisponível (fora do aplicativo). Escolher a pasta não é analisá-la: a análise começa pela atividade "Configurar projeto", e a nota "análise somente de leitura" fica visível.
- **Caminho:** a UI recebe só o caminho abreviado pelo host (por privacidade). Ele é cortado visualmente com reticências, mas o texto inteiro está no documento (leitor de tela) e em `title`.
- **Análise:** indicador indeterminado (nenhum percentual inventado), erro com "Tentar de novo", avisos proporcionais (a ausência de Git é informação com a consequência prática, não erro).
- **Stepper:** `aria-current="step"`, estado de cada etapa dito em texto ("concluída", "etapa atual", "pendente", "com erro", "bloqueada") e por forma; etapas futuras **não são controles**. O erro/bloqueio vem do estado real da jornada (`stepIssue`).
- **Teclado:** ordem de foco conferida no navegador e por teste (atalho → marca → menu → cartões → aviso → seletor de pasta), itens indisponíveis focáveis com o motivo, anel de foco de 2 px em todos os controles, `<details>` e botões nativos.
- **Movimento:** transições curtas e `prefers-reduced-motion` respeitado.
- **Alvos:** controles de 40–44 px; mínimo técnico de 24 px nos secundários.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

## 6. Responsividade

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

Verificada a 900×650 (referência) e 760×560 (mínimo do Tauri), nos dois temas, sem rolagem horizontal. O menu lateral (224 px) vira uma faixa de ícones abaixo de 864 px; os rótulos continuam acessíveis. O stepper, as colunas da etapa Projeto e as da etapa Método usam a largura **do próprio bloco** (container query), não a da janela: em 900 px o stepper mostra os seis rótulos e as duas colunas da etapa Projeto aparecem; a etapa Método tem uma, duas ou três colunas conforme a largura (36 rem e 62 rem). O ornamento do título só aparece quando sobra largura. O rodapé de ações é `sticky` e não cobre o conteúdo.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

## 7. Validação executada

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

| Verificação | Resultado |
| --- | --- |
| Testes da UI (Vitest + Testing Library) | 225 passam (inclui axe-core, tokens, shell, componentes, recentes, jornada, tela inicial) |
| `tsc --noEmit`, ESLint (com `jsx-a11y`), Prettier | sem erros |
| Testes .NET / Rust | 232 + 2 / 16 passam (8 novos, dos recentes) |
| Contraste dos tokens (nos dois temas, em seis superfícies, incluindo os ícones coloridos e o item ativo do menu) | passam |
| Contraste efetivo no navegador (todas as telas e etapas, 2 temas, 2 tamanhos) | 0 falhas |
| Navegação por teclado (ordem e foco) | conferida no navegador |
| Estado do motor | testado com a ponte do host simulada (pronto, iniciando, falha, valor desconhecido) |

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

**O axe-core roda em jsdom**: confere ARIA, nomes, rótulos e semântica, mas não contraste nem foco visual (por isso o auditor no navegador e a conferência manual). Uma execução sem violações **não** prova que a interface é acessível.

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

## 8. Limitações conhecidas

> **Nota (2026-10-30):** o produto apresenta um único modelo de implementação, o **Mariliz**. Nomes de kits e metodologias citados neste documento são históricos: na interface e nos contratos o mecanismo de especificação é a "estrutura de especificação" (ver [backend-architecture.md](backend-architecture.md)).

1. **Leitor de tela real** (NVDA, JAWS, Narrator) **não foi testado**; a semântica foi conferida por teste automatizado e inspeção do DOM.
2. **Zoom do sistema e alto contraste do Windows** não foram verificados.
3. Algum ícone da Synthra usado no menu lateral emite aviso do React no console (`fill-rule` em vez de `fillRule`); não identifiquei qual. É da biblioteca e não afeta o funcionamento.
4. Catálogo e Configurações existem só como itens indisponíveis ("Em breve").
5. Os recentes foram testados no host (Rust) e na UI com o host simulado; o diálogo nativo e a reabertura de um recente **no aplicativo instalado** são conferidos à mão (ver o relatório da rodada).
6. O contraste efetivo no navegador (auditor) foi rodado na rodada anterior do desenho; nesta, o que cobre o novo desenho é o teste de tokens (incluindo a superfície rebaixada) e a conferência visual a 744, 884 e 1100 px. O auditor **não** foi rodado de novo em todas as telas.
7. Sem imagem de fundo da janela (retirada a pedido): o fundo é liso nos dois temas. A imagem da arte do cabeçalho só vale no tema escuro; no claro entra a ilustração vetorial (conferido no navegador, não no aplicativo).
