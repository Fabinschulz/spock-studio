# Spock Studio: roadmap de implementação

**Data:** 2026-11-12. Documento 6 da análise de referência. Base: `SPOCK_PRODUCT_EVOLUTION.md` (o quê) e
`SPOCK_DESIGN_DIRECTION.md` (como se vê).

Este roadmap é uma proposta de ordem. Não é autorização para implementar: cada fase começa com a aprovação do dono do
produto, como nas fases anteriores da plataforma.

## 1. Como este roadmap se encaixa no que já existe

- As fases 1 a 6 de `evolucao-engineering-management.md` estão entregues ou em andamento. Este roadmap não as refaz.
- A ordem aprovada em 2026-11-11 é respeitada: (1) revisões do Harness, feita; (2) execuções ligadas à entrega; (3)
  cycle time e bloqueios; (4) decisões gerenciais com evidência técnica; (5) agentes técnicos; (6) histórico
  consolidado e investigação com LangGraph e RAG. A tabela da seção 3 mostra onde cada uma cai.
- O que a análise acrescenta são duas fases antes (validação e fundações de interface) e uma de hub. Elas entram
  porque as entregas aprovadas dependem delas: uma central de execuções sem padrão de lista e detalhe repetiria o
  problema da tela Projeto, e indicadores novos sobre conectores nunca validados seriam número sem base.
- As fases têm letras (A a G) para não colidir com a numeração das fases da plataforma.

Regras que valem para todas as fases:

1. Arquitetura preservada: o motor .NET decide e persiste; React apresenta; Python orquestra só onde há estado,
   roteamento ou pausa humana; Tauri hospeda. A interface não fala com o Python.
2. Cada método novo entra nos três lugares (interface, host, motor), com fixture de contrato.
3. Cada indicador novo entra com fórmula escrita e teste conferido à mão.
4. Nada de dado simulado. O que não tem base aparece como "sem dado", com o motivo.
5. Nenhuma dependência nova sem aprovação.
6. Sem refatoração global. As mudanças de interface são tela a tela.
7. Suítes ao fim de cada fase: `dotnet test`, `pnpm --dir desktop/ui check`, build da interface, `cargo test`.

## 2. Visão geral

| Fase | Nome | Valor principal | Tamanho | Depende de |
| --- | --- | --- | --- | --- |
| A | Validar o que existe | Confiança nos dados e segredo protegido | Pequeno | Credenciais de teste (decisão do dono) |
| B | Fundações de interface e shell | Projeto e ambiente sempre visíveis; telas consistentes | Médio | Nada |
| C | Central de execuções ligada à entrega | Um lugar para ver o que aconteceu | Médio | B |
| D | Gestão: tempo, bloqueios, marcos, decisões | Gestão explica por que, não só quanto | Médio a grande | A, B |
| E | Catálogo do Harness | Entender o que está instalado no projeto | Médio | B |
| F | Hub de integrações, nível 2 | Mais de um provedor de IA; dado externo como contexto | Médio a grande | A |
| G | Inteligência compartilhada | Engenharia e Gestão cruzadas, com evidência | Grande | C, D, F |
| Depois | Itens P2 e P3 | Ver seção 5 | | |

```
A ----+--------------------> D ----+
      |                            |
      +--------------------> F ----+--> G
B ----+--> C ----------------------+
      +--> E
```

A e B podem andar em paralelo. C e E dependem só de B. D pede A e B. G fecha o ciclo.

## 3. Fases

### Fase A: validar o que existe

**Por que primeiro.** A lição mais clara do Kaivor é o custo de acumular funcionalidade "implementada, validação real
pendente". O Spock tem hoje três pontos assim: sincronização real do GitHub, todo o conector do Jira e o assistente
gerencial com modelo real. Além disso, a chave de modelo está em texto puro.

| Entrega | Camada | Observação |
| --- | --- | --- |
| Sincronização real de um repositório GitHub de teste | Motor | Conferir paginação, limite de chamadas e ponto de retomada com dado real |
| Sincronização real de um projeto Jira de teste | Motor | O conector foi escrito só com dublês; tratar como não comprovado até aqui |
| Assistente gerencial com modelo real | Motor | Conferir se o verificador descarta o que deve e não descarta demais |
| Chave de modelo no cofre | Motor, interface | Migrar `model.json` para o cofre que as conexões já usam; ler o arquivo antigo uma vez e apagá-lo |
| Registro do que foi validado e do que não foi | Docs | Atualizar a seção de validação do documento da plataforma |

- **Depende do dono:** credencial de teste do GitHub e do Jira; chave de modelo; decisão 3 da seção 13 do documento
  da plataforma (migrar a chave).
- **Risco:** o conector do Jira pode exigir correção ao encontrar a API real. É melhor descobrir agora.
- **Pronto quando:** as três validações estiverem escritas com o resultado, inclusive o que falhou, e nenhum segredo
  estiver em texto puro.

**Estado da fase A (2026-11-12).**

- **Feito: chave de modelo no cofre.** Onde há cofre (Windows, DPAPI do usuário), a chave vai para o mesmo cofre das
  conexões, sob um identificador reservado que o serviço de conexões nunca gera. Quem tinha `model.json` é migrado na
  primeira leitura, e o arquivo só some depois de o cofre devolver exatamente a mesma chave: qualquer falha do cofre
  deixa o arquivo e a chave continua valendo. Sem cofre (Linux e macOS) o comportamento antigo se mantém, e a central já
  avisa que ali o cofre não existe. Provado por 17 testes, dois deles com a DPAPI real (nenhum arquivo contém a chave).
- **Pronto para rodar, sem resultado ainda: as três validações reais.** `LiveValidationTests` fala com GitHub, Jira e
  OpenAI de verdade, só quando a pessoa define as variáveis no próprio terminal; sem elas os testes aparecem como
  ignorados, nunca como aprovados. Cada um confere consistência da contagem, idempotência da segunda sincronização,
  isolamento ao recorte vinculado e ausência da credencial em qualquer arquivo, e imprime um relatório só com contagens.
- **Falta do dono:** as credenciais de teste (itens 1 e 2 da seção 7) e rodar o comando abaixo. Até o relatório voltar, o
  conector do Jira e o assistente com modelo real continuam como **não comprovados**.

```powershell
# Defina no terminal, nunca no chat. Basta definir os grupos que quiser validar.
$env:MARILIZ_LIVE_GITHUB_TOKEN = "..."; $env:MARILIZ_LIVE_GITHUB_REPO = "dono/repositorio"
$env:MARILIZ_LIVE_JIRA_SITE = "https://equipe.atlassian.net"; $env:MARILIZ_LIVE_JIRA_EMAIL = "..."
$env:MARILIZ_LIVE_JIRA_TOKEN = "..."; $env:MARILIZ_LIVE_JIRA_PROJECT = "CHAVE"
$env:MARILIZ_LIVE_OPENAI_KEY = "..."
dotnet test runtime/tests/Mariliz.Application.Tests --filter "FullyQualifiedName~LiveValidationTests" --logger "console;verbosity=detailed"
```

### Fase B: fundações de interface e shell

**Por que agora.** Toda tela nova das fases seguintes precisa de página, seção, linha, estados e painel de detalhe.
Sem isso, cada uma copiaria o SCSS de novo. E o problema de experiência de maior impacto (projeto invisível) é barato.

| Entrega | Camada | Observação |
| --- | --- | --- |
| Componentes de página, seção, linha, vazio, carregando, erro, retorno de ação, divulgação | Interface | Em `shared/components/ui/`, sobre o ui-kit |
| Painel de detalhe e confirmação sobre o `Drawer` do ui-kit | Interface | Conferir foco, Esc e tema escuro do `Drawer` antes de adotar |
| Seletor de projeto no shell; seleção persistida | Interface, host | O host já guarda os recentes; falta guardar o último aberto |
| Alternador de ambiente e navegação por ambiente | Interface | Rotas atuais mantidas |
| Estado do motor em um só lugar; faixa superior com projeto e ambiente | Interface | Atualizar `app-shell.test.tsx` |
| Visão geral por ambiente | Interface | Só com métodos que já existem (`engineering.*`, `management.*`, `harness.runs`) |
| Portfólio navegável | Interface | Abrir o projeto pela linha |
| Migração das telas existentes para os componentes novos | Interface | Uma tela por vez; remover o padrão antigo ao terminar cada uma |
| Português do Brasil em tudo; um ícone por item; uma fonte de ícones | Interface | |
| Estado da jornada preservado por projeto | Interface | Não perder o plano ao sair da rota |

- **Não precisa de método novo no motor**, com uma exceção pequena: nome de exibição do projeto, se for editável.
- **Risco:** reorganizar o shell quebra testes de shell e de rotas. Eles são atualizados junto, não removidos.
- **Pronto quando:** nenhuma tela usa as classes copiadas; a lista de conferência por tela (seção 10 de
  `SPOCK_DESIGN_DIRECTION.md`) passa em todas; capturas novas em 760 e 1440 px.

**Estado da fase B (2026-11-12).** Primeira entrega feita na branch `feat/fase-b-shell-contexto`, sem commit até o
dono pedir. Só interface; motor, host, contratos e protocolo não mudaram.

| Feito | Observação |
| --- | --- |
| Seletor de projeto no topo do shell | Painel de disclosure com recentes, "Abrir outra pasta" e "Todos os projetos". Reaproveita o botão de recente que já existia, em vez de criar uma terceira lista |
| Último projeto reaberto ao abrir o aplicativo | Usa o primeiro dos recentes do host (já ordenados). Falha em silêncio; uma escolha da pessoa vence a restauração |
| Alternador Engenharia e Gestão | Links, com o ambiente atual marcado por `aria-current` e por forma. Nas telas comuns (Início, Integrações, Configurações) vale o último ambiente, guardado no armazenamento local |
| Menu só do ambiente atual, mais Plataforma | Projeto ganhou ícone próprio (não repete o de Configurar) |
| Estado do motor em um lugar só | Saiu do topo; a versão foi para o rodapé do menu; a tagline saiu |
| Portfólio navegável | A linha do projeto aberto leva ao acompanhamento; as outras abrem o projeto quando casam com um recente |
| Testes | 298 na interface (eram 255 antes da fase), mais tipos, lint, formato e build limpos. Prévia visual conferida em 1280 e 760 px com host simulado; nada foi conferido no Tauri empacotado |

Limite conhecido: o host guarda os recentes por caminho e não conhece o `projectId` do motor. Por isso só o projeto
aberto agora casa por id; os demais casam por nome e caminho abreviado, e só quando um único recente combina. Um
projeto fora dos seis recentes continua abrindo só pela tela inicial (a tela diz isso). Resolver de vez pede o host
guardar o `projectId` junto do recente, o que é mudança em Rust e fica como decisão separada.

**Segunda entrega da fase B (2026-11-12).** Também só interface, na mesma branch.

| Feito | Observação |
| --- | --- |
| Componentes de página compartilhados | `Page`, `PageSection`, `LoadingState`, `ErrorState` (com "Tentar de novo" quando a leitura pode ser refeita), `EmptyState`, `NoProjectState` e `ResourceView`, em `shared/components/ui/molecules`, sobre o ui-kit. 13 testes, inclusive de acessibilidade |
| Telas migradas | Portfólio, Acompanhamento, Assistente, Integrações, Projeto e Configurações, com as nove seções de Gestão, Integrações e Projeto. Acabaram as classes `.page`, `.title`, `.lead`, `.section` e `.sectionHead` copiadas em quatro módulos de estilo, e os cerca de vinte parágrafos de "lendo" e de erro escritos à mão nessas telas |
| Jornada Configurar preservada ao sair | Guardada por projeto, só na memória, e retomada ao voltar (`JourneyStoreProvider`, `restorableJourney`). Não se retoma uma execução em andamento (o acompanhamento morre com a tela) nem uma leitura ou plano "carregando" (voltam a "ainda não pedido"). O plano velho é seguro: o motor replaneja ao aplicar e recusa com `STALE_PLAN` se o projeto mudou. Concluir ou cancelar limpa. Um pedido novo vindo da tela Projeto começa do zero |
| Português do Brasil | "Guardar chave", "Guardar credencial", "Guardar a chave em Configurações" e "A carregar" viraram "Salvar" e "Carregando" |
| Testes | 326 na interface, mais tipos, lint, formato e build limpos. Prévia visual conferida (Acompanhamento, Integrações, jornada saindo para Gestão e voltando com o plano de 13 alterações e sem novo `harness.plan`) |

Decisão visível ao dono: a página de Configurações passou da largura de 36rem para os 46rem das demais, por uniformidade.

**Terceira entrega da fase B (2026-11-12).** Também só interface, na mesma branch.

| Feito | Observação |
| --- | --- |
| Tela Projeto repartida em três | `/projeto` é a **Visão geral** (perfil tecnológico, o que já existe, execuções e atalhos), `/projeto/harness` traz o Harness aplicado e as revisões, `/projeto/conhecimento` traz o conhecimento. A tela deixa de ser um relatório de seis seções empilhadas. Decisão 5 da seção 6 |
| Menu de Engenharia | Visão geral, Harness, Conhecimento, Configurar, Pedido de engenharia e Catálogo (em breve), cada um com ícone próprio. O Pedido de engenharia, que não tinha lugar no menu, passou a ter. "Visão geral" só fica marcada em `/projeto`, não nas telas abaixo dele |
| Remover conexão pede confirmação | No mesmo padrão de "Desconectar": diz que a conexão sai da lista, que a credencial é apagada deste computador e que usá-la de novo exige conectar com credencial nova (é o que o motor faz em `ConnectionService.Remove`). Desconectar e remover passaram a ter teste, que antes não existia |
| Testes | 335 na interface, mais tipos, lint, formato e build limpos. Prévia visual conferida nas três telas e no menu |

Gestão não ganhou uma "Visão geral" separada: o Acompanhamento já é a visão geral do projeto nesse ambiente, e uma tela nova só repetiria o conteúdo. O alternador de ambiente continua levando a ele.

**Quarta entrega da fase B (2026-11-12).** Também só interface, na mesma branch.

| Feito | Observação |
| --- | --- |
| Execução do workflow e Pedido de engenharia na `Page` | A Execução trocou o `<h1>` e as nove seções avulsas por `Page` e `PageSection`, e o Pedido de engenharia passou a usar `Page` (com a trilha que já tinha). O Pedido passou de 36rem para os 46rem das demais |
| `Disclosure` | Um componente para o "ver detalhes": o `<details>` do navegador com o mesmo visual (triângulo, foco, alvo mínimo, cor de ação). Substitui sete `<details>` de texto (assistente, resposta do assistente, previsão, conhecimento, perfil e revisões) e passou a ser a base do `EvidenceDisclosure` |
| Confirmação de sucesso das ações de conexão | `SuccessState` e uma mensagem opcional em `useAction`, escrita a partir do resultado: conectar, atualizar credencial, desconectar, remover, vincular e desvincular. **Testar** só confirma quando o motor devolve a conexão como "conectada": `connection.test` não falha quando o provedor recusa, só marca a conexão com erro, e um "deu certo" ali seria falso. A mensagem some quando a próxima ação começa |
| Testes | 343 na interface, mais tipos, lint, formato e build limpos. Prévia visual da mensagem de sucesso, do `Disclosure` aberto e fechado e do Pedido de engenharia |

Limites desta entrega: a mensagem de sucesso aparece no fim da seção, como os erros, e não dentro da linha da conexão que foi testada. Os quatro `<details>` de linha de lista da jornada Configurar (decisões, validação, arquivos, execução) têm sumário com layout próprio e ficaram como estão. O teste de teclado do `Disclosure` só prova o foco: o jsdom não ativa um `<summary>` com Enter, os navegadores ativam.

**Quinta entrega da fase B (2026-11-12).** Também só interface, na mesma branch. Fecha itens de navegação e foco da lista de problemas.

| Feito | Observação |
| --- | --- |
| Rolagem ao trocar de tela e de etapa | Era um defeito: o shell tem a altura da janela, quem rola é o `<main>`, e o hook rolava o documento, que nunca rola. Medido no navegador: com o `<main>` rolado em 400 px e o documento em 0, trocar de tela agora leva o `<main>` a 0. A área que rola vem de um contexto (`ContentAreaProvider`), e o shell passa o seu `<main>` |
| Foco ao trocar de tela e de etapa | O foco vai para o `<main>` depois de trocar de tela ou de etapa, e não na primeira renderização. Antes ficava num botão ou item de menu que já não representava o que estava na tela. O teste compara com a chave anterior, e não com "é a primeira vez", para valer também quando o React remonta o efeito |
| Stepper da jornada navegável | As etapas concluídas viram botões ("Voltar para a etapa X"); a atual e as futuras nunca são controles. Só antes de aplicar (`canReopenEarlierSteps`): durante e depois da aplicação o plano já foi usado, e reabri-lo levaria a aprovar de novo um plano que o motor recusaria com `STALE_PLAN` |
| Esc no painel de contexto | Abrir o painel pelo botão leva o foco a ele, e o Esc o fecha devolvendo o foco ao botão. O painel que já nasce aberto em janela larga não toma o foco. Sem levar o foco ao abrir, o Esc não teria a quem se dirigir, porque o painel só tem texto: foi uma falha da minha primeira versão, achada ao revisar e testada com a tecla real |
| Testes | 365 na interface, mais tipos, lint, formato e build limpos. Conferido no navegador: rolagem e foco ao trocar de tela, voltar pelo stepper e abrir e fechar o painel com Esc |

Limite: o teste do hook simula `scrollTo` (o jsdom não o implementa em elementos); a rolagem real foi medida no navegador, não no teste.

**Sexta entrega da fase B (2026-11-12).** Também só interface, na mesma branch.

| Feito | Observação |
| --- | --- |
| `ConfirmAction` | A confirmação de uma ação que apaga algo, num componente só: grupo com o nome do que se confirma, a frase do que acontece, botão de perigo e "Manter". Substitui as três cópias (desconectar, remover e desvincular). O foco vai para a opção segura ao aparecer e o Esc desiste |
| Foco que não cai no vazio | Ao clicar em "Remover" ou "Desconectar", o botão clicado saía da tela e o foco ia para o nada, o que desorienta quem usa teclado. Agora o foco vai para "Manter", e Enter ali nunca apaga nada |
| Mensagem de sucesso na linha | `useAction` passa a dizer a que linha a mensagem pertence (`target`). Testar, atualizar credencial e desconectar mostram o sucesso na linha da conexão que agiu. Remover (a linha some) e conectar (não é de uma linha) mostram na seção |
| Testes | 375 na interface, mais tipos, lint, formato e build limpos. Cobrem o foco e o Esc da confirmação, e a mensagem na linha certa e não na de outra conexão |

Limite: esta entrega foi conferida por testes, não por prévia visual.

Ainda falta nesta fase:

| Pendente | Observação |
| --- | --- |
| Painel de detalhe e confirmação sobre o `Drawer` do ui-kit | O `Drawer` já traz título, Esc, clique fora e botão fechar. Entra junto com a central de execuções (fase C), que é a primeira tela a precisar dele, para não ficar componente sem uso |
| Início e etapas da jornada Configurar | Têm desenho e moldura próprios (lançador e jornada); migrar para `Page` não ganha consistência e arrisca o que é a parte mais acabada do produto |
| Nome de exibição do projeto editável | Pede método novo no motor |

### Fase C: central de execuções ligada à entrega

Entrega 2 da ordem aprovada.

| Entrega | Camada | Observação |
| --- | --- | --- |
| Método de listagem de execuções com filtro e paginação | Motor | Projeção sobre os diários; sem armazenamento novo |
| Sincronização no mesmo modelo de execução, com eventos | Motor | Acaba com a releitura a cada 1,5 s |
| Respostas do assistente como execução | Motor | Já estão na auditoria |
| Tela Execuções: lista, filtros, detalhe em painel | Interface | Em Plataforma (tudo) e em Engenharia (do projeto) |
| Resumo de entrega no topo da execução | Motor, interface | Pedido, feito, validado, ficou de fora |
| Execução ligada a um item de trabalho | Motor, interface | A pessoa informa a issue ou o pull request; a ligação aparece em Gestão |
| Indicador de execução em andamento e de aprovação pendente no menu | Interface | |
| `run.snapshot` conferindo o projeto | Motor | Lacuna registrada |

- **Regra:** abrir o detalhe nunca dispara nada. Reverter e validar de novo continuam ações explícitas.
- **Risco:** o volume de diários em projetos antigos. A listagem pagina no motor e lê só o cabeçalho de cada diário.
- **Pronto quando:** qualquer execução passada pode ser reaberta pela interface, e uma falha mostra o próximo passo.

**Estado da fase C (2026-11-12).** Primeira entrega feita na branch `feat/fase-c-execucoes`, sem commit até o dono pedir.
Motor, host e contrato; a tela é a próxima entrega.

| Feito | Observação |
| --- | --- |
| `run.list` | Lista as execuções de um projeto, ou de todos os autorizados, da mais recente para a mais antiga. Filtra por tipo (`apply`, `validate`, `revert`, `workflow`) e por estado, pagina por cursor opaco (horário e id, que desempata execuções no mesmo instante) e limita a página a 100. Devolve também um resumo (total, andando, esperando aprovação) que ignora os filtros, para o indicador do menu |
| `run.detail` | Operações, passos, verificações, ações propostas (sem o conteúdo que seria gravado), uso de modelo, contagem de eventos e os últimos 200 com número e tipo. O conteúdo de cada evento continua sendo do `run.snapshot` |
| Só leitura | Nada novo em disco e nada disparado: um teste confere que abrir o detalhe e listar não mudam nenhum arquivo |
| Só projetos autorizados | Um projeto esquecido some da lista, mesmo com diário em disco, e o detalhe dele é recusado com `PROJECT_NOT_AUTHORIZED`. Um `runId` com formato estranho (`../x`, `a/b`) volta como `RUN_NOT_FOUND`, não como falha interna |
| `live` | Distingue "andando neste processo agora" de "running" no diário depois de uma queda, para a tela não mostrar como ativa uma execução que ninguém executa |
| Contrato | Fixtures `contracts/fixtures/runs/run-list.json` e `run-detail.json` geradas pelo motor, esquema Zod `run-catalog.ts` conferido contra elas, métodos liberados no host (Rust) e no cliente, com o teste que compara as duas listas |
| Testes | 14 novos no motor (577 no total, 6 ignorados de propósito) e 377 na interface, mais tipos, lint, formato e build limpos |

Limite do desenho: a listagem lê o diário inteiro de cada execução e só então monta o resumo, **não só o cabeçalho** como
o risco desta fase previa. Com poucos diários isso é barato; em projetos com milhares de execuções será preciso um
índice ou ler só o início do arquivo. Fica registrado como medição pendente, não resolvido.

**Segunda entrega da fase C (2026-11-12).** A tela Execuções, só interface, na mesma branch. Primeiro uso do `Drawer`.

| Feito | Observação |
| --- | --- |
| Tela Execuções em `/execucoes` | Item "Execuções" em Plataforma, sempre visível nos dois ambientes. Lista com projeto, tipo, estado (com `StatusChip`, forma além da cor) e data; filtros por projeto, tipo e estado; "Carregar mais" com o cursor do motor; resumo "N em andamento, N aguardando aprovação". Trocar um filtro descarta as páginas carregadas, e uma página que chega depois da troca não é gravada |
| Escopo do projeto | Sem projeto aberto, só "todos". Com projeto aberto, um seletor alterna entre todos e só aquele, em vez de uma tela separada para Engenharia: uma rota só, sem repetir a lista |
| Detalhe no `Drawer` | Painel à direita com estado, motivo, passos, o que mudou, verificações, ações propostas por agentes, o que ficou de fora, pendências, modelo e eventos. Só lê: um teste confere que só `run.list` e `run.detail` são chamados |
| Uma falha mostra o próximo passo | Por estado: falhou sem nada aplicado, falhou com parte aplicada (aponta o Configurar para reverter), requer intervenção, concluída com limitações, aguardando aprovação e interrompida |
| Interrompida | Um diário "em execução" sem execução viva (o aplicativo foi fechado) aparece como **Interrompida**, em aviso, e não como ativa |
| Acompanhamento ao vivo | O link "Abrir o acompanhamento" só aparece para quem anda agora ou espera aprovação; as demais já terminaram e ficam no detalhe, porque `run.snapshot` só existe para execuções que o processo conhece |
| Contrato tolerante | `kind` e `state` são texto no Zod, não enum: um tipo ou estado novo do motor aparece como veio e não derruba a lista |
| `Drawer` conferido | Esc fecha, o botão "Fechar o detalhe" fecha, o foco entra no painel e volta para a linha que o abriu, e o tema escuro está correto. Conferido nos testes e no navegador, em 1280 e 760 px, com clique real |
| Testes | 393 na interface (16 novos da tela), mais tipos, lint, formato e build limpos. Prévia visual com host simulado e dados de fixture; nada foi conferido no Tauri empacotado |

Limites: o tema claro não foi conferido no `Drawer` (a prévia ficou escura nas duas tentativas), os rótulos de operação
e de estado de passo cobrem o que o motor emite hoje e um valor novo aparece como veio, e o indicador de execução
andando no menu continua pendente.

**Terceira entrega da fase C (2026-11-12).** Fecha a lacuna do `run.snapshot` e o indicador no menu, na mesma branch.

| Feito | Observação |
| --- | --- |
| `run.snapshot` e `run.cancel` conferem o projeto | Antes bastava o `runId`: qualquer um acessava a execução de qualquer projeto. Agora a execução de um projeto não autorizado é "não encontrada" no retrato e não é cancelada (`cancelled: false`), e o pedido pode trazer `projectId`, que precisa bater com o da execução. O parâmetro é opcional: a tela de acompanhamento só conhece o `runId` e segue funcionando. Sem a central ligada (sessões de teste) o comportamento antigo se mantém |
| Indicador no menu | O item Execuções ganha um aviso ao lado do nome: o número de execuções **aguardando aprovação** em tom de atenção, ou, sem nenhuma, o de **em andamento**. O nome acessível do link diz os dois ("Execuções, 1 aguardando aprovação, 2 em andamento") |
| Como lê | `run.list` com `limit: 1`, só pelo resumo. Relê ao ligar, a cada 20 s, ao trocar de tela, e não pergunta com a aba escondida. Uma leitura que falha deixa o último valor; sem motor pronto não há indicador, em vez de um número velho |
| Testes | 18 no catálogo (4 novos: retrato e cancelamento com e sem projeto, projeto errado, projeto não autorizado), 11 novos na interface (hook, menu e descrição do aviso). Suites: 581 no motor, 404 na interface, tipos, lint, formato e build limpos |

Limite: cada leitura do indicador faz o motor ler os diários de todos os projetos, o mesmo custo da lista. Com a
cadência de 20 s é aceitável hoje; com milhares de execuções pede o índice já registrado como medição pendente. O
aviso não foi conferido visualmente no navegador nesta entrega, só por testes.

**Quarta entrega da fase C (2026-11-12).** O resumo de entrega no topo do detalhe da execução, na mesma branch.

| Feito | Observação |
| --- | --- |
| `summary` em `run.detail` | Contagens do diário, sem interpretação: **feito** (arquivos criados, atualizados, removidos e revertidos; passos concluídos; ações de agentes executadas), **validado** (verificações que passaram, falharam, ficaram bloqueadas ou não rodaram, e quantas obrigatórias não passaram) e **ficou de fora** (alterações não aplicadas, arquivos em conflito, ações não executadas, pendências) |
| Seção "Resumo da entrega" | Primeira seção do painel, em quatro linhas: Pedido, Feito, Validado, Ficou de fora. Sem nenhuma verificação registrada, diz isso em vez de mostrar zero, que pareceria falha; sem nada de fora, diz "Nada ficou de fora." |
| O "Pedido" não traz o texto da pessoa | Tentei gravar o pedido no diário do workflow e **desfiz**: dois testes de segurança existentes provam que o diário nunca guarda o texto do pedido (ele fica só no checkpoint do runtime), e um terceiro que um projeto não enxerga o conteúdo do outro. O "Pedido" diz o que o tipo de execução faz ("Aplicar a configuração do Harness no projeto"). Mostrar o texto exigiria decidir antes onde ele pode ser guardado |
| Concordância | Os testes do domínio pegaram "passouram" e "1 de 3 passos concluídos"; as frases agora concordam com o número |
| Testes | 20 no catálogo, 9 do domínio do resumo e o painel conferido com o resumo da fixture. Suítes: 583 no motor, 412 na interface, tipos, lint, formato e build limpos |

Limite: o resumo não foi conferido visualmente no navegador (a grade de quatro linhas é nova), só por testes.

**Quinta entrega da fase C (2026-11-12).** A execução ligada a um item de trabalho, na mesma branch. Falta mostrar a
ligação em Gestão (próxima entrega).

| Feito | Observação |
| --- | --- |
| `run.link` e `run.unlink` | A pessoa informa o número (`41` ou `#41`) ou a chave (`APP-12`, sem diferenciar maiúsculas) e o motor procura entre o que as integrações **do próprio projeto** já sincronizaram. Item não lido não se liga (`LINK_ITEM_NOT_FOUND`); o mesmo identificador em duas integrações pede a integração (`LINK_ITEM_AMBIGUOUS`); formato fora do padrão é recusado antes de procurar; até 10 itens por execução. Ligar de novo o mesmo item não muda nada nem repete a auditoria. Desligar o que não está ligado não faz nada |
| Só a referência | A ligação guarda integração, tipo, identificador e instante, em `links.json` ao lado do diário e **fora dele**: ligar não reescreve o diário (um teste confere os bytes) e o título do item nunca é copiado para o disco. Título e situação saem dos dados sincronizados na hora de mostrar |
| Item que saiu do recorte | A ligação continua e aparece como "fora do recorte atual da integração", sem título nem situação, em vez de inventar o que ele era |
| Isolamento e auditoria | Projeto não autorizado, execução inexistente, id com formato estranho e execução de outro projeto são recusados; um item de outro projeto não se liga. Ligar e desligar entram na trilha de auditoria (`run.linked`, `run.unlinked`) |
| Interface | Bloco "Itens de trabalho" no painel de detalhe: a lista com provedor, título e situação (chip), "Desligar" por item (sem confirmação: é reversível), e o campo "Ligar a um item (número ou chave)". O erro do motor aparece com a mensagem dele e o que a pessoa digitou fica no campo |
| Contrato | Fixture `run-link.json`, `links` no detalhe, esquema Zod conferido, métodos liberados no host e no cliente. O `Program.cs` monta o catálogo com o serviço de ligações, porque só o motor com integrações o tem |
| Testes | 21 novos no motor (17 de ligação e 4 do catálogo já contados na suíte de 602) e 6 na interface. Suítes: 602 no motor, 418 na interface, tipos, lint, formato e build limpos |

Limites: o link do item não é clicável, porque abrir endereço externo dentro da janela do aplicativo muda a tela; ligar
exige que o item já esteja sincronizado, então um item novo pede uma sincronização antes. O bloco não foi conferido no
navegador, só por testes.

**Sexta entrega da fase C (2026-11-12).** A ligação aparece em Gestão, na mesma branch.

| Feito | Observação |
| --- | --- |
| `run.linked` | O caminho inverso de `run.link`: cada item do projeto que tem execuções ligadas, com título e situação lidos dos dados sincronizados e as execuções (tipo, estado, início) da mais recente para a mais antiga; os itens, do ligado há menos tempo ao mais antigo. Só abre o diário das execuções que têm ligação (a varredura procura `links.json`, não lê todos os diários). Projeto não autorizado é recusado; um projeto não enxerga as ligações do outro |
| Seção em Acompanhamento | "Execuções ligadas a itens", depois dos Riscos: o item (`#41`, título, provedor, situação em chip) e a lista das execuções ligadas a ele. Só aparece se o projeto tem fonte vinculada; sem ligação, diz como ligar e aponta para Execuções. Um item que saiu do recorte aparece como "fora do recorte atual da integração" |
| Leitura independente | A seção lê por conta própria: se `run.linked` falhar, o resto do Acompanhamento continua e só ela mostra o erro |
| Contrato | Fixture `run-linked.json`, esquema Zod conferido, método liberado no host e no cliente |
| Testes | 5 novos no motor e 5 na interface. Suítes: 607 no motor, 423 na interface, tipos, lint, formato e build limpos. Numa rodada com a máquina carregada, 2 testes da interface estouraram os 5 s e passaram na repetição |

Limites: as execuções da lista não abrem direto pelo item (o detalhe vive no painel de Execuções e ainda não tem
endereço próprio); a seção não foi conferida no navegador, só por testes.

**Sétima entrega da fase C (2026-11-12).** A sincronização avisa por evento, na mesma branch. Acaba a releitura a cada
1,5 s, mas a sincronização **não virou uma execução na lista** (veja o limite).

| Feito | Observação |
| --- | --- |
| Eventos `sync.started`, `sync.progress`, `sync.finished` | O `runId` do evento é o id da sincronização, com `seq` crescente a partir de 1. Um aviso ao começar, um a cada página gravada e um ao terminar, este **depois** de a conexão e o histórico estarem atualizados (um teste olha o estado no instante do aviso) e de a integração estar livre para a próxima |
| Só estado e contagens | Projeto, integração, estado, páginas e contagens de itens (trazidos, novos, alterados, iguais) e o código do erro. Nenhum item, título, endereço ou credencial sai por evento; um teste confere |
| Aviso que falha não derruba | Se a saída do motor fecha no meio, a sincronização termina, grava e libera a integração do mesmo jeito |
| Interface | A lista de vínculos se relê quando chega um aviso **deste projeto**; avisos de outro projeto, de outros assuntos ou fora do formato são ignorados; para de ouvir ao sair da tela. A leitura periódica caiu de 1,5 s para 15 s e só roda enquanto há sincronização em andamento: é a rede de segurança para um aviso perdido, não o mecanismo |
| Contrato | `syncEventDataSchema` na interface, conferido pelo teste da tela com o aviso no formato do motor. O host repassa qualquer evento do motor, então nada mudou em Rust |
| Testes | 6 novos no motor e 5 na interface. Suítes: 613 no motor, 428 na interface, tipos, lint, formato e build limpos |

Limite: a sincronização **não aparece em `run.list`** nem tem diário. O desenho dela é outro: uma execução do motor tem
no máximo uma por projeto de cada vez (para não escrever duas vezes na mesma pasta), e uma sincronização não escreve no
projeto. Colocá-la na lista pede um tipo de execução sem essa exclusividade e a decisão de onde guardar o histórico, que
hoje já está em `integration.history` (50 sincronizações por integração). Fica como decisão do dono. Também não foi
conferido no aplicativo empacotado que o evento chega do motor real à tela: só a emissão (testes do motor) e a escuta
(testes da interface), cada uma com seu dublê.

**Oitava entrega da fase C (2026-11-12).** As perguntas ao assistente viram execuções, na mesma branch.

| Feito | Observação |
| --- | --- |
| Tipo de execução `assistant` | Cada pergunta respondida grava um diário (`ask-…`) no mesmo armazenamento das demais, e por isso aparece sozinha em `run.list`, no filtro de tipo ("Pergunta ao assistente") e no detalhe. A auditoria `management.assistant_answered` continua como estava |
| Só contagens, nunca texto | O diário traz os passos "Resposta escrita pelo modelo" e "Conferência do motor" (afirmações aceitas e descartadas, fatos citados) e o uso de tokens. **Nem a pergunta nem a resposta entram**: um teste confere que o texto de ambas, e a chave, não estão no arquivo, pela mesma regra de que o diário não guarda texto de pedido |
| Estados | Resposta sem nada descartado: concluída. Afirmações descartadas pela conferência: **concluída com limitações**, com a pendência dita. Modelo sem configurar, fora do contrato ou fora do prazo: **falhou**, com o código do motivo, e o painel diz "A pergunta não foi respondida" e o que fazer, sem falar de alterações |
| Não bloqueia nada | Não passa pelo rastreador (uma pergunta não escreve no projeto), então não impede nem é impedida por uma execução andando no mesmo projeto; um teste confere |
| O que não vira execução | Pergunta curta ou longa demais (é erro de entrada) e projeto não autorizado, que nem cria pasta |
| Falha ao gravar | Uma falha de disco ao gravar o registro nunca atrapalha a resposta |
| Testes | 7 novos no motor e 2 na interface. Suítes: 620 no motor, 430 na interface, tipos, lint, formato e build limpos |

Limites: o registro nasce **depois** da resposta, então uma pergunta que ainda espera o modelo não aparece como
"em andamento"; e o texto da pergunta não fica no histórico, de propósito, o que torna a lista menos informativa do que
a do Kaivor.

**Estado da fase C (2026-11-12): oito entregas.** Da tabela original: listagem com filtro e paginação, tela com detalhe
em painel, resumo de entrega, ligação com item de trabalho (e sua leitura em Gestão), indicador no menu, `run.snapshot`
conferindo o projeto, sincronização com eventos e respostas do assistente como execução estão feitos. **Pendente de decisão
sua:** colocar a sincronização na lista de execuções (hoje ela só avisa por evento e guarda o histórico em
`integration.history`). Medição pendente: a listagem lê o diário inteiro de cada execução. Não conferido no aplicativo
empacotado nem no navegador: o indicador do menu, o resumo de entrega, o bloco de itens, a seção em Gestão e os eventos
de sincronização; o tema claro do `Drawer`.

### Fase D: gestão que explica

Entregas 3 e 4 da ordem aprovada, mais marcos e relatórios.

| Entrega | Camada | Observação |
| --- | --- | --- |
| Cycle time | Motor | Jira pelo histórico de status; GitHub pela API de eventos da issue. Sem base, "sem dado" |
| Bloqueios | Motor | Vínculos de bloqueio do Jira; marcador acordado no GitHub |
| Marcos | Motor | Milestone do GitHub, versão do Jira; previsão por marco |
| Histórico de cada risco | Motor | Guardar a leitura, em vez de só recalcular |
| Registro de decisão gerencial com evidências | Motor, interface | Com os fatos do momento e as execuções relacionadas |
| Relatório do período | Motor, interface | Só fatos do motor; texto de IA passa pelo verificador |
| Indicador, gráfico de barras e faixa de previsão | Interface | Componentes simples, com valores escritos |
| Histórico de perguntas do assistente | Motor, interface | |

- **Depende de:** fase A (conectores validados) e fase B (componentes).
- **Risco:** campos de bloqueio e sprint variam por site do Jira. Começar pelo que é padrão e dizer o que não foi lido.
- **Pronto quando:** cada indicador novo tem fórmula, teste conferido à mão e fixture; a tela mostra de onde veio cada
  número.

**Estado da fase D (2026-11-12).** Primeira entrega feita na branch `feat/fase-d-gestao-explica` (criada da `master`),
sem commit até o dono pedir: o **cycle time**.

| Feito | Observação |
| --- | --- |
| Fórmula | Dias corridos do **início** à conclusão, dos itens entregues no período (a mesma população do lead time). O início é a **primeira mudança de status para um status da categoria "em andamento"**; reabertura posterior não muda o início. Mediana e percentil 85 pelo mesmo método do lead time (posição ⌈p·n⌉, valor observado), mínimo de 5 itens |
| De onde vem a categoria do status | O histórico do Jira traz só nomes ("Em revisão"), que mudam por site. O nome é classificado pelos **itens que estão nele agora**: só vale se todos concordam na categoria. Efeitos ditos na descrição do código: um status que nenhum item ocupa hoje não é reconhecido, e um nome com categorias conflitantes também não. Em ambos os casos o item **sai do cálculo e é contado** |
| "Sem dado" com motivo | Fonte sem nenhum histórico de status (o GitHub): "A fonte não informa quando o trabalho começou". Amostra pequena: diz quantos há e quantos ficaram de fora ("há 3 (2 sem início conhecido)"). Nunca um zero |
| Onde aparece | `cycleTime` em cada tipo de dado de `management.metrics` (amostra, excluídos, mediana, P85); fato `cycletime` no pacote do assistente, com número e motivo; figura "Cycle time mediano" na seção de fluxo, com "85% em até N dias" e "N itens sem início conhecido"; o item saiu da lista "Ainda não calculado" (Bloqueios, Mudança de escopo e Previsibilidade continuam nela) |
| Conferido à mão | Os testes trazem as contas na descrição: ciclos de 1, 2, 3, 4, 5, 6 e 10 dias dão mediana 4 e P85 6; com reabertura e várias entradas vale a primeira; direto de "A fazer" para "Concluído" sai e é contado; nome sem diferenciar maiúsculas; nome conflitante; só o que foi entregue no período. A fixture `metrics.json` do motor real mostra mediana 3 e P85 6 num cenário que calculei antes de gerar |
| Testes | 10 novos de cálculo, 4 na tela de fluxo e ajustes nos de Gestão e do assistente. Suítes: 630 no motor, 434 na interface, tipos, lint, formato e build limpos |

Limites: **o GitHub continua "sem dado"**. A API de eventos da issue pediria uma chamada por item e foge do teto de
chamadas da sincronização; não foi feita. Nada disto foi validado com um Jira real (a fase A segue sem a validação real),
então o formato do histórico foi lido do que o conector já grava e dos testes com dublê. O percentil 85 pela posição, e
não interpolado, arredonda para um valor observado, como no lead time.

**Segunda entrega da fase D (2026-11-12).** Os **bloqueios**, na mesma branch.

| Feito | Observação |
| --- | --- |
| Definição | Um item **aberto** está bloqueado se tem o rótulo `blocked`, `bloqueado` ou `bloqueada` (sem diferenciar maiúsculas) **ou** um vínculo "is blocked by" para um item que ainda não foi concluído. Item concluído nunca conta, mesmo com o rótulo |
| Jira | O conector passou a pedir `issuelinks`. Lê o vínculo padrão `Blocks`: no JSON, o vínculo com `inwardIssue` é a relação "is blocked by" deste item com o outro; com `outwardIssue` o item é o que bloqueia e não está bloqueado. Um tipo de vínculo renomeado ainda conta se a descrição de entrada for "is blocked by"; qualquer outro tipo (Relates, Duplicates) não é bloqueio. O bloqueador já concluído não bloqueia |
| "Não lido" não é "nenhum" | `BlockedBy` no item é lista vazia quando o vínculo foi lido e não há, e **nulo quando não foi lido** (GitHub, ou dado sincronizado antes). O indicador traz `linksRead` e uma `basis` por extenso: "Vínculos lidos em 10 de 12 itens" ou "Os vínculos de bloqueio não foram lidos nesta fonte". O número só vale para o que a fonte permitiu ler |
| GitHub | Só o rótulo: o GitHub não tem vínculo de bloqueio. O "marcador acordado" ficou fixo nos três rótulos acima; torná-lo configurável por projeto é uma decisão pendente |
| Onde aparece | `blocked` por tipo de dado em `management.metrics` (total, por vínculo, por rótulo, lidos e base); fato `blocked` no pacote do assistente, com a base; figura "Bloqueados" na seção de fluxo, com "N por vínculo, N por rótulo" e o que foi lido; **novo risco `blocked`** ("Itens bloqueados"), por regra: 1 item é baixo, 2 a 4 médio, 5 ou mais alto, com os itens do mais antigo ao mais novo (até 10). Saiu de "Ainda não calculado" |
| Conferido à mão | O teste conta A (rótulo), B (vínculo), C (os dois), D (concluído com rótulo, não conta), E (vínculo lido sem bloqueio) e F (nada lido): 3 bloqueados, 2 por vínculo, 2 por rótulo, 3 com vínculo lido |
| Efeito colateral | `BlockedBy` entra no hash de cada item: na primeira sincronização depois desta versão, **todos os itens do Jira aparecem como "alterados"** uma vez (o conteúdo gravado mudou). Não perde dado nem duplica |
| Testes | 26 novos no motor (cálculo, risco, leitura do Jira com sete formatos de vínculo, GitHub só com rótulo) e 3 na tela de fluxo; fixtures regeneradas, com um item bloqueado por vínculo no cenário de riscos (a previsão mudou junto: 11 itens abertos, provável em 22/12/2026). Suítes: 656 no motor, 437 na interface, tipos, lint, formato e build limpos |

Limites: nada foi validado com um Jira real, e os campos de bloqueio variam por site. Só o vínculo padrão é lido, e um
site que usa o campo "Impediment"/"Flagged" ou um tipo de vínculo próprio aparece com "0" e com a base dizendo o que foi
lido, o que é diferente de dizer que não há bloqueio. Não calculei há quanto tempo cada item está bloqueado: o provedor
não informa quando o vínculo ou o rótulo foi posto, e usar a data de atualização inventaria um número.

**Terceira entrega da fase D (2026-11-12).** Os **marcos**, com previsão por marco, na mesma branch.

| Feito | Observação |
| --- | --- |
| O que é um marco | O **milestone** do GitHub (issue e pull request) e a **versão de correção** do Jira (`fixVersions`). O item guarda o nome e o prazo do marco no provedor (a data do milestone em UTC, a data de lançamento da versão). Lista vazia é "sem marco"; nulo é "sincronizado antes de os marcos serem lidos" e conta como sem marco |
| Andamento | Por marco e por tipo de dado: itens, entregues, **concluídos sem entrega** (descartados, duplicados, pull request fechado sem merge, sem data de conclusão) e abertos. O percentual usa como base os itens menos os concluídos sem entrega. Item com duas versões no Jira conta nas duas; o nome agrupa sem diferenciar maiúsculas; o prazo é o mais cedo entre os itens que o informam. Os itens sem marco aparecem contados, por tipo |
| Previsão | O mesmo método da previsão do projeto (simulação sobre o ritmo semanal), com o **trabalho restante só do marco** e o **ritmo do tipo de dado inteiro**: o marco sozinho teria poucas entregas para servir de histórico. As premissas e os limites vão na resposta e na tela |
| Situação | `done` (tudo concluído, mesmo com prazo passado), `overdue` (prazo passou e há item aberto, haja previsão ou não), `no_forecast` (com o motivo da previsão), `no_due_date`, `on_track` (o cenário **conservador**, 85% das simulações, cabe no prazo), `at_risk` (o provável cabe, o conservador não) e `behind` (nem o provável cabe). Prazo igual à data prevista conta como cabendo |
| Onde aparece | Método `management.milestones`; seção "Marcos" no Acompanhamento, depois da previsão, com o prazo, o chip de situação, "N de M itens entregues, N abertos", os três cenários e a explicação do método atrás de "Como a previsão do marco é calculada"; só aparece com fonte vinculada. Um fato por marco no pacote do assistente, com a situação e o motivo. A lista para em 20 marcos e diz quantos ficaram de fora, os com trabalho aberto primeiro, pelo prazo |
| Conferido à mão | Ritmo de 2 entregas por semana em 12 semanas e 6 itens abertos no marco: toda simulação termina em 3 semanas, **03/12/2026** (12/11 mais 21 dias). Com prazo em 12/12 e em 03/12 o marco está no prazo, com prazo em 02/12 está atrasado em relação ao prazo; as três situações do meio (em risco) foram conferidas com previsões construídas, porque o sorteio da simulação não se calcula à mão |
| Efeito colateral | Os marcos entram no conteúdo gravado de cada item: na primeira sincronização depois desta versão os itens aparecem como "alterados" uma vez (junto com o efeito dos bloqueios). Sem perda nem duplicação |
| Testes | 22 novos no motor (andamento, situação, serviço e protocolo, isolamento entre projetos, o fato do assistente, leitura do GitHub e do Jira) e 18 na interface; fixture `milestones.json` do motor real. Suítes: 676 no motor, 453 na interface, tipos, lint, formato e build limpos. Numa rodada com a máquina carregada três testes do runtime Python falharam por tempo e passaram isolados |

Limites: nada foi validado com um Jira ou GitHub reais. Não há previsão por **ordem** de trabalho: o método assume que o
time só trabalha nos itens do marco, o que costuma ser otimista quando há outros itens em andamento. O prazo é o do
provedor: sem ele não há comparação, e um milestone do GitHub sem data aparece como "sem prazo". O sprint do Jira não é
lido, só a versão de correção. A mudança de escopo por marco segue em "Ainda não calculado": pede guardar o escopo ao
longo do tempo, que o produto ainda não faz.

**Quarta entrega da fase D (2026-11-12).** O **histórico de cada risco**, na mesma branch.

| Feito | Observação |
| --- | --- |
| O que é guardado | Um registro do estado das regras de risco: para cada risco, identificador, título, gravidade e os **identificadores** dos itens citados. Nunca o título do item nem conteúdo do provedor (um teste confere). Um arquivo por projeto, `projects/{projeto}/risk-history.json`, com o identificador conferido contra o formato do motor. Arquivo ilegível vale como "sem histórico" e é recomeçado. Fica com os 200 registros mais recentes |
| Quando se registra | Quando uma sincronização **termina com sucesso e todas as fontes do projeto estão completas**. Uma leitura parcial ou falha faria um risco parecer resolvido só porque os dados não chegaram, então nesses casos não se registra. Sem dados lidos, também não. Ler `management.risks` continua sem gravar nada |
| Só quando muda | Estado igual ao último registro só confirma (`CheckedAt` avança); estado diferente vira registro novo. Os itens citados em outra ordem não contam como mudança |
| Trajetória | Para cada risco que já apareceu: apareceu, subiu, baixou, mudou os itens (entraram e saíram) e foi resolvido, do mais antigo ao mais recente. Um risco que já estava no **primeiro** registro é "observado", não "apareceu": o motor não sabe desde quando ele existia. Ativos primeiro, do mais grave ao mais leve; resolvidos depois, do mais recente. Até 30 trajetórias, com a conta do que ficou de fora |
| Onde aparece | `management.riskhistory`; seção "Histórico dos riscos" no Acompanhamento, logo depois dos Riscos (só com fonte vinculada): quantos registros, desde quando, conferido pela última vez, o chip ativo ou resolvido, "visto pela primeira vez" e a trajetória atrás de um `Disclosure`. Sem registro nenhum, diz quando o primeiro será feito. A **base** do motor vai na resposta e na tela: o que aconteceu antes do primeiro registro, ou entre sincronizações, não é conhecido |
| Ouvinte do fim da sincronização | `SyncService.Finished`: roda na linha da sincronização, depois de tudo gravado e antes do aviso `sync.finished`. Um ouvinte que falha **não** desfaz a sincronização: o tipo do erro (nunca a mensagem) vai para a trilha de auditoria como `integration.sync_listener_failed` |
| Conferido à mão | A trajetória de cinco registros (A média, A sobe e ganha o item 3, perde o 2, some e volta mais leve; B aparece e some) dá para A: observado, subiu, mudou os itens, resolvido, apareceu, e para B: apareceu, resolvido, com as datas dos registros certos |
| Testes | 24 novos no motor (trajetórias, registro, serviço, sincronização, arquivo, protocolo) e 15 na interface; fixture `risk-history.json` do motor real, com um cenário de três registros em que dois riscos aparecem, mudam e se resolvem. Suítes: 701 no motor, 467 na interface, tipos, lint, formato e build limpos. Em rodadas com a máquina carregada alguns testes que sobem o runtime Python ou usam o prazo de 5 s falharam por tempo e passaram isolados |

Limites: **o histórico começa no primeiro registro depois desta versão**; o que já estava no projeto aparece como
"observado". A ligação do `Program.cs` (assinar `Finished` para registrar) não tem teste próprio: os testes assinam o
mesmo evento do mesmo jeito, mas a linha de composição do motor real não foi exercida. Os registros só são feitos quando
uma sincronização termina, não por tempo: um projeto que não sincroniza não ganha registro. Nada foi validado com um
Jira ou GitHub reais.

**Quinta entrega da fase D (2026-11-12).** O **registro de decisão gerencial**, na mesma branch.

| Feito | Observação |
| --- | --- |
| O que é uma decisão | Título (até 120 caracteres), o que foi decidido (até 1000), o porquê (opcional, até 2000), as execuções relacionadas (até 10) e, guardados pelo motor, os **fatos do dia** e o que ficou de fora do pacote. Identificador `dec-…`, um arquivo por decisão em `projects/{projeto}/decisions`, até 500 por projeto |
| Os fatos vêm do motor | O pedido não tem campo de fatos (um teste manda um e vê que ele é ignorado): o motor monta o pacote de evidências **naquele instante**, o mesmo que iria ao assistente, e o guarda com a decisão. Reabrir a decisão meses depois mostra o que valia no dia, mesmo que os números de hoje sejam outros (um teste confere) |
| Execuções relacionadas | Cada uma precisa existir no projeto; uma inexistente, de outro projeto ou com id estranho recusa o registro **inteiro** e não grava nada. Guarda identificador, tipo, estado e início como estavam no dia |
| Segredos | O texto da pessoa passa pela mesma redação de segredos do resto do motor: uma chave colada por engano (testadas uma da AWS e uma do GitHub) não fica no arquivo, e a resposta traz `redacted` para a interface avisar. A trilha de auditoria leva números e identificadores, nunca o título nem o texto |
| Sem editar nem apagar | A decisão é a trilha de por que se decidiu: o protocolo só oferece registrar, listar e abrir (`decision.record`, `decision.list`, `decision.get`), e um teste confere que `decision.update` e `decision.delete` não existem. Quem precisar remover uma decisão tem de apagar o arquivo da pasta de dados; fica como pendência decidir se o produto deve oferecer isso |
| Isolamento | Projeto não autorizado é recusado; um projeto não vê nem abre a decisão do outro; id forjado (`../x`) é "não encontrada"; arquivo ilegível fica fora da lista sem derrubar as outras |
| Onde aparece | Seção "Decisões" no Acompanhamento, depois dos marcos e do histórico de riscos e antes das fontes. O botão "Registrar uma decisão" abre o formulário (título, decisão, porquê e as dez execuções mais recentes para marcar); o formulário diz que ela não se edita nem se apaga e que os fatos de hoje ficam guardados. A lista mostra título, data, decisão, porquê e as execuções; "Ver os fatos do dia (N)" busca a decisão sob demanda e mostra os fatos e o que ficou de fora. Um erro do motor aparece com o motivo, sem perder o que foi digitado |
| Testes | 28 novos no motor (registro, fatos, execuções, validação, segredos, auditoria, limite, lista, isolamento, arquivo, protocolo) e 14 na interface (com 3 fixtures do motor real: registro, lista e abertura). Suítes: 730 no motor, 481 na interface, tipos, lint, formato e build limpos |

Limites: nenhum fato da fase inclui o título dos itens a menos que todas as fontes autorizem (é a regra do pacote), mas
**o que está no pacote fica no disco desta máquina com a decisão**, como já ficaria no do assistente; vale saber antes de
registrar. O formulário não relaciona itens de trabalho nem riscos (só execuções), e o seletor mostra apenas as dez
execuções mais recentes do projeto. A ligação do `Program.cs` não tem teste próprio da composição, e a tela não foi
conferida no navegador, só por testes. Não há identidade de usuário, então a decisão não diz quem a registrou.

**Sexta entrega da fase D (2026-11-12).** O **relatório do período**, na mesma branch, **sem o texto de IA**.

| Feito | Observação |
| --- | --- |
| O que traz | `management.report` com a largura do período em semanas (2 a 26, padrão 4, terminando agora): os **indicadores** do período (`management.metrics`), a **previsão**, os **riscos** de agora, as **mudanças de risco** que aconteceram dentro do período (da mais recente, até 50), os **marcos**, as **decisões** registradas no período (título e decisão, sem os fatos do dia) e as **execuções** que começaram no período, contadas por tipo e estado |
| Só compõe | Cada parte é a mesma resposta dos métodos que já existem, recortada pelo período: nada é calculado de novo e nada é gravado (um teste compara os arquivos da pasta de dados antes e depois). O período pedido vale para os indicadores; a previsão, os riscos e os marcos são sempre "de agora" |
| Honestidade | A resposta traz a `basis`: "nenhum texto foi escrito por modelo de linguagem", o histórico de riscos só existe desde o primeiro registro, as execuções são as que começaram no período. Um projeto não vê o que é do outro e o não autorizado é recusado |
| Tela | Rota `/gestao/relatorio` e item "Relatório" no menu de Gestão. Escolhe-se o período (2, 4, 8 ou 12 semanas) e a tela **reaproveita as seções do Acompanhamento** (fluxo, previsão, riscos, marcos), mais as mudanças de risco, as decisões e as execuções do período |
| Levar para fora | "Copiar como texto" copia o relatório em Markdown, escrito só com o que o motor devolveu: um indicador sem base sai como "sem valor (motivo)", nunca zero; uma fonte incompleta é avisada no texto; seção vazia diz que está vazia. Se a cópia falhar, a tela diz como seguir |
| Conferido | O teste de período usa datas calculadas à mão (decisão há 10 dias entra em 4 semanas, a de 40 dias só em 8; execução há 60 dias fica fora até de 8 semanas). O cenário da fixture traz cycle time mediano 3 e percentil 85 em 6, um marco em risco (o provável cabe no prazo, o conservador não) e um risco de bloqueio |
| Testes | 13 novos no motor e 15 na interface (página e Markdown); fixture `report.json` do motor real. Os testes do Markdown pegaram um "1 abertos" que estava errado e foi corrigido. Suítes: 743 no motor, 496 na interface, tipos, lint, formato e build limpos |

Limites: **falta o resumo escrito por IA** que a tabela do roadmap previa ("texto de IA passa pelo verificador"): esta
entrega é só o relatório de fatos. O assistente já existe com verificador, e um resumo do período pelo mesmo caminho é
a próxima peça possível; não foi feito para não misturar texto de modelo com o que o motor calcula. O relatório não foi
conferido no navegador, só por testes, e não há impressão nem arquivo: só "Copiar como texto".

Falta nesta fase: componentes de indicador e faixa de previsão (visuais), histórico de perguntas do assistente, e o
resumo do período por IA, se for desejado.

### Fase E: catálogo do Harness

Entrega 5 da ordem aprovada.

| Entrega | Camada | Observação |
| --- | --- | --- |
| Ficha por componente (o que faz, quando age, o que lê, o que pode alterar, de onde veio) | Motor | Dado do motor, com fixture; os textos moram junto do ativo |
| Tela Harness com Estado, Catálogo e Revisões | Interface | Reparte a tela Projeto atual |
| Inventário de MCP no catálogo, com alerta de segredo literal | Motor, interface | O leitor já existe |
| Política dos agentes em linguagem simples | Motor, interface | A partir do hook |
| Tela Conhecimento separada | Interface | Com o que já existe em `engineering.knowledge` |

- **Decisão embutida:** sem editor livre. Mudança continua pela jornada Configurar.
- **Cuidado:** alterar o texto dos ativos em `Assets/` muda o hash do Harness aplicado. Os textos da ficha devem ficar
  fora do arquivo que é gravado no projeto.
- **Pronto quando:** uma pessoa que nunca abriu `.claude/` consegue dizer o que cada componente faz no projeto dela.

**Estado da fase E (2026-11-12).** Primeira entrega feita na branch `feat/fase-e-catalogo` (criada da `master`), sem commit
até o dono pedir: a **ficha por componente** e a tela **Catálogo**. Da tabela da fase, a tela Harness com Estado e
Revisões e a tela Conhecimento separada já tinham sido entregues na fase B; faltam o inventário de MCP e a política dos
agentes em linguagem simples.

| Feito | Observação |
| --- | --- |
| Ficha por componente | Para cada um dos 11 componentes do catálogo: **o que faz**, **quando age**, **o que lê**, **o que pode alterar**, **de onde veio** e **onde fica no projeto**, em português simples. Os textos moram em `Assets/catalog/components.json`, embutido no motor mas **fora dos arquivos que vão para o projeto**: mudar uma ficha não muda o hash do Harness aplicado de ninguém (um teste confere que nenhum texto de ficha aparece em nenhum arquivo de um projeto com o Harness aplicado) |
| Ferramentas do agente | Lidas do cabeçalho do próprio arquivo do agente (`tools: Read, Grep, ...`), cada uma com o que quer dizer ("ler arquivos", "rodar comandos no terminal"). Não são digitadas na ficha, então não envelhecem quando o agente muda |
| A ficha presa à realidade | Os testes impedem a ficha de mentir: todo componente do catálogo tem ficha e vice-versa; nenhum campo vazio nem travessão; o que a ficha diz que o agente altera confere com as ferramentas que ele declara (sem `Write` a ficha tem de dizer "Nada"; com `Write`, "Escreve um único arquivo"); "adaptado de" só aparece quando o próprio ativo diz que é adaptado; e os arquivos que cada ficha cita são os que o Harness aplicado registrou no lock |
| `engineering.catalog` | Sem projeto, o catálogo geral só com as fichas; com projeto, também o que está **aplicado**, **dispensado** em `mariliz.yaml` ou **não aplicado** nele, e o estado do pior arquivo (íntegro, modificado à mão, ausente...). Um componente dispensado cujos arquivos ainda estão no projeto continua "dispensado", com o aviso de que saem na próxima aplicação. Uma regra escrita para o projeto (proposta pelo Harness Engineer) aparece com uma ficha genérica |
| Tela | `/projeto/catalogo`, e o item "Catálogo" do menu de Engenharia deixou de ser "Em breve". Os componentes vêm agrupados (agentes, skills, regras, hooks, ciclo de especificação, declaração do projeto), cada um com a frase do que faz e os detalhes atrás de um `Disclosure`. Diz que não há editor: mudar é pela jornada Configurar |
| Só leitura | Ler o catálogo não escreve nada no projeto (um teste compara o conteúdo antes e depois). Projeto não autorizado é recusado |
| Testes | 15 novos no motor e 12 na interface; fixture `catalog.json` do motor real. O estado dos arquivos agora tem um só lugar de rótulos (`harness-state.ts`), usado pela tela de estado e pelo catálogo. Suítes: 758 no motor, 509 na interface, tipos, lint, formato e build limpos |

Limites: **o critério "uma pessoa que nunca abriu `.claude/` consegue dizer o que cada componente faz" não foi testado com
uma pessoa**; os textos foram escritos e conferidos contra os ativos, mas ninguém de fora os leu. A tela não foi conferida
no navegador, só por testes. A ficha do hook descreve o comportamento de forma geral ("bloqueia ou pede confirmação"); a
lista do que ele bloqueia, em linguagem simples, é a entrega "política dos agentes". Nada na ficha é traduzido por modelo.

Falta nesta fase: inventário de MCP com alerta de segredo literal e a política dos agentes em linguagem simples.
### Fase F: hub de integrações, nível 2

| Entrega | Camada | Observação |
| --- | --- | --- |
| Provedor de IA como tipo de conexão | Motor, interface | Credencial no cofre (fase A), teste, estado, troca |
| Segundo provedor de IA | Motor | O endpoint continua em lista permitida; a chave continua no motor |
| Modelo por finalidade | Motor, interface | Assistente, juiz, workflow |
| Resumo por conexão: projetos, dados, contexto | Interface | As quatro perguntas da seção 6.2 de `SPOCK_PRODUCT_EVOLUTION.md` |
| Política de contextualização consumida pelo índice | Motor, Python | Dado sincronizado autorizado entra como fonte externa; a tela mostra o que passa a ser enviado |
| Vocabulário de impacto no contrato de conector | Motor | "Leitura" e "Altera dados", mesmo sem escrita |
| Cofre em Linux e macOS | Motor | Decisão 2 da seção 13 |

- **Depende de:** fase A.
- **Risco:** dado externo no contexto dos agentes é o ponto mais sensível do roadmap. A política nasce fechada; a
  mudança é por conexão e por projeto; o proxy continua redigindo segredos; a auditoria registra.
- **Pronto quando:** um teste prova que dado de uma fonte não autorizada nunca chega ao índice nem ao provedor.

### Fase G: inteligência compartilhada

Entrega 6 da ordem aprovada e fase 7 do documento da plataforma.

| Entrega | Camada | Observação |
| --- | --- | --- |
| Linha do tempo do projeto | Motor, interface | Revisões, mudanças de perfil, execuções, decisões, riscos |
| Trilha de auditoria única por projeto | Motor | Referencia diário e arquivo de auditoria |
| Workflow de investigação em LangGraph | Python, motor | Só aqui há etapas e decisão no meio: cruzar riscos, itens parados e execuções |
| Laço agentic do RAG dentro desse workflow | Python | Reformular e buscar de novo, com teto |
| Registros de decisão indexados como memória do projeto | Motor, Python | |
| Evals com perguntas reais de gestão | Python | |
| Assistente sobre o portfólio | Motor | Respeitando a política de cada projeto |

- **Depende de:** C (execuções ligadas), D (decisões e bloqueios), F (política consumida), e validação com modelo real.
- **Risco:** é a fase com mais IA. Vale a mesma regra do assistente: fatos do motor, resposta verificada, o que foi
  descartado aparece.
- **Pronto quando:** uma pergunta como "por que a vazão caiu?" devolve uma resposta em que cada afirmação cita um fato,
  e o caminho da investigação pode ser reaberto na central.

## 4. Como a ordem aprovada aparece aqui

| Ordem aprovada em 2026-11-11 | Fase |
| --- | --- |
| 1. Revisões do Harness | Feita (branch `feat/plataforma-fase-6-versoes`) |
| 2. Execuções ligadas à entrega | C |
| 3. Cycle time e bloqueios | D |
| 4. Decisões gerenciais com evidência técnica | D |
| 5. Agentes técnicos com prioridades e objetivos | E |
| 6. Histórico consolidado e investigação com LangGraph e RAG | G |

**Decidido em 2026-11-12:** as fases A e B entram antes da ordem aprovada. A sequência passa a ser A e B (em
paralelo, quando possível), depois C, D, E, F e G. O motivo: sem B, a central de execuções nasceria sem componentes
compartilhados; sem A, os indicadores de tempo nasceriam sobre conectores não validados.

A decisão é sobre a ordem. O início de cada fase continua pedindo aprovação própria, e a fase A só começa com as
credenciais de teste (decisões 1 e 2 da seção 6).

## 5. Depois deste ciclo

Itens que não entram nas fases A a G. Ficam registrados para não se perderem e para não virarem tarefa por inércia.

| Item | Prior. | Quando reconsiderar |
| --- | --- | --- |
| Paleta de comandos | P2 | Quando houver mais de cinco projetos em uso ou mais de quinze telas |
| Painéis redimensionáveis | P2 | Depois do painel de detalhe estar em uso |
| Sincronização agendada e rotinas de plataforma | P2 | Depois da fase C (sincronização como execução) |
| MCP gerenciado pelo plano do Harness | P2 | Depois da fase E |
| Servidor MCP do Spock para o Claude Code | P2 | Depois da fase F; pede investigação própria de segurança |
| Assistente de engenharia | P2 | Depois da validação com modelo real e da fase G |
| Uso de modelo agregado | P2 | Junto da fase F, se couber |
| Modo de diagnóstico | P2 | Quando houver o primeiro usuário fora do time |
| Estado de operação interrompida | P2 | Junto da fase C, se couber |
| Segundo ambiente de IA (Codex ou Cursor) | P2 | Por demanda real |
| Leitura do ciclo de especificação feito no Claude Code | P2 | Depois da fase E |
| Azure DevOps ou GitLab | P2 | Depois de GitHub e Jira comprovados, por necessidade |
| Mapa de etapas somente leitura | P3 | Se um workflow com ramificação visível existir |
| Escrita em provedores | P3 | Só com decisão de produto; exige operação nomeada, aprovação e estado "incerto" |
| Bancos e APIs como conexão | P3 | Por demanda real |
| Identidade, papéis e organização | P3 | Depende da decisão 4 da seção 13 |
| Tema claro | P3 | Sem demanda |

Não entram em nenhum horizonte: construtor de automações, editor visual de fluxo, Database Studio, HTTP Client,
conversa livre como centro, memória aprendida, aplicativo web.

## 6. Decisões que precisam do dono do produto

| # | Decisão | Bloqueia |
| --- | --- | --- |
| 1 | Credencial de teste para GitHub e Jira | Fase A, e por ela D e F |
| 2 | Chave de modelo real para validar o assistente | Fase A, e por ela G |
| 3 | Migrar a chave de modelo para o cofre | Fase A e F |
| 4 | Aprovar o shell com seletor de projeto e alternador de ambiente | Fase B |
| 5 | A tela Projeto pode ser repartida em Visão geral, Harness e Conhecimento | Fases B e E |
| 6 | As fases A e B entram antes da ordem aprovada, ou a ordem segue como está. **Decidido em 2026-11-12: A e B antes.** | Resolvida |
| 7 | Segundo provedor de IA: qual | Fase F |
| 8 | Dado sincronizado pode alimentar o conhecimento dos agentes, com a política por conexão | Fases F e G |
| 9 | Componentes novos ficam no aplicativo, ou o ui-kit volta a receber mudanças | Fase B |
| 10 | Cofre em Linux e macOS agora ou depois | Fase F |

## 7. Riscos do roadmap

| Risco | Fase | Redução |
| --- | --- | --- |
| Conector do Jira falhar com dado real | A | Descobrir cedo; corrigir antes de D |
| Reorganização do shell quebrar testes | B | Testes atualizados na mesma mudança; rotas mantidas |
| Migração de telas ficar pela metade | B | Uma tela por vez; padrão antigo removido ao fim de cada uma |
| Central lenta com muitos diários | C | Paginação no motor; leitura só do cabeçalho |
| Campos do Jira variarem por site | D | Começar pelo padrão; dizer o que não foi lido |
| Texto de ficha mudar o hash do Harness | E | Texto fora do arquivo gravado no projeto |
| Dado externo vazar para o provedor de IA | F | Política fechada por padrão; teste de isolamento; auditoria |
| LangGraph onde bastava uma chamada | G | Só com etapas e decisão no meio; revisão antes de começar |
| Fases crescerem | Todas | Cada fase fecha com suíte verde e documento atualizado antes da próxima |

## 8. O que este roadmap não faz

- Não transforma todas as oportunidades em tarefas: 17 itens ficam para depois e 7 ficam de fora.
- Não estima prazo em dias. O tamanho é relativo (pequeno, médio, grande) porque depende de decisões da seção 6.
- Não altera nada: nenhum código, contrato, entidade ou tela foi modificado nesta análise.
