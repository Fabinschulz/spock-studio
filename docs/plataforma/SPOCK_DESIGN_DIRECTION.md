# Spock Studio: direção visual e de experiência

**Data:** 2026-11-12. Documento 5 da análise de referência. Complementa `docs/redesign/proposta-de-redesign.md`
(direção Workbench, aprovada) e não a substitui: o que aquela proposta decidiu continua valendo.

Este documento é direção, não implementação. Nenhuma tela foi alterada. Os desenhos abaixo são esquemas de estrutura,
não layouts finais.

## 1. Ponto de partida

O que já está decidido e não muda:

- Direção **Workbench**, tema escuro único, acento teal `#33C1BA`.
- Tokens semânticos em `desktop/ui/src/shared/styles/_tokens.scss`, com contraste conferido por teste.
- Tipografia Inter variável; escala 28, 22, 16, 14, 13, 12, 11.
- `@mariliz/ui-kit` como biblioteca oficial de componentes.
- Sem travessão em código e interface; "sem dado" para valor ausente.
- Marca Spock Studio ("olhar lógico"), sem elemento de obra de terceiros.

O que este documento acrescenta: a organização do shell para dois ambientes, os padrões de tela que faltam e a lista
do que reutilizar, evoluir e criar.

## 2. Avaliação da interface atual

### 2.1 O que está bom

| Ponto forte | Onde |
| --- | --- |
| Tokens disciplinados: nenhuma cor literal fora dos tokens | `_tokens.scss` e os 45 módulos SCSS |
| Honestidade de estado: nada aparece pronto, zero ou online sem o motor dizer | Todas as telas de Gestão; estado do motor |
| A jornada Configurar: linha do tempo, frase de estado, painel de contexto, aprovação explícita | `features/configure-project` |
| Estados vazios escritos com o próximo passo | Gestão, Integrações |
| Base de acessibilidade: landmarks, estado em texto e forma, foco visível, movimento reduzido | Shell e componentes; oito suítes com axe |
| Privacidade visível: caminho abreviado, "o que será enviado" antes de perguntar | Assistente, Integrações |

### 2.2 Problemas encontrados

| # | Problema | Evidência | Impacto |
| --- | --- | --- | --- |
| 1 | O projeto ativo não aparece no shell; trocar exige voltar ao Início; a seleção não sobrevive a reinício | `AppShell.tsx`, `project-selection-context.tsx` | Alto |
| 2 | Engenharia e Gestão são só rótulos de grupo | `SideNav.tsx` | Alto |
| 3 | Projeto e Acompanhamento são relatórios de texto em 46rem, sem resumo nem visualização; metade da janela vazia em telas largas | `project-page.module.scss`, `management-page.module.scss` | Alto |
| 4 | Sem esqueleto de página: cabeçalho, seção, linha, vazio, carregando e erro copiados em quatro módulos | Os dois acima, mais `integrations-page.module.scss` e `execution/components/workspace.module.scss` | Alto |
| 5 | A faixa superior repete o estado do motor e não tem título, trilha nem ações | `AppHeader.tsx` | Médio |
| 6 | Pedido e Execução sem lugar no menu; execuções sem link; "Configurar" ao lado de "Configurações"; ícone repetido | `SideNav.tsx`, `project-page.tsx` | Médio |
| 7 | Retorno de ação fraco: carregando é uma frase; sucesso não é confirmado; erro longe da linha; "Remover" sem confirmação | `connections-section.tsx` | Médio |
| 8 | A jornada perde o estado ao sair; o stepper não é navegável; foco e rolagem não tratados | `use-configure-journey.ts` | Médio |
| 9 | Vocabulário técnico em telas de gestor; mistura de português do Brasil e europeu | `management-page.tsx`, `model-settings-page.tsx` | Médio |
| 10 | Quatro fontes de ícone; dois indicadores de estado; dois seletores de pasta; componentes sem consumidor | `shared/components/ui/` | Baixo, mas cumulativo |

## 3. Referências de UX/UI

O Kaivor foi estudado como referência de experiência (ver `KAIVOR_PRODUCT_ANALYSIS.md`, seção 13). O que se aproveita
são princípios de uso. Marca, paleta, ícones, cena de fundo, textos e composições do Kaivor não são reproduzidos.

### 3.1 Princípios aproveitados

| # | Princípio | Onde foi observado no Kaivor | Aplicação no Spock |
| --- | --- | --- | --- |
| 1 | Contexto sempre visível | Projeto, branch, modelo e acesso no campo de mensagem | Projeto e ambiente no topo do shell |
| 2 | Navegação em níveis | Poucos itens fixos, ferramentas fixáveis, busca | Itens do ambiente, Plataforma comum, busca |
| 3 | Painel lateral em vez de modal central | Regra do produto para edição, permissão e confirmação | Detalhe, edição e confirmação em painel lateral |
| 4 | Retorno ao lado da ação | Regiões de estado junto do controle, quase sem avisos flutuantes | Retorno por ação, na linha |
| 5 | A janela não rola; rola a menor região | Lista, editor ou painel rolam | Lista e painel de detalhe com rolagem própria |
| 6 | Densidade com teto | Controles de 30 a 40 px; prioridade por posição e rótulo | Controle de 36 px já é o padrão; manter |
| 7 | Estados como critério de pronto | Vazio, carregando, erro, sucesso, texto longo, janela estreita e teclado conferidos | Lista de conferência por tela (seção 10) |
| 8 | Rascunho isolado | Edição só vale ao salvar | Estado da jornada preservado por projeto |
| 9 | Detalhes técnicos recolhidos | JSON e identificadores atrás de "Detalhes técnicos" | Duas camadas de texto (seção 8) |
| 10 | Botão desabilitado explica o motivo | Regra escrita | Mesma regra |
| 11 | Confirmação diz o que não será afetado | Painel de confirmação destrutiva | Mesmo conteúdo, em um só padrão |
| 12 | Vazio diferente de filtro sem resultado | Central de execuções | Central, portfólio, catálogo |
| 13 | Atividade da IA como resumo recolhível | Linha de atividade por resposta | Resumo de uma linha no topo da execução |
| 14 | Honestidade de medição | "Estimativa; não representa cobrança" | Já é prática: "sem dado", confiança com motivo |

Telas e fluxos de onde vieram: shell e conversa (`.impeccable/review/chat-1440-dark.png` no repositório do Kaivor),
rail expandido (`projects-dark.png`), divisão de conteúdo (`file-split-dark.png`), configurações como modo
(`settings-completion/`), página de conexões (`ConnectionsHome`), central de execuções
(`src/features/executions/`), painel de tarefas (`src/features/autonomy/tasks-panel.tsx`), e a prévia de produto do
site (abas Bot, Tarefas, Rotinas, Conexões, Notas). As capturas não foram copiadas para este repositório.

### 3.2 O que não se aproveita

| Do Kaivor | Por quê |
| --- | --- |
| Neutros sem acento cromático | O teal é identidade do Spock |
| Temas claro e escuro | Decisão de tema escuro único |
| Cena de fundo e vidro no campo de mensagem | Decoração sem função num produto centrado em projeto |
| Fonte de sistema | Inter já está em uso |
| Composição centrada na conversa | O centro do Spock é o projeto |
| Kanban de execuções | Entra como filtro por estado na central, não como quadro |
| Canvas de diagrama | Fora do escopo |

### 3.3 O que evitar, aprendido com os problemas do Kaivor

- Três gerações visuais convivendo: uma mudança de direção precisa terminar antes de outra começar.
- Cinco famílias de ícones apesar da norma.
- Estilos em cascata de compatibilidade (62 folhas, 752 `!important`): regra antiga escondeu controle primário.
- Vários nomes para o mesmo conceito (Rotinas, Tarefas, Cortex, Automações).
- Capacidades pré-selecionadas em massa (181 caixas marcadas).
- Expor o modelo interno antes de a pessoa concluir uma tarefa útil.

## 4. Hierarquia de navegação

### 4.1 Shell proposto

```
+--------------------------------------------------------------------------------+
| (1) [olhar] Spock   (2) api-pagamentos v   (3) [Engenharia | Gestão]   (4) [/] |
+----------------+---------------------------------------------------------------+
| (5)            | (6) Harness                              [Abrir Configurar]   |
|  Visão geral   |     api-pagamentos / Harness / Estado                         |
|  Harness       +-----------------------------------------+---------------------+
|  Configurar    | (7)                                     | (8)                 |
|  Pedidos       |   [Estado] [Catálogo] [Revisões]        |  Agente revisor     |
|  Conhecimento  |                                         |  O que faz          |
|  Execuções     |   linha  .  meta  .  estado             |  Quando age         |
|                |   linha  .  meta  .  estado   <-------- |  O que lê           |
|                |   linha  .  meta  .  estado             |  O que pode alterar |
|  ------------  |                                         |                     |
| (9) Plataforma |                                         |  [Dispensar]        |
|  Integrações   |                                         |                     |
|  Modelos       |                                         |                     |
|  Execuções     |                                         |                     |
|  Preferências  |                                         |                     |
| (10) Motor: ok |                                         |                     |
+----------------+-----------------------------------------+---------------------+
```

| # | Elemento | Função |
| --- | --- | --- |
| 1 | Marca | Volta à Visão geral do ambiente atual |
| 2 | Seletor de projeto | Menu com recentes, "Abrir pasta" e "Todos os projetos". Mostra o nome e, ao abrir, o caminho abreviado |
| 3 | Alternador de ambiente | Controle segmentado. Mantém o projeto ao trocar |
| 4 | Busca | Paleta de comandos (P2). Até lá, o espaço fica reservado |
| 5 | Navegação do ambiente | Muda com o alternador |
| 6 | Cabeçalho de página | Título, trilha, ação primária. Um só componente |
| 7 | Área principal | Lista, abas, formulário ou relatório |
| 8 | Painel de detalhe | Abre ao escolher uma linha; fecha com Esc; redimensionável (P2) |
| 9 | Plataforma | Comum aos dois ambientes |
| 10 | Estado do motor | Um só lugar. Sai da faixa superior |

Com o rail recolhido (abaixo de 864 px, como hoje), os itens 2 e 3 continuam no topo; o painel de detalhe sobrepõe a
lista, com fundo escurecido, foco preso e Esc.

### 4.2 Regras de navegação

- A faixa superior deixa de mostrar tagline e versão. A versão vai para Plataforma, em Sobre.
- "Configurar" (jornada) e "Preferências" (antiga Configurações) deixam de ter nomes parecidos.
- Um ícone por item, sem repetição, todos do ui-kit.
- Trilha em toda página de segundo nível, pelo mesmo cabeçalho de página.
- Estado vivo no menu: ponto de atividade em Execuções quando há execução em andamento; marcador quando há aprovação
  pendente.
- Sem projeto aberto, os itens que dependem de projeto ficam indisponíveis com o motivo, como o "Catálogo" de hoje.

## 5. Organização dos workspaces

Os dois ambientes usam o mesmo shell, os mesmos componentes e os mesmos tokens. Diferem em três coisas.

| | Engineering Workspace | Management Workspace |
| --- | --- | --- |
| Unidade principal | O projeto e o que está instalado nele | O projeto e o que está sendo entregue |
| Vocabulário | Pode usar nomes técnicos na primeira camada | Linguagem de tarefa; nome técnico só em "Detalhes técnicos" |
| Forma dominante | Lista e detalhe; linha do tempo; diff | Indicador, gráfico simples, tabela, texto com evidência |
| Página de entrada | Estado do Harness, última execução, aprovações pendentes, próximo passo | Sinais do projeto, previsão, riscos abertos, data da leitura |

Identidade por ambiente sem segunda paleta: o teal continua sendo o único acento. A diferença aparece no rótulo do
alternador, no título da Visão geral e no conteúdo. Uma segunda cor de acento criaria a impressão de dois produtos.

### 5.1 Visão geral de Gestão (esquema)

```
+--------------------------------------------------------------------------+
| Visão geral                                       Lido há 2 h [Atualizar] |
| api-pagamentos / Gestão                                                   |
+--------------------------------------------------------------------------+
| Em andamento   Entregues/sem.   Lead time (mediana)   Prazo vencido       |
|     12              4,5              6,0 dias               3             |
|  issues          8 semanas        8 semanas             de 21 com prazo   |
+-------------------------------------+------------------------------------+
| Previsão de entrega                 | Riscos abertos                     |
|  provável   12 dez                  |  alta   Prazo vencido (3 itens)    |
|  [faixa: otimista .. conservador]   |  média  Em andamento parado (2)    |
|  confiança média: 2 motivos   >     |  Ver todos                    >    |
+-------------------------------------+------------------------------------+
| Ainda não calculado: cycle time, bloqueios (motivo)                   >  |
+--------------------------------------------------------------------------+
```

Regras: cada número traz a base e o período; indicador sem base mostra "sem dado" e o motivo; a data da leitura fica
no cabeçalho; nada de número por pessoa.

### 5.2 Central de execuções (esquema)

```
+--------------------------------------------------------------------------+
| Execuções                                                                 |
| [buscar]  Tipo v   Estado v   Projeto v   Período v          [Limpar]     |
+------------------------------------------------+-------------------------+
| ok     Configuração    api-pagamentos   2 min  | Configuração            |
| !      Pedido          api-pagamentos   40 s   | Concluída com ressalvas |
| ...    Sincronização   portal-web       agora  |                         |
| x      Validação       api-pagamentos   12 s   | Pedido / Feito /        |
|                                                | Validado / Ficou de fora|
|                                                |                         |
|                                                | Etapas             >    |
|                                                | Evidências         >    |
|                                                | Detalhes técnicos  >    |
|                                                |                         |
|                                                | [Reverter] [Validar]    |
+------------------------------------------------+-------------------------+
```

Regras: abrir o detalhe nunca dispara nada; ações que mudam algo ficam no rodapé do painel, separadas da leitura;
"Nenhuma execução ainda" é diferente de "Nenhuma execução com estes filtros".

## 6. Hierarquia de telas

| Nível | Engenharia | Gestão | Plataforma |
| --- | --- | --- | --- |
| 1 | Visão geral | Visão geral | Integrações |
| 1 | Harness | Portfólio | Modelos de IA |
| 2 | Harness / Estado, Catálogo, Revisões | Portfólio / projeto (abre a Visão geral dele) | Integrações / conexão (painel) |
| 3 | Catálogo / componente (painel) | | |
| 1 | Configurar (jornada, seis etapas) | Acompanhamento | Execuções |
| 2 | | Acompanhamento / Fluxo, Previsão, Riscos | Execuções / execução (painel ou página) |
| 1 | Pedidos | Marcos e bloqueios | Preferências |
| 2 | Pedidos / novo pedido | | Diagnóstico |
| 1 | Conhecimento | Decisões | Sobre |
| 1 | Execuções do projeto | Assistente | |
| | | Relatórios | |

Padrões de página, para que toda tela nova caia em um deles:

| Padrão | Uso | Exemplo |
| --- | --- | --- |
| Visão geral | Indicadores no topo, blocos de resumo com "ver mais" | Visão geral dos dois ambientes |
| Lista e detalhe | Lista filtrável e painel lateral | Execuções, Catálogo, Portfólio, Integrações |
| Página com abas | Um objeto com várias facetas | Harness, Acompanhamento |
| Jornada | Etapas com painel de contexto | Configurar |
| Formulário curto | Uma ação, poucas entradas | Novo pedido, nova conexão |
| Pergunta e resposta | Entrada, o que será enviado, resposta em seções | Assistente |

## 7. Diretrizes visuais

| Tema | Diretriz |
| --- | --- |
| Cor | Teal `#33C1BA` só para ação primária, seleção e foco. Cor de estado só no marcador e no texto do estado. Nenhuma cor nova sem token e sem passar pelo teste de contraste. |
| Superfícies | Composição de painéis, não pilha de cartões. Separação por tom e linha fina. Cartão só para indicador e para atividade da Visão geral. |
| Elevação | Sombra só em menu e painel sobreposto. |
| Tipografia | Aplicar a escala existente por componente, não por tela: título de página 22, título de seção 16, corpo 14, interface 13, legenda 12, selo 11. Números com algarismos tabulares. Nenhum `h1` sem classe. |
| Espaço | Base de 4 px já definida. Respiro de página 24; entre seções 24; dentro de seção 12; em linha de lista 8 a 12. |
| Densidade | Controle de 36 px; linha de lista de 40 a 48 px; alvo mínimo de 32 px. |
| Largura | Texto corrido até 46rem. Lista, tabela, grade de indicadores e lista com detalhe usam a largura disponível. |
| Rolagem | A janela não rola. Rolam a área principal e o painel de detalhe, cada um por si. |
| Movimento | 120, 220 e 360 ms, já definidos. Painel lateral entra em 220 ms. Movimento reduzido respeitado. |
| Ícones | Só os do ui-kit, com traço e tamanho únicos (16 em linha, 20 no menu). |
| Estado | Sempre forma mais texto, nunca só cor. |
| Gráfico | Só números do motor. Sem eixo decorativo; valor escrito ao lado; faixa de previsão com os três cenários nomeados. |

## 8. Texto

- Português do Brasil em tudo. Corrigir "Guardar" para "Salvar" e "A carregar" para "Carregando".
- Sem travessão; "sem dado" para valor ausente (regra existente).
- Duas camadas. Na frente, linguagem de tarefa. Dentro de "Detalhes técnicos", o nome técnico.

| Na frente | Em "Detalhes técnicos" |
| --- | --- |
| "Componentes que você dispensou" | `components.exclude` em `mariliz.yaml` |
| "8 de cada 10 simulações terminam até esta data" | percentil 85 |
| "Verificações" | Evals |
| "Regra de proteção dos agentes" | hook PreToolUse |
| "Registro do que foi instalado" | `mariliz.lock` |

- Legenda de uma frase; a explicação longa vai para "Como isto é calculado".
- Botão com verbo e objeto ("Sincronizar agora", "Abrir Configurar").
- Botão primário desabilitado sempre diz o que falta.
- Nunca mostrar sucesso sem confirmação do motor.

## 9. Componentes

A regra é: primeiro o que o ui-kit já exporta; depois, compor no aplicativo sobre o ui-kit; só então criar.

Observação: o ui-kit está publicado e não está em desenvolvimento ativo. Por isso os componentes novos nascem na camada
compartilhada do aplicativo (`desktop/ui/src/shared/components/ui/`). Levar algum deles para o ui-kit é decisão à parte.
O `package.json` fixa a versão 0.6.0; vale conferir se há versão mais nova publicada antes de começar.

### 9.1 Existentes no ui-kit a reutilizar

| Componente do ui-kit | Uso hoje | Uso proposto |
| --- | --- | --- |
| `Drawer` | Nenhum | Painel de detalhe, edição de conexão, confirmação destrutiva. Substitui o painel próprio da jornada quando for seguro |
| `Menu`, `MenuItem` | Nenhum | Seletor de projeto; ações por linha |
| `TabBar`, `TabItem` | Nenhum | Harness (Estado, Catálogo, Revisões); Acompanhamento (Fluxo, Previsão, Riscos) |
| `DataTable`, `TableComponent` | Nenhum | Portfólio, central de execuções, vazão por semana |
| `LoadingBar` | Nenhum | Carregamento de página e de seção |
| `SelectField`, `SelectFormField` | Nenhum | Filtros da central e do portfólio; período dos indicadores |
| `Switch` | Nenhum | Política de contextualização; preferências |
| `Divider` | Nenhum | Separação de grupos no menu e em painéis |
| `Breadcrumb` | Uma tela | Todas as páginas de segundo nível, via cabeçalho de página |
| `Modal` | Uma tela | Só onde bloquear é o correto; o restante vai para `Drawer` |
| `Typography` | Uma tela | Títulos de página e de seção |
| `Alert` | Nove arquivos | Continua; unificar com `InlineNotice` |
| `Button`, `TextField`, `Checkbox`, `FormProvider` | Em uso | Continuam |
| Ícones (cerca de 426) | 16 em uso | Única fonte de ícones |

### 9.2 Componentes locais que precisam evoluir

| Componente | Situação | Evolução |
| --- | --- | --- |
| `PageHeader` | Usado em uma tela; oito refazem à mão | Vira o cabeçalho de toda página: título, trilha, descrição curta, ações |
| `AppShell`, `SideNav`, `AppHeader` | Menu único, topo redundante | Seletor de projeto, alternador de ambiente, navegação por ambiente |
| `StatusChip` e `StatusGlyph` | Dois para a mesma ideia | Um indicador de estado com duas apresentações (selo e marca) |
| `ContextPanel` e `JourneyFrame` | Painel próprio, sem Esc nem foco preso | Mesmo comportamento do painel lateral: Esc, foco, fundo |
| `WizardSteps` | Etapas não navegáveis | Etapa concluída vira navegável |
| `EvidenceDisclosure` e os `details` soltos | Onze arquivos, estilos distintos | Um componente de divulgação |
| `ProjectPicker` e `FolderPickerCard` | Dois seletores de pasta, duas listas de recentes | Um seletor, usado no shell e na jornada |
| `SectionCard` | Um uso remanescente | Substituir pela seção padrão |
| `Glyph`, `FolderIcon`, SVG soltos | Três fontes de ícone além do ui-kit | Trocar pelos ícones do ui-kit |
| `Checklist`, `CommandDisplay` | Sem consumidor | Remover ou adotar |
| Leitura assíncrona | Quatro padrões | `useAsyncResource` como único padrão de leitura |

### 9.3 Componentes novos realmente necessários

Cada um substitui código repetido que já existe ou atende uma tela proposta. Nenhum é decorativo.

| Componente | Substitui ou atende | Compõe |
| --- | --- | --- |
| Página (`Page`) | Classes `.page`, `.title`, `.lead` em quatro módulos | `PageHeader`, área de conteúdo |
| Seção (`Section`) | Classes `.section` e cabeçalhos refeitos | Título, ação, estados |
| Linha de lista (`ListRow`) | Classes `.row`, `.rowTitle`, `.meta` | Título, meta, estado, ações |
| Estado de carregamento | `<p role="status">Lendo</p>` | `LoadingBar` e rótulo |
| Estado vazio | Parágrafos soltos | Título, texto, ação |
| Erro de seção | `<p role="alert">` | `Alert`, "Tentar de novo", detalhe técnico |
| Retorno de ação | Ausência de confirmação | Região de estado na linha, com sucesso e erro |
| Painel de detalhe | Tela de execução sem contexto; ficha do catálogo | `Drawer` |
| Confirmação | Modal, inline e ausente | `Drawer` com alvo, efeito e o que não muda |
| Seletor de projeto | Troca só no Início | `Menu` |
| Alternador de ambiente | Ambientes como rótulo | Controle segmentado sobre `TabBar` ou botões |
| Indicador (`Metric`) | `<dl>` de indicadores | Valor, base, período, "sem dado" com motivo |
| Gráfico de barras e faixa de previsão | Tabela com barras em CSS | SVG simples, com valores escritos |
| Barra de filtros | Central e portfólio | `TextField`, `SelectField` |
| Resumo de entrega | Topo da execução | Quatro campos: pedido, feito, validado, ficou de fora |
| Dica (`Tooltip`) | Atributo `title` como único meio de ver texto cortado | A avaliar no ui-kit; senão, local |

Ficam para depois: paleta de comandos, divisor redimensionável, linha do tempo como componente genérico (a da jornada
serve), esqueleto de carregamento.

## 10. Proposta de Design System

O design system do Spock é a soma de três camadas, com uma regra por camada.

| Camada | Onde | Regra |
| --- | --- | --- |
| Tokens | `shared/styles/_tokens.scss` e tema do ui-kit | Toda cor, espaço, raio e tempo vem de token. Token novo passa pelo teste de contraste. Limpar apelidos de primitivo que apontam para o mesmo valor. |
| Primitivos | `@mariliz/ui-kit` | Botão, campo, seleção, aba, menu, tabela, painel, modal, alerta, ícone. Não recriar localmente o que o ui-kit exporta. |
| Padrões do produto | `shared/components/ui/` | Página, seção, linha, estados, indicador, painel de detalhe, confirmação, resumo de entrega. Compostos sobre os primitivos. |

Cada padrão do produto registra, num arquivo curto ao lado do componente: quando usar, quando não usar, os estados que
cobre e o teste que o protege. Não é um site de documentação: é o mínimo para que a próxima tela não reinvente.

Lista de conferência por tela, antes de dar como pronta:

1. Vazio, carregando, erro e sucesso tratados.
2. Vazio diferente de filtro sem resultado.
3. Texto longo e número grande não quebram o layout.
4. Funciona em 760 px e usa a largura em 1440 px.
5. Operável só por teclado; foco visível; Esc fecha o que abre.
6. Estado em forma e texto.
7. Nenhuma cor, ícone ou espaçamento fora do sistema.
8. Texto em português do Brasil, sem travessão, sem jargão na primeira camada em telas de Gestão.
9. Botão desabilitado diz o motivo.
10. Toda ação tem retorno visível.

## 11. Recomendações de consistência visual

1. **Uma mudança por vez, até o fim.** Migrar as telas para os padrões novos tela a tela, e remover o padrão antigo ao
   terminar cada uma. Não deixar duas gerações convivendo.
2. **Um nome por conceito.** Execução (não "run", "rodada" ou "operação" na interface), Conexão, Vínculo, Verificação,
   Revisão, Componente. Escrever o glossário antes das telas novas.
3. **Um componente por ideia.** Um indicador de estado, um seletor de pasta, um painel lateral, uma divulgação.
4. **Uma fonte de ícones.**
5. **Um padrão de leitura assíncrona.**
6. **Testes acompanham.** Os testes de shell, rotas, tokens e tipografia são atualizados junto com a mudança, não
   removidos. O teste de contraste por nome de token impede renomear token sem intenção.
7. **Capturas novas.** As de `docs/redesign/screenshots/` são anteriores a Gestão e à marca Spock. Refazer ao fim de
   cada fase, no mesmo conjunto de larguras.
8. **Conferência no Tauri empacotado**, com leitor de tela, zoom e alto contraste, que nunca foi feita.

## 12. O que não foi verificado

- Nenhuma tela do Spock foi renderizada para este documento; a avaliação vem do código e da documentação.
- O comportamento real dos componentes do ui-kit ainda não usados (`Drawer`, `TabBar`, `DataTable`, `Menu`) não foi
  testado: é preciso conferir foco, Esc e tema escuro antes de adotá-los.
- O Kaivor não foi executado; os princípios vêm de código, specs, capturas do repositório dele e do site público.
- Nenhum teste com usuário foi feito, nem aqui nem no Kaivor.
