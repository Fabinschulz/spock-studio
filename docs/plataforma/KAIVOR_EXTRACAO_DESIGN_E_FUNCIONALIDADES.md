# Kaivor: extração de design e funcionalidades

**Data:** 2026-11-13. Adendo aos seis documentos de 2026-11-12 (`KAIVOR_PRODUCT_ANALYSIS.md` e seguintes). Aqui estão
os valores medidos de design, a especificação do companion "Kai", o inventário de funcionalidades com a situação de
cada uma no Spock Studio e as decisões que precisam do dono do produto antes de qualquer implementação.

Nada no Spock foi alterado. Este documento extrai e organiza; não implementa.

## 1. Método e limites

| Fonte | Uso | Limite |
| --- | --- | --- |
| Instância local do Kaivor (Vite em `127.0.0.1:5173`, versão 0.2.95, build de desenvolvimento) aberta no navegador | Tokens computados, estrutura do shell, lista de controles, texto da tela Conexões | Sem as chamadas internas do Electron (`window.kaivor`), Tarefas, Rotinas e Notas ficam em "Carregando…". Só o shell e Conexões renderizam completos |
| Seis capturas do aplicativo Electron enviadas pelo dono | Telas reais: Bot, Rotinas, Conexões, seletor de modelos, modo debug, o Kai | Estado "sem IA conectada" em todas; nenhuma tela com dado |
| Repositório do Kaivor | `DESIGN.md`, `docs/design-system.md`, `src/styles/00-kaivor-theme-tokens.css`, specs, `electron/main.cjs`, `electron/main/application/companion/activity.cjs` | O mesmo limite de leitura da análise de 12/11 (arquivos de 10 a 18 mil linhas lidos por busca dirigida) |

O aplicativo Electron em execução não pôde ser conduzido por mim (só o navegador do painel está disponível). Animações,
transições reais, o botão "Auto", o comportamento do Kai com o app minimizado e as telas com dado continuam
**não verificados em uso**.

## 2. Proveniência: ler antes de reutilizar

O design do Kaivor não é todo original dele. Isto muda o que o Spock pode levar.

| Elemento | Origem declarada no repositório | Consequência para o Spock |
| --- | --- | --- |
| Composição do shell (rail, abas de sessão, campo de mensagem, seletores de branch e de modelo, reordenação de abas) | Adaptações do projeto MonoCode, licença MIT, fonte `github.com/hardbeat920/monocode`, aviso em `public/notices/monocode.txt`. O `DESIGN.md` diz "owner-pinned Monocode composition" | Levar a composição exige manter o aviso MIT. Levar só medidas e regras, escritas de novo, não exige |
| Ícones | `@hugeicons/core-free-icons` 4.3.0 e `@hugeicons/react`, com Lucide de reserva | Licença não verificada aqui. O Spock usa só os ícones do `@mariliz/ui-kit` (decisão existente) |
| Papel de parede da floresta | Cena original gerada para o Kaivor, com arquivos `.provenance.txt` e `.webp.json` | Pertence ao Kaivor. Não levar |
| **Kai (robô)** | "Arte aprovada fornecida no template de referência do projeto" (`docs/templates`), arquivo `public/brand/kaivor-pet.svg` | Origem do template não confirmada. **Não levar o desenho.** O Kaivor registra em spec que não copia robô de outra marca |
| Paleta de código | Dracula Classic (escuro) e Alucard Classic (claro) | Confirmar a licença antes de qualquer uso |
| Fontes | Sistema no shell; IBM Plex Mono no código; Nerd Fonts nos avisos | Spock usa Inter |

Regra proposta: o Spock recebe **valores e regras de design escritos de novo**, nunca arquivos, SVG, imagens ou textos de
interface do Kaivor.

## 3. Design extraído

### 3.1 Paleta efetiva (tema escuro, computada na instância)

| Papel | Valor | Observação |
| --- | --- | --- |
| Canvas | `#151515` | `--bg`, `--bg-primary` |
| Barra lateral | `#161616` | `--surface`, `--bg-secondary` |
| Superfície elevada | `#222222` | `--raised`, `--bg-card`, `--bg-elevated` |
| Seleção | `#ffffff0e` | `--signal-soft` |
| Borda e linha | `#ffffff10` | `--line` e `--line-soft` são o mesmo valor |
| Texto principal | `#ececec` | `--text` |
| Texto secundário | `#a0a0a0` | `--text-2` **e** `--text-3` são o mesmo valor: não há nível terciário |
| Foco | `#c1c1c1` | Contorno de 2 px com deslocamento de 2 px |
| Campo de mensagem | `#272727b8`, borda `#ffffff24`, desfoque de 18 px | Única superfície translúcida |
| Sucesso, alerta, perigo | `#34D399`, `#FBBF24`, `#FB7185` | Cor só em estado |
| Acento | `#ececec` (neutro) | O shell não tem acento cromático |

Tema claro: canvas `#fafafa`, barra `#f4f4f4`, superfície `#ffffff`, seleção `#00000009`, borda `#00000012`, texto
`#252525`, secundário `#666666`, foco `#676767`, campo `#ffffffd9`.

**Defeito observado:** o arquivo de tokens ainda define a paleta antiga roxa (`--signal: #A78BFA`). A camada neutra a
sobrepõe, mas `--signal-line` continua roxo (`rgba(167,139,250,.30)`) e vaza para bordas de seleção. São duas gerações
visuais convivendo, o problema que a análise de 12/11 já listava como "o que evitar".

### 3.2 Tipografia

| Papel | Valor |
| --- | --- |
| Família | `-apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif`; código em `'IBM Plex Mono'` |
| Saudação | 20 px, peso 500, entrelinha 1,4, espaçamento -0,025 em (18 px abaixo de 800 px de largura) |
| Corpo e campo de mensagem | 14 px, entrelinha 1,6 |
| Rótulo e aba | 13 px |
| Contexto | 12 px |
| Ação de status | 11 px |
| Prévia de Markdown | 14 px, entrelinha 1,75; títulos 19 px e 24 px |
| Escala utilitária | 10, 11, 12, 13, 14, 16, 18, 24 |
| Números | Algarismos tabulares |

### 3.3 Medidas e ritmo

| Item | Valor |
| --- | --- |
| Rail | 60 px recolhido, 200 px expandido |
| Faixa superior, rodapé | 48 px, 28 px |
| Ação do rail | 44 px (recolhido), 36 px (expandido); ícone de 20 px, traço 1,75, vão de 4 px |
| Controles | 30, 34 e 40 px (pequeno, médio, grande). Botão de ícone 30 a 34 px. Seletor compacto e botão de enviar 28 px |
| Coluna da conversa, campo de mensagem | 800 px, 832 px com margens |
| Raios | 5 (seletor), 6 (controle), 7 (aba), 8 (busca), 10 (cartão e painel), 12 (campo de mensagem) |
| Espaço | 4, 8, 12, 16, 20, 24, 32 |
| Movimento | 140, 220 e 320 ms; curva `cubic-bezier(.22,1,.36,1)`; reordenação 160 ms |
| Sombra | Suave `0 1px 3px rgba(0,0,0,.22)`; flutuante `0 8px 24px rgba(0,0,0,.28)` (menus e seletores; o `DESIGN.md` cita `0 16px 48px #0003` para o seletor de modelos) |
| Pontos de quebra | 560, 720, 900, 1080, 1180, 1320; adaptações do shell em 1100, 800 e 760; arquivo ocupa a tela abaixo de 800 px de conteúdo |
| Papel de parede | Opacidade 0,42 (escuro, conversa vazia), 0,16 (escuro, com conversa), 0,14 e 0,05 no claro; máscara progressiva; some com transparência reduzida |

### 3.4 Padrões de componente

| Componente | Regra observada |
| --- | --- |
| Rail | Ícone discreto em repouso; rótulo e fundo de seleção em hover e ativo; nome acessível mesmo sem rótulo visível |
| Enviar | Botão invertido (fundo `#ececec`, ícone `#151515`), 28 px; desabilitado usa fundo de seleção, texto secundário e opacidade 0,7 |
| Campo de mensagem | Preenchimento 12 px; editor mínimo de 64 px; borda sobe para `#ffffff40` em foco; barra superior com projeto e branch; barra inferior com anexar, modelo, "Auto", "Voz" e recolher |
| Seletor compacto | 28 px, preenchimento horizontal de 8 px, fundo de seleção |
| Painel flutuante | Superfície elevada, raio 8 px, área de resultados com rolagem limitada |
| Painel lateral | `DesignModal` renderiza gaveta lateral (nome legado). Usado para edição, permissões e confirmação |
| Estado | `DesignStatusPill` com tons neutro, informação, sucesso, alerta, perigo; `DesignEmptyState`; `DesignStepProgress` |
| Abas de sessão | Faixa com migalha ("Tarefas ›"), busca, divisão de painel, abas com ×, botão de nova sessão |
| Botão primário desabilitado | Regra escrita: explica o pré-requisito. **Na prática, "Preparar rascunho" em Rotinas não explica** |
| Foco | Contorno de 2 px, deslocamento de 2 px; campo de busca usa `focus-within` de 1 px |
| Movimento reduzido e transparência reduzida | Animações caem para 0,01 ms; cena some e o campo fica opaco |

### 3.5 Padrões de tela (capturas reais)

| Tela | Estrutura | Observação |
| --- | --- | --- |
| Bot (vazia) | Saudação centrada, atalhos "Revisar contexto" e "Memória do bot", cartão "Seu próximo passo começa com uma conexão", campo de mensagem | Único lugar com a cena de fundo |
| Conexões | Título, uma frase, cinco linhas (título, descrição, seta) separadas por linha fina, frase final sobre autorização | Tela mais limpa; alinhada à esquerda |
| Rotinas | Três colunas numeradas (recurso, rascunho, execução), exemplo recolhido, listas Execuções e Automações, modelos, formulário | Página longa; centralizada; usa caixas de seleção nativas; mostra o termo interno "Cortex" |
| Seletor de modelos | Popover com trilho de ícones (favoritos, todos), busca, lista, "Voltar aos recentes" | Sem IA conectada mostra "Nenhum modelo com esses filtros", mensagem enganosa |
| Modo debug | Gaveta lateral direita com filtros (debug, info, warn, error, main, renderer, ipc, process), copiar, arquivo, limpar, desligar; selo com contador na barra superior | Contador total em vez de contador de erros; entradas repetidas e duplicadas entre main e renderer; argumentos `%s` não substituídos |
| Faixa de plano | "Seu Pro grátis, 3 dias restantes. Até 16/11/2026, 12:52. Sem cobrança automática." e botão "Conhecer planos" | Texto honesto sobre cobrança; faixa permanente |
| Rodapé do shell (modo web) | Branch e versão, "Terminal", "Saída" | Faixa de 28 px |

### 3.6 O Kai (companion): especificação extraída

Fonte: `electron/main.cjs` (linhas 5044 em diante), `electron/main/application/companion/activity.cjs`,
`electron/companion-preload.cjs`, spec `feature-kaivor-identity-theme-v1` (AC-025, AC-040 a AC-045).

| Aspecto | Comportamento |
| --- | --- |
| Janela | Transparente, sempre no topo, 108×139 px, margem de 20 px da área útil, canto padrão inferior direito |
| Quando aparece | Quando a janela principal é minimizada (e o companion não foi desativado) |
| Interação | Clique restaura o app; arrastar e soltar encaixa no canto mais próximo (os quatro cantos); botão direito oferece desativar, persistido. O arrasto é feito por eventos do mouse, não pela região de arrasto nativa, porque a região nativa engolia o clique |
| Estados | `idle`, `thinking`, `working`, `happy`, `error`. O desenho tem dez rostos (inclui piscar, piscadela, surpreso, curioso, dormindo) |
| Entrada de dados | Eventos `chat-run-*`: iniciado e pensando viram `thinking`; chamada e resultado de ferramenta, saída e batimento viram `working`; concluído vira `happy`; falha vira `error`. Estados terminais ficam 10 s e voltam a `idle`. Quest entra como reserva |
| Texto | Bolha de duas linhas, 112 caracteres, sem código, HTML nem marcação, com segredos redigidos, inserido por `textContent` |
| Cor | Acento em pensando e trabalhando, verde em concluído, vermelho em erro; o rosto e o texto repetem o estado (não depende só de cor). LEDs do peito acompanham |
| Tema e acessibilidade | Segue o tema claro ou escuro; respeita movimento reduzido; nome acessível; tudo local, sem recurso externo |
| Lacuna | Só eventos de chat e Quest. Tarefas e Rotinas não parecem alimentá-lo (inferência a partir do mapeamento de eventos) |

### 3.7 Lições de design confirmadas ao vivo

1. Duas gerações visuais convivendo (paleta roxa antiga sob a neutra).
2. Alinhamento inconsistente entre telas (Conexões à esquerda, Rotinas centralizada).
3. Controles nativos sem estilo em Rotinas.
4. Termo interno exposto ("Cortex") depois de uma auditoria que dizia ter removido esse problema.
5. Botão primário desabilitado sem motivo, contra a regra do próprio produto.
6. Estado vazio com causa errada no seletor de modelos.
7. Mesma conversa duplicada na aba e na barra lateral.
8. Diagnóstico com contador sem sinal, entradas duplicadas e argumentos de formatação perdidos.

## 4. Funcionalidades: inventário e situação no Spock

Legenda da coluna Spock: **Feito** (já entregue, com a fase), **Parcial**, **Falta** (cabe no escopo), **Fora** (fora de
horizonte por decisão registrada em `SPOCK_IMPLEMENTATION_ROADMAP.md` seção 5).

| Área do Kaivor | O que faz | Spock |
| --- | --- | --- |
| Navegação em níveis, projeto no shell | Cinco itens, ferramentas fixáveis, busca, contexto sempre visível | **Feito** (fase B): seletor de projeto e alternador de ambiente. Fixar ferramentas: Fora (poucas telas) |
| Painel lateral em vez de modal, estados, retorno ao lado da ação | Padrão único | **Feito** (fase B) |
| Central de execuções | Projeção única, busca, filtros, detalhe lateral, próximo passo | **Feito** (fase C) |
| Hub de conexões, cofre, validação antes de salvar | Cinco portas; segredo no cofre do sistema | **Feito** (fase F), com cofre Linux e macOS ainda sem validação em máquina real |
| Provedor de IA como conexão, modelo por finalidade | Vários provedores | **Feito** (fase F: OpenAI, Anthropic, modelo por finalidade) |
| Catálogo de agentes e ficha "o que pode acessar" | Cartões e editor | **Feito** (fase E, sem editor livre) |
| Linha do tempo, trilha de auditoria, decisões como memória, investigação | Atividade da IA, memória de projeto | **Feito** (fase G, em andamento; faltam avaliações de gestão e validação com modelo real) |
| Paleta de comandos (Ctrl/Cmd+K) | Busca global | **Falta** (P2) |
| Painéis redimensionáveis, restaurar layout | Divisores por ponteiro e teclado | **Falta** (P2) |
| Modo de diagnóstico | Captura de log redigido, painel com filtros | **Falta** (P2); incorporar com as correções de 3.7 |
| Estado ambiente fora da janela (o Kai) | Status da execução com o app minimizado | **Falta** (P3 no roadmap; ver decisão 3) |
| Estado "resultado incerto" | Escrita sem confirmação nunca vira sucesso | **Falta** (P2) |
| Uso de modelo agregado | Tokens por agente e provedor, marcando estimativa | **Falta** (P2) |
| Sincronização agendada, rotinas de plataforma | Rotinas com o app aberto | **Falta** (P2) |
| Servidor MCP do Spock para IAs externas | Gateway local com credencial por consumidor | **Falta** (P2, exige investigação de segurança) |
| MCP gerenciado pelo plano do Harness | Hub de MCP | **Falta** (P2) |
| Assistente de engenharia | Conversa com projeto, com citações | **Falta** (P2) |
| Segundo ambiente de IA | Codex, Cursor | **Falta** (P2, por demanda) |
| Abrir arquivo, diff ou projeto no editor da pessoa | Atalho, sem virar IDE | **Falta** (P2) |
| Tema claro | Paridade de temas | Fora (P3, sem demanda) |
| Construtor de automações (Rotinas) | Etapas por capacidade | **Fora** |
| Editor visual de fluxo (Cortex) e rascunho de diagrama por IA | Canvas | **Fora** |
| Database Studio | Cliente de seis motores | **Fora** |
| HTTP Client | Coleções, ambientes | **Fora** |
| Conversa livre como centro do produto (Bot), modo Auto, Voz | Chat, autonomia, voz | **Fora** |
| Memória aprendida e vista "Cérebro" | Três tipos de memória | **Fora** |
| Notas | Notas do usuário (conteúdo não verificado ao vivo) | Não avaliada |
| Quests e pipeline de desenvolvimento por agentes, Kanban | Discovery, Spec, Builder, QA, Reviewer | **Parcial por decisão**: o Spock lê o ciclo feito no Claude Code, não o executa (P2, depois da fase E) |
| Cunho comercial (plano Local e Pro, trial, Enterprise) | Faixa de plano | Não se aplica ao Spock hoje |
| Aplicativo web | Execução em servidor | **Fora** |
| Captura de tela, reunião ao vivo | Voz, copiloto de reunião | **Fora** (não aparecem no produto atual ou fogem ao propósito) |

## 5. O que "implementar tudo" significaria

- **O que cabe** (a coluna **Falta**): 12 itens, 11 deles P2 e o Kai P3, que o roadmap já prevê como "depois deste
  ciclo".
- **O que exigiria revogar uma decisão**: os itens **Fora**. São as áreas inteiras que no Kaivor custaram o maior volume
  (Database Studio: 114 specs, 74 arquivos no processo principal e 24 folhas de estilo; Cortex: cerca de 2.100 linhas
  só no canvas e uma spec de 44 KB). O Spock tem hoje poucas telas e um modelo de execução único; trazer essas áreas
  criaria um segundo produto.
- **O design**: levar a linguagem visual do Kaivor significaria trocar o tema (teal `#33C1BA`, Inter, tokens testados
  por contraste, `@mariliz/ui-kit`) por neutros de carvão sem acento e fonte de sistema. Isso desfaz a direção
  Workbench aprovada e o teste de contraste por nome de token, e herdaria a derivação do MonoCode (seção 2).

## 6. Decisões que precisam do dono do produto

| # | Decisão | Recomendação |
| --- | --- | --- |
| 1 | **Design:** manter o Workbench teal e levar só princípios e medidas, ou adotar a linguagem neutra do Kaivor | Manter. Os princípios (painel lateral, retorno na linha, densidade com teto) já foram levados |
| 2 | **Escopo:** os itens da coluna Falta, ou também revogar áreas da lista Fora | Só a coluna Falta, começando por: diagnóstico, estado incerto, uso de modelo, paleta de comandos, painéis redimensionáveis |
| 3 | **Kai:** personagem, ícone de bandeja, ou nada | Primeiro um ícone de bandeja do Tauri com quatro estados e selo de aprovação pendente. Se houver personagem, desenho original a partir do "olhar lógico" da marca, sem pernas nem braços |
| 4 | **Proveniência:** confirmar a origem do template que deu o Kai e a licença da paleta Dracula antes de qualquer reaproveitamento | Não reaproveitar arquivos; só valores e regras reescritos |
| 5 | **Telas com dado:** conduzir o Electron em execução (por você, com capturas das telas Tarefas, Notas, Execuções, Agentes e Configurações) para fechar o que o modo web não mostrou | Fazer antes de qualquer fase nova |

## 7. O que não foi verificado

- Qualquer tela com dado real, e as telas Tarefas, Rotinas (além da captura), Notas, Execuções, Agentes, Cortex,
  Kanban, Database Studio, HTTP Client e Configurações em uso.
- Animações, transições, atalhos de teclado e comportamento do Kai com o app minimizado.
- Contraste medido, leitor de tela, Windows e Linux.
- Licenças de Hugeicons e Dracula, e a origem do template do Kai.
