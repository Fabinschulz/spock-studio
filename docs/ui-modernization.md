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
| Menu lateral com a marca (nome e "Engenharia de software assistida por IA"), Início, Projetos, Catálogo, Configurações e "Sistema local · Pronto para usar" | Igual. Item atual com preenchimento e **barra à esquerda** (a forma, não só a cor). "Projetos" leva à primeira etapa, onde se escolhe a pasta ou reabre um recente. O estado do sistema vem do host; fora do aplicativo diz "Prévia no navegador" |
| "Bem-vindo(a)!", título grande "Como podemos *ajudar?*" e texto de apoio | Igual (título fluido, 32 a 44 px) |
| Frase "Mais que ferramentas, uma engenharia inteligente." com traço de destaque, chevrons e brilho | Incluída. O texto é real; chevrons e brilho são ornamento escondido do leitor de tela, e só aparecem quando sobra largura. **Não** há a ilustração isométrica dos modelos 1 e 2: seria um desenho sem função e sem asset da marca |
| Quatro cartões com ícones **circulares** coloridos (teal, índigo, azul, laranja), "EM BREVE" no canto e selo "Principal" | Igual. Textos no imperativo, como no modelo. Os três indisponíveis têm borda sutil, título normal e "Em breve" dito em texto (e `aria-disabled`); **não** ganharam a seta, que sugeriria que abrem algo |
| Aviso "Funcionalidades em breve" com botão de fechar | Igual. A lista vem da mesma fonte dos cartões, então não diz nada que não seja verdade; "Dispensar aviso" é um botão rotulado |
| Seção "PROJETO": "Selecionar pasta do projeto", botão "Selecionar pasta ›", pasta escolhida com "Pasta selecionada", "Trocar" e a nota de que os arquivos ficam no computador | Igual. A análise **não** começa por "Analisar projeto" aqui: começa pelo cartão "Configurar projeto" (que já pede a pasta se faltar) |
| Selo "TLC (padrão)" no cabeçalho do assistente | Selo **somente leitura** (sem seta): não há outro kit para escolher |
| Trilha "Projetos › Configurar projeto" | "Início › Configurar projeto": não existe uma página "Projetos" além da própria jornada |
| Stepper acima do título, com os seis rótulos | Igual em 900 px e em 760 px; só abaixo de ~600 px de largura útil vira marcadores + "Etapa N de 6" |
| Etapa Projeto: card "Selecionar pasta do projeto" (campo com a pasta e botão de pasta), **Projetos recentes** em lista, "O que vamos analisar?" e "Ambiente de IA" à direita | Igual no arranjo, com **dados reais**. O campo mostra a pasta (o caminho abreviado pelo host) e o botão abre o diálogo nativo: **não é um campo de texto**, porque só o diálogo autoriza um caminho. A análise começa ao escolher a pasta, então **não há** "Analisar projeto" nesta etapa; o cartão "Ambiente de IA" mostra o que a análise **detectou** (a escolha é na etapa seguinte) |
| Etapa Método: cartão do TLC (recursos, versão, dependências), "Ambiente de IA" com opções de marcador à direita e "Resumo da configuração" | Igual. Em janelas largas (≥ ~990 px de área útil) são **três colunas**; nas de 900 px, o TLC ocupa a largura toda e ambiente e resumo ficam lado a lado; abaixo disso empilha. Versão, dependências e "Compatível" são reais ("Compatível" só aparece se a versão foi validada por esta fase) |
| Rodapé "Voltar" / "Próximo ›" | "Próximo ›" na etapa Projeto. Na etapa Método o botão é **"Gerar plano"**, porque ele de fato pede o plano ao motor e tem estado de carregamento |
| Ambiente "Outros"; "Ver detalhes" do TLC; "Ver todos" nos recentes | Não incluídos: não existem ou não têm destino |
| Tools/MCPs na lista "O que será configurado" | Dito como **"nenhum é instalado nesta jornada"** (o modelo sugere que são configurados) |
| Barra de título personalizada; ícone "M" no tile teal e o nome "Mariliz AI Engineering" | **Barra nativa**, como pedido. A marca é a do projeto Mariliz (arcos "m" e nó teal) e o nome é **Mariliz Studio**, como definido |

### Projetos recentes (host)

A UI não pode autorizar um caminho (só o diálogo nativo do host pode), então a lista mora no **host Rust**, em `recent-projects.json` no diretório de dados do aplicativo:

- Só entram pastas escolhidas pelo diálogo nativo e autorizadas pelo motor; no máximo 6, do mais recente ao mais antigo, sem duplicar.
- A UI recebe **id opaco, nome e caminho abreviado**, nunca o caminho completo. Reabrir é o comando `open_recent_project(id)`: o host só aceita ids da própria lista, confere que a pasta existe e pede ao motor que autorize de novo, com as mesmas regras de uma escolha nova.
- Pasta que sumiu aparece como "indisponível", com o motivo dito, e não pode ser aberta.
- Arquivo ausente ou corrompido vira lista vazia; a gravação é atômica (arquivo temporário + renomeação).
- Os comandos novos (`list_recent_projects`, `open_recent_project`) entram no manifesto e na capability da janela; a allowlist de métodos do motor não mudou.
- Escolher um recente só **seleciona** a pasta, como o diálogo. Os recentes aparecem na **primeira etapa** da jornada (em lista, com nome, caminho abreviado e data), como no modelo, e é lá que se chega pelo item "Projetos" do menu.

### Prints do aplicativo instalado

Conferidos em 900×650 no instalador gerado desta versão, em `docs/screenshots/`: `studio-01-inicio`, `studio-02-projeto-sem-pasta` (Projetos, com os recentes reais), `studio-03-projeto-analisado`, `studio-04-metodo`, `studio-05-metodo-ambientes`, `studio-06-plano` e `studio-07-revisao`. A conferência no aplicativo achou um defeito que os testes não pegam: o texto longo do ambiente sem suporte estourava o cartão e escondia os marcadores sob o resumo (corrigido). As etapas Aplicação e Resultado **não** foram refeitas no aplicativo, porque aplicar roda o instalador do TLC pela rede.

## 5. Estados e acessibilidade

- **Seleção de pasta:** nenhuma, escolhida, cancelada (dita em texto, sem alarme), inválida/inacessível (erro anunciado com a causa do host), ocupada (diálogo aberto; segundo clique ignorado) e indisponível (fora do aplicativo). Escolher a pasta não é analisá-la: a análise começa pela atividade "Configurar projeto", e a nota "análise somente de leitura" fica visível.
- **Caminho:** a UI recebe só o caminho abreviado pelo host (por privacidade). Ele é cortado visualmente com reticências, mas o texto inteiro está no documento (leitor de tela) e em `title`.
- **Análise:** indicador indeterminado (nenhum percentual inventado), erro com "Tentar de novo", avisos proporcionais (a ausência de Git é informação com a consequência prática, não erro).
- **Stepper:** `aria-current="step"`, estado de cada etapa dito em texto ("concluída", "etapa atual", "pendente", "com erro", "bloqueada") e por forma; etapas futuras **não são controles**. O erro/bloqueio vem do estado real da jornada (`stepIssue`).
- **Teclado:** ordem de foco conferida no navegador e por teste (atalho → marca → menu → cartões → aviso → seletor de pasta), itens indisponíveis focáveis com o motivo, anel de foco de 2 px em todos os controles, `<details>` e botões nativos.
- **Movimento:** transições curtas e `prefers-reduced-motion` respeitado.
- **Alvos:** controles de 40–44 px; mínimo técnico de 24 px nos secundários.

## 6. Responsividade

Verificada a 900×650 (referência) e 760×560 (mínimo do Tauri), nos dois temas, sem rolagem horizontal. O menu lateral (184 px) vira uma faixa de ícones abaixo de 864 px; os rótulos continuam acessíveis. O stepper, as colunas da etapa Projeto e as da etapa Método usam a largura **do próprio bloco** (container query), não a da janela: em 900 px o stepper mostra os seis rótulos e as duas colunas da etapa Projeto aparecem; a etapa Método tem uma, duas ou três colunas conforme a largura (36 rem e 62 rem). O ornamento do título só aparece quando sobra largura. O rodapé de ações é `sticky` e não cobre o conteúdo.

## 7. Validação executada

| Verificação | Resultado |
| --- | --- |
| Testes da UI (Vitest + Testing Library) | 177 passam (inclui axe-core, tokens, shell, componentes, recentes, jornada, tela inicial) |
| `tsc --noEmit`, ESLint (com `jsx-a11y`), Prettier | sem erros |
| Testes .NET / Rust | 232 + 2 / 16 passam (8 novos, dos recentes) |
| Contraste dos tokens (72 pares, incluindo os ícones coloridos e a superfície rebaixada dos campos) | passam |
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
6. O contraste efetivo no navegador (auditor) foi rodado na rodada anterior do desenho; nesta, o que cobre o novo desenho é o teste de tokens (incluindo a superfície rebaixada) e a conferência visual a 744, 884 e 1100 px. O auditor **não** foi rodado de novo em todas as telas.
7. Sem a ilustração isométrica dos modelos 1 e 2 (ver a tabela da seção 4).
