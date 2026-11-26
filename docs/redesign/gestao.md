# Spock Studio — Área Gestão: diagnóstico e proposta

> **Estado:** diagnóstico feito no aplicativo real e proposta aprovada pelo dono em 2026-11-25, com as seis decisões
> conforme a recomendação (seção 5). Os incrementos G-1 a G-7 estão entregues (o G-7 neste PR). O que
> ficou sem validar com dados reais tem etapa própria, na seção 6. Continua o item 4 do briefing (fluxos internos de Gestão), que o redesign de
> [`workspace-unificado.md`](workspace-unificado.md) não cobriu: ele entregou a área e a navegação, não as telas de dentro.
>
> Caminhos de arquivo relativos a `desktop/ui/src/features/management/`, salvo indicação.

## 1. Método

- Aplicativo real: `tauri dev` com o motor .NET compilado e a inteligência empacotada, janela dirigida pelo Playwright
  ligado ao protocolo de depuração, axe-core 4.11 injetado (regras WCAG 2.0, 2.1 e 2.2, níveis A e AA, com contraste).
- Pasta de dados do motor nova e isolada (`MARILIZ_DATA_DIR`). Dois projetos de teste descartáveis (um serviço
  Node/TypeScript pequeno, com Git e um commit), abertos pelo seletor do topo para o Portfólio ter mais de um projeto. No
  fim, as pastas foram apagadas e a lista de recentes voltou ao que era.
- Percurso: as quatro telas sem projeto aberto; Portfólio com dois projetos; Acompanhamento vazio; o formulário de
  decisão; Assistente sem chave de modelo; uma investigação em etapas (sem modelo, que é o que roda sem chave); o caminho da
  investigação aberto; Relatório; uma decisão registrada e ligada à investigação; os fatos guardados com ela; o Relatório
  de novo, com a decisão e a execução; copiar o Relatório; a ordem do teclado no Portfólio; o link do aviso de chave; e
  "Abrir projeto" a partir do Portfólio.
- Capturas em 1440×900 e 900×650, cada uma também com a altura do conteúdo inteiro. As imagens não entram no repositório;
  a seção 3 as descreve.

| Tela e estado | Altura do conteúdo (1440 / 900) | axe |
| --- | --- | --- |
| As quatro telas sem projeto | cabem na tela | 0 violações |
| Portfólio, dois projetos sem ferramenta | 981 / 1039 px | 0 |
| Acompanhamento sem ferramenta | cabe / cabe | 0 |
| Acompanhamento com o formulário de decisão aberto | 1074 / 1158 px | 0 |
| Acompanhamento com uma decisão e os fatos dela abertos | cabe / 1001 px | 0 |
| Assistente antes de perguntar | cabe / 746 px | 0 |
| Assistente com a resposta da investigação | 1120 / 1187 px | 0 |
| Assistente com o caminho da investigação aberto | 1354 / 1503 px | 0 |
| Relatório (com e sem decisão) | cabe / 845 px | 0 |

O axe não acusou nada em nenhum estado, nas duas larguras, e o console ficou sem erros. No Portfólio o teclado segue a
ordem da tela. A resposta do Assistente leva o foco ao título "Resposta", como prometido no código. Os problemas abaixo são
de leitura, de hierarquia e de coerência (WCAG 3.3.2 e 2.4.6), não de marcação.

**O que não foi verificado, e por quê:** nenhum estado com dados de andamento. Fluxo, previsão, riscos, histórico dos
riscos, marcos, execuções ligadas a itens, fontes de dados e os pontos de atenção do Portfólio só aparecem depois de
vincular uma ferramenta de tarefas ou de código e sincronizar, e as duas conexões que o motor conhece pedem um token da
conta do dono. Este diagnóstico não digitou credencial nenhuma e não inventou dados. Os problemas desses estados (marcados
com **(código)** nas tabelas) vêm da leitura do código e dos testes, não do aplicativo; a decisão DG5 propõe como
verificá-los. Também não foram verificados: resposta escrita por modelo (exige chave), pergunta ao portfólio com modelo,
leitor de tela de verdade.

## 2. Problemas que atravessam a área

| # | Problema | Onde |
| --- | --- | --- |
| G1 | A entrada da área muda conforme haja projeto: sem projeto, "Gestão" abre o Portfólio; com projeto, o Acompanhamento. A lista lateral põe o Portfólio primeiro, mas a trilha "Gestão / …" sempre leva ao Acompanhamento, que, sem projeto, só diz "Nenhum projeto selecionado". | `app/layouts/app-shell/navigation.tsx:154-191` e `:312` |
| G2 | Três lugares para perguntar sobre andamento: o campo do Início (que leva ao Assistente), o Assistente e o bloco "Perguntar sobre o portfólio". O Portfólio e o Assistente repetem o mesmo código: aviso de chave, validação, erro com `needsKey`, prévia do que será enviado. | `components/portfolio-ask.tsx`, `assistant-page.tsx:51-75` e `:91-96` |
| G3 | Períodos diferentes para a mesma coisa: o Acompanhamento e o Portfólio usam 8 semanas, fixas e sem dizer; o Relatório abre em 4, com escolha; os fatos guardados numa decisão dizem "últimas 8 semanas". Comparar o Acompanhamento com o Relatório do mesmo dia dá números diferentes sem aviso. O motor já aceita o período nos indicadores, mas a tela não o passa. | `service/management-api.ts:76-77`; motor: `runtime/Mariliz.Application/Management/ManagementService.cs:12` e `ProjectReport.cs:35` |
| G4 | O aviso "Nenhuma chave de modelo configurada" manda a "Configurações"; a chave fica em Configurações › Minha IA. O título do aviso sai maior que o texto, que vem em negrito miúdo, como o T5 do Configurar. | `components/portfolio-ask.tsx:65-70`, `assistant-page.tsx:91-96` e `:164` |
| G5 | Botão desabilitado sem dizer por quê: "Perguntar" (sem chave), "Perguntar ao portfólio" (sem chave) e "Registrar decisão" (título e decisão vazios). O último, sem variante, parece texto apagado mesmo quando habilitado. | `assistant-page.tsx:110-116`, `components/portfolio-ask.tsx:242`, `components/decisions-section.tsx:160` |
| G6 | Três formatos de erro: parágrafo com `role="alert"` (`.errorText`), `ErrorState` e `InlineNotice`. | `assistant-page.tsx:159`, `components/portfolio-ask.tsx:142`, `components/decisions-section.tsx:158` |
| G7 | Termos sem explicação: marco, regra de risco, gravidade, tempo total e tempo de trabalho (com o nome em inglês entre parênteses), "85% em até", fato, provedor de modelo, investigação em etapas, rodada. O `Term` aparece uma vez, em "simulações". | `components/flow-section.tsx:30-35`, `components/forecast-section.tsx:70-80`, `assistant-page.tsx:117-122` |
| G8 | Textos do motor que vazam para a tela: a data em formato ISO e em UTC ("Hoje é 2026-11-26" às 22h49 do dia 25), concordância errada ("Mudança de escopo não é calculado"), "Títulos de itens não foram incluídos: nem todas as fontes autorizaram" quando não há fonte nenhuma, e "Nenhum código… é enviado" entre os fatos guardados de uma decisão, que não é enviada a ninguém. | motor: `runtime/Mariliz.Application/Management/Assistant/EvidenceBuilder.cs:22`, `:36`, `:38-41` |
| G9 | Um único `management-page.module.scss` de 368 linhas para quatro telas e onze componentes. `.portfolioRow` e `.portfolioHead` desenham riscos, decisões, marcos e itens ligados. O `plural` é redefinido em quatro arquivos, embora exista em `shared/utils`. | `management-page.module.scss`, `components/decisions-section.tsx:12`, `milestones-section.tsx:13`, `risk-history-section.tsx:12`, `portfolio-page.tsx:138` |
| G10 | Data em `StatusChip`: a data da decisão vira um selo, que no resto do aplicativo é estado. | `components/decisions-section.tsx:214` |

## 3. Problemas por tela

### 3.1 Portfólio

Em 1440×900, com dois projetos sem ferramenta: o título, uma descrição de duas linhas, duas linhas de projeto (nome,
caminho, "Aberto agora" e "Ver acompanhamento" ou "Abrir projeto", e um aviso com barra laranja "Nenhuma ferramenta
vinculada: não há dados de andamento.") e, logo abaixo, o bloco "Perguntar sobre o portfólio", que ocupa mais da metade da
tela: um parágrafo sobre política de fontes, o aviso de chave, as caixas dos projetos, o campo da pergunta, a ajuda "Entre
3 e 500 caracteres." em cor de destaque, o botão desabilitado e a prévia recolhida. Em 900×650 as duas linhas de projeto e
o começo do bloco de pergunta cabem; o botão fica abaixo da dobra.

| # | Problema | Onde |
| --- | --- | --- |
| PF1 | Com a lista de recentes cheia, mas sem nenhum projeto aberto nesta pasta de dados, a tela diz "Nenhum projeto aberto ainda. Escolha uma pasta na tela inicial." O seletor do topo, ao lado, oferece os recentes. | `portfolio-page.tsx:38-41` |
| PF2 | "Nenhuma ferramenta vinculada" aparece como ponto de atenção, com o mesmo estilo dos sinais reais, uma vez por projeto, e sem ação para vincular. Projeto sem dado não é projeto em risco. | `portfolio-page.tsx:127-133`; motor: `runtime/Mariliz.Application/Management/PortfolioSignals.cs:16` |
| PF3 | A navegação promete "lado a lado", mas cada projeto é um bloco de frases: "Issues: 3 abertos · 1 em andamento · 2 entregues em 8 semanas · 1 com prazo vencido". Comparar dois projetos exige ler e somar de cabeça. **(código)** | `portfolio-page.tsx:112-125` |
| PF4 | Dois nomes para o mesmo projeto: o do `package.json` ("svc-gestao-a") como título e o da pasta no caminho. Mesmo achado do P4 do Configurar. | `portfolio-page.tsx:84-85` |
| PF5 | A tela mistura duas tarefas: ver quem pede atenção e fazer uma pergunta com modelo. A pergunta ocupa mais espaço que a lista, empurra a nota de rodapé ("Um projeto sem o botão de abrir…") para longe da linha a que se refere e repete o Assistente (G2). | `portfolio-page.tsx:56-62`, `components/portfolio-ask.tsx` |
| PF6 | A prévia diz "a pergunta e 9 fatos de 2 projetos" e lista "p1: svc-gestao-a (4 fatos)" e "p2: svc-gestao-b (4 fatos)": a data comum, que entra uma vez, não aparece, e o apelido interno `p1` vai para a tela. | `components/portfolio-ask.tsx:110-134` |

### 3.2 Acompanhamento

Sem ferramenta vinculada, em 1440×900: título, a descrição com o nome do projeto, "Atividades" com "Não há dados de
atividades… Vincule uma em Conexões › Serviços", "Decisões" com o botão "Registrar uma decisão" e o estado vazio, e "Ainda
não calculado" com duas linhas fixas (mudança de escopo e previsibilidade). Previsão, fluxo, riscos e marcos somem sem
aviso. Com o formulário aberto, ele entra entre o título "Decisões" e o estado vazio, que continua lá embaixo.

Com dados, o código monta até dez seções, uma embaixo da outra, nesta ordem: Atividades, Riscos, Histórico dos riscos,
Execuções ligadas a itens, Previsão de entrega, Marcos, Fluxo, Decisões, Fontes de dados, Ainda não calculado.

| # | Problema | Onde |
| --- | --- | --- |
| AC1 | Até dez seções na mesma coluna, sem resumo. Os números de fluxo, que respondem "estamos entregando?", são a sétima seção; a data da leitura das fontes, que diz se dá para confiar nos números, é a nona. Cada risco ocupa cinco linhas (evidências, tendência, itens, o que fazer, regra). **(código)** | `management-page.tsx:71-86`, `components/risks-section.tsx:29-55` |
| AC2 | Sem ferramenta, o que a tela mostraria some calado: as seções de previsão, fluxo e riscos devolvem `null`. A pessoa não sabe o que vai ganhar ao vincular, e a única ação para isso é um link no meio de uma frase. | `components/forecast-section.tsx:16`, `flow-section.tsx:15`, `risks-section.tsx:8`, `management-page.tsx:99-103` |
| AC3 | "Ainda não calculado" é uma seção inteira, sempre presente, com o que o produto ainda não faz. É informação honesta, mas não muda decisão nenhuma no dia a dia. | `management-page.tsx:168-180` |
| AC4 | O formulário de decisão abre entre o título e a lista, com o estado vazio ainda à vista; o botão de enviar parece desabilitado (G5); o texto "Depois de registrada, ela não pode ser editada nem apagada" vem depois dos campos, não antes. | `components/decisions-section.tsx:30-40` e `:154-163` |
| AC5 | "Ver os fatos do dia (6)" abre os fatos do motor como texto corrido, com os problemas do G8, e mistura com eles duas frases sobre o que vai ao provedor de modelo. | `components/decisions-section.tsx:230-244` |
| AC6 | Explicações longas sempre abertas: como as atividades são contadas em cada serviço, o que conta como entregue, lead time e cycle time (um parágrafo de cinco linhas). O método da previsão já fica recolhido; os outros não. **(código)** | `management-page.tsx:119-124`, `components/flow-section.tsx:30-35` |
| AC7 | Nos riscos, os itens vêm numa linha só, separados por vírgula ("Itens: 12 (título), 15 (título), …"), e a regra aparece como identificador ("Regra: …"). **(código)** | `components/risks-section.tsx:44-52` |
| AC8 | O período não aparece nem se escolhe (G3). | `management-page.tsx:47-56` |

### 3.3 Assistente

Em 1440×900, antes de perguntar: título, descrição de duas linhas, o aviso de chave, o campo da pergunta, a ajuda em cor de
destaque, o botão **Perguntar** desabilitado, a caixa "Investigar em etapas", cinco sugestões como botões contornados e a
prévia recolhida ("a pergunta e 4 fatos"). Ao marcar a caixa, o botão vira **Investigar** e habilita. A resposta da
investigação sem modelo chega abaixo da prévia: "Sem o modelo, esta investigação só reuniu os fatos…", "Dados
observados" com um item, "Informações indisponíveis" e a nota final.

| # | Problema | Onde |
| --- | --- | --- |
| AS1 | Dois modos escondidos numa caixa de seleção. Sem chave, o único caminho que funciona é marcar "Investigar em etapas", que o aviso cita entre parênteses; a caixa não diz o que muda, e o rótulo do botão troca com ela. | `assistant-page.tsx:109-123` |
| AS2 | As sugestões vêm depois do botão e só preenchem o campo: quem clica numa sugestão precisa voltar e clicar em Perguntar. | `assistant-page.tsx:124-137` |
| AS3 | Sem modelo, a nota final diz "Escrito por  a partir dos fatos…": o nome do modelo vazio e a afirmação de que um modelo escreveu o que ninguém escreveu. | `components/assistant-answer.tsx:72-75` |
| AS4 | O caminho da investigação mostra identificadores internos ("Fatos: today, src.none, missing.scopeChange, …") e o id da execução ("inv-9ace7aca5223"), com link para a lista de execuções em vez do detalhe dela. | `components/investigation-path.tsx:26` e `:37-40` |
| AS5 | Números que não batem: a prévia diz "4 fatos", o caminho diz "6 fatos". A investigação reúne também execuções e mudanças, mas a tela não diz isso. | `assistant-page.tsx:140-156`, `components/investigation-path.tsx` |
| AS6 | A ajuda "Entre 3 e 500 caracteres." usa a cor de destaque e o tamanho do texto principal: compete com a pergunta. Vem do `TextField` da biblioteca. | `assistant-page.tsx:107`, `components/portfolio-ask.tsx:99` |

### 3.4 Relatório

Em 1440×900: título, a seção "Período" com um só campo ("Largura do período"), a seção "Para levar" com "Copiar como
texto" e a frase "Período de 28/10/2026 às 22:49 a hoje às 22:49…", depois Marcos, Mudanças de risco, Decisões e
Execuções do período, e a nota de base. Ao copiar, a frase do período vira "Relatório copiado." e volta depois de quatro
segundos. Sem ferramenta, fluxo, previsão e riscos somem, enquanto Marcos aparece com uma frase que cita os dois serviços
pelo nome.

| # | Problema | Onde |
| --- | --- | --- |
| RL1 | A ação principal da tela, copiar, fica numa seção chamada "Para levar", como botão contornado. | `report-page.tsx:92-107` |
| RL2 | Uma seção "Período" para um campo só, com o rótulo "Largura do período". | `report-page.tsx:60-70` |
| RL3 | O período aparece com hora ("às 22:49") quando é contado em semanas; a confirmação de cópia apaga a frase do período por quatro segundos. | `report-page.tsx:100-106` |
| RL4 | As seções seguem uma ordem diferente da do Acompanhamento e reaproveitam os mesmos componentes; fluxo, previsão e riscos somem caladas sem dados (o mesmo AC2). | `report-page.tsx:108-117` |
| RL5 | "Investigação de gestão, concluído com limitações: 1": o número no fim e o estado no masculino para uma investigação. | `report-page.tsx:167-171` |

## 4. Componentes locais e a biblioteca (`@mariliz/ui-kit` 0.10.1)

| Hoje | Na biblioteca | Proposta |
| --- | --- | --- |
| Indicadores em `<dl>` com nota e "sem dado" (`.figures`, `.figure`, `Figure` em `flow-section.tsx:133`) | `CardData` (título, valor e cor) | Fica o local: o `CardData` não tem nota nem o estado "sem dado", que é a regra da área (nunca mostrar ausência como zero). Vira um componente da área, fora do SCSS geral. |
| Linhas de projeto em frases (`portfolio-page.tsx:79-136`) | `DataTable` (grade com paginação) | Tabela simples (`<table>` com cabeçalho de coluna), não o `DataTable`: o Portfólio tem poucos projetos e não pede paginação, ordenação nem seleção (DG4). |
| Seções empilhadas no Acompanhamento | `TabBar` (já usado na Revisão do Configurar) | Usar nas partes do Acompanhamento (DG2). |
| Barras semanais em `<table>` com barra só visual | — (não há gráfico) | Fica. |
| `Disclosure` local | — (não há acordeão) | Fica; passa a guardar os métodos e o "Ainda não calculado". |
| Aviso de chave e erro: `InlineNotice`, `ErrorState` e `.errorText` | `Alert` | Um padrão: `InlineNotice` (que já é o `Alert`, com `role="alert"` só no erro), como decidido no C2 do Configurar. |
| Data em `StatusChip` (`decisions-section.tsx:214`) | `StatusChip` | Sai o selo; a data vira texto com `<time>`. |
| Sugestões como `Button` contornado | — (só há `StatusChip`, que não é clicável) | Ficam botões, antes do campo (AS2). |
| Escolha de modo por `Checkbox` | `Radio` | Trocar por dois `Radio` com descrição (AS1). |
| Período por `SelectField` | `SelectField` | Fica, e passa a valer também no Acompanhamento. |
| `plural` e `count` redefinidos em quatro arquivos | — | Usar `shared/utils` (`plural`). |

## 5. Proposta

### Princípios

1. Cada tela responde uma pergunta, e a resposta vem primeiro: Portfólio, "qual projeto pede atenção?"; Acompanhamento,
   "como está este projeto?"; Assistente, "o que eu quero saber?"; Relatório, "o que levo para fora?".
2. Dado ausente nunca vira zero nem some calado: a tela diz o que falta e oferece o caminho para ter.
3. Um período para tudo, à vista e com o mesmo valor em todas as telas.
4. O método fica recolhido, nunca removido. Termo técnico só com `Term` ou dentro do detalhe.
5. Uma ação primária por tela; botão desabilitado sempre diz o que falta.

### Telas: o que muda, o que fica, o que sai

| Tela | Muda | Fica | Sai |
| --- | --- | --- | --- |
| **Portfólio** | Uma tabela com uma linha por projeto e colunas comparáveis: projeto, pontos de atenção, abertos, em andamento, entregues no período, prazo vencido, leitura mais antiga e a ação (Abrir ou Ver acompanhamento). Quem pede atenção primeiro. Projeto sem ferramenta mostra "Sem ferramenta" em tom neutro com "Vincular", e não conta como ponto de atenção. Sem projeto aberto, o estado vazio oferece os recentes e "Abrir outra pasta", como o seletor do topo. | Pontos de atenção por regra, abrir projeto, a explicação de quem não tem botão de abrir (na própria linha). | O bloco "Perguntar sobre o portfólio" daqui; ele vai para o Assistente (DG1). |
| **Acompanhamento** | No topo, uma faixa "O que pede atenção": riscos ativos, itens com prazo vencido e dados incompletos, com a data da leitura mais antiga e o período (que se escolhe ali). Abaixo, abas: *Visão geral* (atividades, previsão e os riscos ativos, resumidos), *Entregas* (fluxo e marcos), *Riscos* (riscos com detalhe e histórico), *Decisões* e *Fontes* (fontes de dados, execuções ligadas a itens e, recolhido, "O que ainda não é calculado"). Sem ferramenta: um estado vazio único no topo, dizendo o que aparece depois de vincular, com "Vincular uma ferramenta" como ação primária; a aba *Decisões* continua disponível. | Todas as seções e números; os métodos (recolhidos); registrar decisão com execuções; os fatos guardados de cada decisão. | O "Ainda não calculado" como seção sempre aberta (vira detalhe recolhido); os parágrafos de método abertos. |
| **Assistente** | Ordem nova: o escopo ("Este projeto" ou "Vários projetos", com as caixas dos projetos), as sugestões, o campo e a escolha de como responder, como dois `Radio` com consequência: "Explicar com o modelo de IA (envia a pergunta e os fatos ao provedor)" e "Só reunir os fatos, em etapas (nada é enviado)". Sem chave, o primeiro fica desabilitado com o motivo e o link para Configurações › Minha IA, e o segundo vem marcado. A sugestão preenche o campo e leva o foco a ele. O caminho da investigação mostra o texto dos fatos e leva ao detalhe da execução. | As quatro partes da resposta, as afirmações descartadas com o motivo, os fatos citados, a prévia do que vai ao provedor, o caminho da investigação, a pergunta que chega do Início. | A caixa "Investigar em etapas". |
| **Relatório** | "Copiar como texto" vira a ação primária, ao lado do título, com a confirmação num lugar próprio (a frase do período fica). O período usa o mesmo campo e o mesmo valor do Acompanhamento, sem hora. Seções na mesma ordem das abas do Acompanhamento; sem ferramenta, uma linha diz o que falta em vez de esconder fluxo, previsão e riscos. | Tudo o que o texto copiado traz; a nota de base; marcos, mudanças de risco, decisões e execuções do período. | As seções "Período" e "Para levar" como títulos. |

Nenhuma capacidade é removida: a pergunta entre projetos muda de tela, e o que sai é título de seção, texto repetido ou
texto que passa a ficar recolhido.

### Incrementos (um PR cada, na ordem)

| # | Incremento | Toca | Risco |
| --- | --- | --- | --- |
| G-1 | **Textos e correções sem mexer na estrutura:** nota do modelo sem modelo (AS3); fatos por texto e sem o id da execução (AS4; o link continua indo para a lista de Execuções, porque a investigação não tem tela de detalhe própria, só a gaveta da lista); motivo nos botões desabilitados (G5); aviso de chave aponta Configurações › Minha IA (G4); data da decisão sem selo (G10); período sem hora (RL3); itens do risco em lista (AC7); `plural` compartilhado (G9); no motor, data local em dia/mês/ano, concordância do "não calculado" e as frases de títulos e de envio só quando cabem (G8). | UI e motor, só textos | Baixo |
| G-2 | **Base comum:** um padrão de aviso e erro (G6); termos novos no glossário e `Term` nos rótulos (G7); o SCSS dividido por componente, com um padrão de linha (G9); a lógica de perguntar num hook único usado pelo Assistente e pelo Portfólio (G2). | UI | Baixo |
| G-3 | **Período comum:** um seletor no Acompanhamento e no Relatório, com o mesmo valor guardado como preferência da tela (G3, AC8, RL2). | `management-page.tsx`, `report-page.tsx`, `service/management-api.ts` | Baixo |
| G-4 | **Portfólio:** tabela, "Sem ferramenta" neutro com Vincular, estado vazio com os recentes, um nome por projeto (PF1-PF4, PF6). O bloco de pergunta sai só no G-5. Depende de DG4. | `portfolio-page.tsx`, motor `PortfolioSignals.cs` | Médio |
| G-5 | **Assistente:** escopo (este projeto ou vários), modo por `Radio`, sugestões antes do campo, contagem de fatos coerente; a pergunta entre projetos passa para cá e sai do Portfólio (AS1, AS2, AS5, PF5). Depende de DG1. | `assistant-page.tsx`, `components/portfolio-ask.tsx`, `portfolio-page.tsx` | Médio |
| G-6 | **Acompanhamento:** faixa "O que pede atenção", abas, estado vazio único, métodos e "Ainda não calculado" recolhidos, formulário de decisão com o aviso antes dos campos (AC1-AC6). Depende de DG2 e, para validar com dados, de DG5. | `management-page.tsx`, `components/*` | Alto |
| G-7 | **Relatório:** copiar como ação primária, ordem igual à do Acompanhamento, sem seções que somem caladas (RL1, RL4, RL5). Depois do G-6. | `report-page.tsx`, `domain/report-markdown.ts` | Baixo |

G-1, G-2 e G-3 não dependem de decisão e podem começar logo. G-4 e G-5 são independentes entre si, mas o G-5 tira o bloco
de pergunta que o G-4 deixa no lugar. G-7 vem depois do G-6. Cada PR é validado no aplicativo real, nas duas larguras, com
o axe, sem erros no console e com projetos de teste descartáveis.

### Estado das entregas

| # | PR | Situação |
| --- | --- | --- |
| G-1 | #214 | Mergeado. O aviso de chave passou depois a apontar para Conexões › Modelos de IA (#224). |
| G-2 | #216 | Mergeado. |
| G-3 | #219 | Mergeado. |
| G-4 | #223 | Mergeado. Ordenação do motor: sem ferramenta não conta como atenção e vem depois de quem tem dado. |
| G-5 | #222 | Mergeado. |
| G-6 | #225 | Mergeado. Faixa "O que pede atenção" e cinco abas (Visão geral, Entregas, Riscos, Decisões, Fontes). Sem ferramenta, um estado só com a ação de vincular. Os estados com dados não foram vistos no aplicativo (seção 6). |
| G-7 | este PR | "Copiar como Markdown" é a ação primária, no cabeçalho; sem as seções "Período" e "Para levar". As seções seguem a ordem das abas do Acompanhamento (Previsão de entrega, Fluxo, Marcos, Riscos, Mudanças de risco, Decisões, Execuções do período), com os mesmos nomes, e o texto copiado segue a mesma ordem. Previsão, fluxo e riscos sem dado ficam no lugar com a frase do Acompanhamento (`NoData`, que agora vive dentro das seções e vale nas duas telas). As execuções saem de "tipo, estado: número" para "tipo · estado · N execuções" (RL5). Os estados com dados não foram vistos no aplicativo (seção 6). |

### Decisões do dono

| # | Decisão | Recomendação |
| --- | --- | --- |
| DG1 | Onde fica a pergunta que cruza projetos: no Assistente, com a escolha "Este projeto / Vários projetos", ou no Portfólio, recolhida. | No Assistente: uma tela para perguntar, um código para isso, e o Portfólio fica com a pergunta "quem pede atenção?". **Decidido em 2026-11-25: no Assistente.** |
| DG2 | O Acompanhamento em abas, com a faixa "O que pede atenção" no topo, ou uma página só, reordenada e com as seções secundárias recolhidas. | Abas: dez seções não cabem numa leitura, e a faixa no topo responde o essencial sem abrir nenhuma. **Decidido em 2026-11-25: abas.** |
| DG3 | O período padrão comum: 8 semanas (o do motor no Acompanhamento e no Portfólio) ou 4 (o do Relatório hoje). | 8 semanas: a previsão usa até 12 de histórico e 4 deixa a média semanal instável. **Decidido em 2026-11-25: 8 semanas.** |
| DG4 | O Portfólio como tabela simples, com estilo local, ou com o `DataTable` da biblioteca. | Tabela simples: o `DataTable` traz paginação e seleção que o Portfólio não usa. **Decidido em 2026-11-25: tabela simples.** |
| DG5 | Como validar os estados com dados: o dono vincula uma ferramenta num projeto de teste (com o token dele, digitado por ele na janela de teste) antes do G-4 e do G-6, ou esses incrementos são validados só nos estados vazios, com os estados com dados cobertos pelos testes. | O dono vincular uma ferramenta: sem isso, a parte maior da proposta (AC1, PF3) não terá sido vista em uso. **Decidido em 2026-11-25: o dono vincula.** |
| DG6 | A sugestão de pergunta preenche o campo e leva o foco a ele, ou já pergunta. | Preencher e focar: perguntar envia dados ao provedor, e isso deve ser um clique consciente. **Decidido em 2026-11-25: preencher e focar.** |

## 6. Validação que falta (etapa própria)

Tudo abaixo passou nos testes automatizados, com as respostas reais do motor como fixture, mas **não foi visto no
aplicativo real**. Os estados vazios, o sem ferramenta e os controles foram validados em 1440×900 e 900×650, com o axe e
sem erros no console. O Relatório sem ferramenta (G-7) também: as sete seções na ordem das abas, três delas com a frase de
"sem dados", a ação de copiar alcançada pelo teclado (Enter e Espaço), a troca de período e o texto copiado, em 1440×900 e 900×650 com
axe sem violações. O que falta precisa de dados que só uma ferramenta vinculada e sincronizada traz, ou de uma chave de
modelo, e por isso depende do dono (DG5): o token é digitado por ele, numa instância do aplicativo com pasta de dados
própria, em projeto de teste descartável.

**Com uma ferramenta vinculada e sincronizada** (de preferência dois projetos, um deles sem ferramenta):

1. **Portfólio:** as linhas por tipo com os números; "sem dado" no prazo vencido quando a fonte não informa; pontos de
   atenção reais (prazo vencido, sincronização incompleta, dados antigos, conexão sem acesso); a ordem (quem pede atenção
   primeiro, depois quem tem dado, depois quem está sem ferramenta).
2. **Acompanhamento:**
   - a faixa, com riscos ativos e quantos são de gravidade alta, prazo vencido, dados incompletos, dados antigos e a
     leitura mais antiga;
   - as cinco abas e a contagem no rótulo de Riscos;
   - fluxo, previsão e marcos com números;
   - riscos com os itens em lista e, depois de duas sincronizações, o histórico dos riscos;
   - as execuções ligadas a itens e o último estado de cada fonte;
   - a troca de período e a decisão registrada com os fatos do dia.
3. **Relatório:** as seções com dados (previsão, fluxo, marcos, riscos, mudanças de risco, decisões e execuções do
   período), com a mesma ordem e os mesmos números do Acompanhamento no mesmo período; e o texto copiado em Markdown,
   conferido contra a tela e colado num editor.

**Com uma chave de modelo configurada:** a resposta direta do Assistente (conferida, com afirmações descartadas), a
investigação em etapas com modelo, a pergunta a vários projetos com a prévia do que vai ao provedor, e a nota "Escrito por"
com o nome do modelo.

**Sem projeto aberto:** o Assistente em "Vários projetos" no aplicativo real (hoje só coberto por teste).

**Com tecnologia assistiva de verdade:** a tabela do Portfólio (grupo de linhas), as abas do Acompanhamento e os grupos de
escolha do Assistente, que o axe e os testes só conferem na marcação.

**Pendências conhecidas, fora desta etapa:** os fatos guardados ao registrar uma decisão sempre dizem "últimas 8 semanas",
mesmo com outro período escolhido na tela (o motor usa o padrão ao registrar); e a investigação de gestão não tem tela de
detalhe própria, só a gaveta da lista de Execuções.
