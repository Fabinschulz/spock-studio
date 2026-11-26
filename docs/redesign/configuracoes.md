# Spock Studio — Área Configurações: diagnóstico e proposta

> **Estado:** diagnóstico feito no aplicativo real e proposta aprovada pelo dono em 2026-11-25, com as cinco decisões seguindo a recomendação. Os incrementos CF1 a CF5 entram um por PR; entregues: CF1 (#215), CF2 (#217), CF4 (#218) e CF5. Cobre os itens 10 e 11 da etapa 4 do pedido original, na parte de Configurações; a Gestão tem o seu próprio documento.
>
> Caminhos de arquivo relativos a `desktop/ui/src/features/settings/`, salvo indicação.

## 1. Método

- Aplicativo real: `tauri dev` com o motor .NET compilado, janela dirigida pelo Playwright ligado ao protocolo de depuração, axe-core injetado (regras WCAG 2.0, 2.1 e 2.2, níveis A e AA, com contraste). O mesmo roteiro do [`configurar.md`](configurar.md).
- Pasta de dados do motor nova e vazia (`MARILIZ_DATA_DIR`): nenhuma chave guardada, o estado de quem abre o aplicativo pela primeira vez.
- Percurso: Configurações › Minha IA; a mesma tela por Conexões › Modelos de IA; um texto curto no campo da chave (que nunca sai do aplicativo, porque o botão fica desabilitado); teto de tokens inválido, salvo e de volta ao padrão; Diagnóstico; Sobre. Depois, o motor encerrado à força uma vez (o aplicativo o reinicia sozinho) e duas vezes seguidas (o aplicativo desiste), com Diagnóstico e Minha IA capturados nesse estado.
- Capturas em 1440×900 e 900×650, cada uma também com a altura do conteúdo inteiro. As imagens não entram no repositório; as seções 3 a 5 as descrevem.

| Tela e estado | Altura do conteúdo (1440 / 900) | Telas de rolagem (1440×900) | axe |
| --- | --- | --- | --- |
| Minha IA, primeiro uso | 2009 / 2093 px | 2,2 | 0 violações |
| Minha IA, teto inválido e teto salvo | 2009–2032 / 2093–2116 px | 2,3 | 0 |
| Modelos de IA (Conexões) | igual a Minha IA | 2,2 | 0 |
| Minha IA, motor parado | cabe na tela | 1 | 0 |
| Diagnóstico, motor pronto e motor parado | cabe na tela | 1 | 0 |
| Sobre | a página cabe; o bloco de avisos rola por dentro (cerca de 272 mil px) | 1 | 0 |

O axe não acusou nada. Os problemas de acessibilidade abaixo são de foco e de compreensão (WCAG 2.4.3, 3.3.1, 3.3.3 e 4.1.3), não de marcação.

**O que não foi verificado no aplicativo:** nada que dependa de uma chave aceita ou recusada por um provedor, porque isso exige uma chave real e mandar a chave para fora do computador. Ficaram de fora, portanto: provedor conectado, com erro e "ainda não conferida"; modelo por finalidade; trocar e remover chave; tornar padrão; chave guardada em arquivo em vez do cofre. Esses estados foram lidos no código e nos testes (`model-settings-page.test.tsx`) e estão marcados como **lido no código** nas tabelas. Também não foram vistos: a inteligência local indisponível (a captura foi interrompida porque outra sessão abriu o aplicativo na mesma porta), leitor de tela de verdade, macOS e Linux.

## 2. Problemas que atravessam a área

| # | Problema | Onde |
| --- | --- | --- |
| G1 | A mesma tela tem dois nomes e duas descrições no menu: "Minha IA", "A chave e o modelo de IA usados neste computador" (Configurações) e "Modelos de IA", "O provedor e o modelo de IA que respondem aos pedidos" (Conexões). Os primeiros passos do Início levam a Conexões; o pedido de engenharia, o resumo do contexto e Da ideia ao projeto mandam a pessoa a "Minha IA". Quem segue um texto chega a uma tela com outro nome. | `app/layouts/app-shell/navigation.tsx:246-251` e `:265-268`; `model-settings-page.tsx:19`; `features/home/setup-checklist/steps.ts:41`; `features/engineering-request/components/engineering-request-form.tsx:39`, `context-review-panel.tsx:45`; `features/idea/idea-page.tsx:71` |
| G2 | O retorno de cada ação aparece longe dela. A página tem um aviso e um erro só, no fim da seção Provedores. Ao salvar o teto de tokens, "Teto de fluxos de engenharia: 10.000 tokens por execução." apareceu acima do título da seção de tetos, debaixo do cartão do terceiro provedor. O erro de uma chave recusada aparece depois dos três cartões, não no do provedor (lido no código). | `model-settings-page.tsx:61-62`, `hooks/use-model-settings.ts:14-15` |
| G3 | O foco se perde depois de salvar. Ao clicar em "Salvar teto", o botão fica desabilitado enquanto o motor responde e a linha é montada de novo; o foco caiu no `<body>` (medido). Quem usa teclado volta ao começo da página. O mesmo deve acontecer com "Salvar chave", que limpa o campo e desabilita o botão (não medido). | `components/budgets-section.tsx:25` e `:63`, `components/provider-row.tsx:30` e `:72` |
| G4 | Com o motor parado, as telas dizem o que está errado mas não o que fazer. Minha IA mostra "Lendo a configuração…" e "O motor não está em execução." ao mesmo tempo, sem botão para tentar de novo nos provedores; a seção de ferramentas oferece "Tentar de novo", que não funciona enquanto o motor está parado. O Diagnóstico mostra "Motor local: Indisponível" e "Inteligência local: Verificando", que fica assim para sempre. Só a barra de status diz "Reinicie o aplicativo". | `model-settings-page.tsx:44-61`, `domain/diagnostics-report.ts:33-39`, `components/ai-tools-section.tsx:25-29` |
| G5 | Termos sem explicação, embora o glossário tenha quase todos: Harness, MCP, hook, skill e os caminhos das pastas de configuração de cada ferramenta na lista de ferramentas; "Motor local" e "Inteligência local" no Diagnóstico; "tokens" no título da seção de tetos. O `Term` só aparece no texto de abertura e na descrição dos provedores. | `domain/ai-tool-labels.ts:24-26` e `:33-34`, `domain/diagnostics-report.ts:36-37`, `components/budgets-section.tsx:19`; glossário em `shared/components/ui/atoms/term/glossary.ts` (`harness`, `mcp`, `hook`, `skill`, `engine`, `localIntelligence`, `token`) |

## 3. Minha IA (e Conexões › Modelos de IA)

Em 1440×900, no primeiro uso, a tela tem de cima para baixo: o título, uma frase sobre provedores e modelos, a seção **Provedores** com um parágrafo de quatro linhas sobre onde a chave fica, e três cartões iguais, um por provedor, cada um com o nome, o selo "Sem chave", a frase "Sem chave neste computador.", o campo da chave, a regra do formato em cor de destaque e o botão **Salvar chave** desabilitado. Cabem o primeiro cartão inteiro e o segundo quase todo. Abaixo da dobra: o terceiro cartão, **Teto de tokens por finalidade** (dois campos com faixa, descrição e botão) e **Ferramentas de IA neste computador** (quatro ferramentas, cada uma com estado e uma frase técnica). Em 900×650, acima da dobra só cabem o parágrafo e o campo do primeiro provedor; a página tem 3,2 telas.

| # | Problema | Onde |
| --- | --- | --- |
| IA1 | O primeiro uso não diz o que fazer. Três cartões iguais pedem três chaves; nada diz que basta uma, como escolher o provedor nem onde se consegue uma chave. A primeira coisa que se lê é como a chave é guardada (quatro linhas), antes de saber para que ela serve. É o contrário da meta do pedido: configurar um provedor sem conhecer os conceitos. | `model-settings-page.tsx:26-43`, `components/provider-row.tsx` |
| IA2 | A tela não diz o que muda com e sem IA. O Início diz ("Sem ele, as leituras e verificações locais continuam funcionando"); aqui a abertura só diz "Os provedores e os modelos que o Spock Studio usa". | `model-settings-page.tsx:26-31`; o texto bom está em `features/home/setup-checklist/steps.ts:39` |
| IA3 | "Sem chave" aparece duas vezes em cada cartão: o selo e a frase logo abaixo. | `components/provider-row.tsx:37` e `:53` |
| IA4 | **Salvar chave** desabilitado não diz por quê. Com um texto curto no campo, o campo não fica inválido (`aria-invalid="false"`, medido) e a ajuda continua a mesma; a regra do formato aparece em cor de destaque, como um link ou um sucesso. | `components/provider-row.tsx:27`, `:65` e `:72` |
| IA5 | Os erros do provedor não dizem o próximo passo: "O provedor recusou a chave." (copiou a chave inteira? ela foi revogada?), "O modelo não pôde ser contactado." (internet, proxy?). A interface recebe o código do erro e poderia dizer. **Lido no código.** | mensagens em `runtime/Mariliz.Infrastructure/HttpsModelTransport.cs:62` e `:80`; exibidas em `components/provider-row.tsx:50` e `model-settings-page.tsx:61` |
| IA6 | A ordem não segue a necessidade. Os tetos de tokens aparecem antes de existir um provedor, quando ainda não valem nada, e empurram o resto para baixo. O modelo por finalidade só aparece depois da primeira chave, e nada avisa que ele vai aparecer. | `model-settings-page.tsx:65-79` |
| IA7 | O teto é técnico e pesado: o título fala em tokens sem explicação, a descrição fala do motor cobrando "antes e depois de cada chamada", cada campo tem três blocos de texto (faixa, descrição, botões), o valor aparece como "8000" enquanto a ajuda diz "8.000", e o rótulo repete "(tokens por execução)". É uma escolha de gasto que quase ninguém precisa fazer no primeiro uso. | `components/budgets-section.tsx:19-20`, `:44-59` |
| IA8 | "Ferramentas de IA neste computador" responde outra pergunta (quais ferramentas de linha de comando existem) e repete a escolha de ferramenta da tela Agentes de IA. "Não está instalada neste computador" afirma mais do que o motor sabe: ele só procura o comando nas pastas do sistema. Cada linha traz caminhos de arquivo e siglas. | `components/ai-tools-section.tsx`, `domain/ai-tool-labels.ts:13` e `:21-35`; a busca em `runtime/Mariliz.Application/AiTools/AiToolService.cs:18`; a outra lista em `features/agents/agents-page.tsx:125-151` |
| IA9 | O rótulo do modelo por finalidade junta parênteses: "Nome do modelo (Provedor) (padrão)". **Lido no código.** | `domain/provider-labels.ts:35` |

## 4. Diagnóstico

Em 1440×900 cabe tudo: o título, a frase "O que está rodando neste computador, para quem dá suporte", a seção **Este computador** com quatro linhas (Aplicativo, Motor local, Inteligência local, Navegador interno) e o botão **Copiar informações**, e o **Registro recente** com as últimas linhas do motor e do aplicativo.

| # | Problema | Onde |
| --- | --- | --- |
| D1 | É um inventário, não um diagnóstico: mostra estados sem dizer se estão certos nem o que fazer. "Inteligência local: Parada" é o normal (ela liga no primeiro pedido, como diz a barra de status), mas lido sozinho parece defeito. A configuração que a pessoa acabou de fazer não aparece: se há modelo de IA conectado, se a chave foi recusada, se ela está no cofre do sistema ou em arquivo, quais ferramentas de IA existem. A meta do pedido, "o diagnóstico diz o que está errado e como corrigir", não é atendida. | `domain/diagnostics-report.ts:33-39`, `diagnostics-page.tsx:43-50` |
| D2 | Com o motor parado, a tela diz "Indisponível" e não oferece saída; a inteligência local fica em "Verificando" (G4). O aplicativo desiste depois de uma reinicialização rápida, e não há como pedir outra sem fechar o aplicativo: o host não tem um comando para isso. | `domain/diagnostics-report.ts:36-37`; `desktop/host/src-tauri/src/engine/lifecycle.rs:12` (`MAX_QUICK_RESTARTS`) |
| D3 | O registro mostra texto interno: a origem como "host" e "motor", e "o motor encerrou (código Some(-1))", que é a forma de depuração de um valor do Rust. A hora não tem data. | `components/diagnostic-log-section.tsx:22-23`; `desktop/host/src-tauri/src/engine/mod.rs:234` |
| D4 | "Navegador interno" é a linha mais longa (a identificação inteira do navegador, em duas linhas) e só serve para o suporte, mas tem o mesmo peso que o estado do motor. | `domain/diagnostics-report.ts:38` |
| D5 | O retorno de **Copiar informações** fica num texto pequeno abaixo da lista, longe do botão, que está no alto à direita. | `diagnostics-page.tsx:36-41` e `:51-53` |

## 5. Sobre

Em 1440×900: o título, a descrição "SPOCK: System of Programming, Organization, Code and Knowledge.", a seção **Aplicativo** com o nome, a versão e o lema, e **Avisos de terceiros** com a contagem ("486 componentes de terceiros") e um bloco rolável com cerca de 760 mil caracteres. O bloco recebe foco pelo teclado e tem nome acessível. É a tela mais simples e quase não pede mudança.

| # | Problema | Onde |
| --- | --- | --- |
| S1 | A descrição da tela, primeira frase que se lê, é a sigla expandida em inglês. | `about-page.tsx:9` e `:20` |
| S2 | O aviso diz que "os textos longos estão na pasta licenses/", sem dizer onde fica essa pasta. | texto gerado por `docs/compliance/tools/generate_sbom.py`, exibido em `about-page.tsx:47-49`; a tela passa a dizer onde fica, sem mexer no texto gerado (que obrigaria a gerar de novo o inventário) |

## 6. Componentes locais e a biblioteca (`@mariliz/ui-kit` 0.10.1)

| Hoje | Na biblioteca | Proposta |
| --- | --- | --- |
| `TextField`, `SelectField`, `Button`, `StatusChip` | os mesmos | Ficam. |
| Três cartões de provedor iguais no primeiro uso | `Radio` | A escolha do provedor vira um grupo de `Radio` (seção 7). |
| `SuccessState` e `ErrorState` (locais, em `shared/components/ui/molecules/page-state`) uma vez por página | `Alert` (via `InlineNotice`) | Ficam os locais, mas um por linha, junto da ação. Erro com o próximo passo e, quando houver, o botão que resolve. |
| `LoadingState` "Lendo a configuração…" | `LoadingBar` | Fica o local (é uma frase, não uma barra), mas nunca junto do erro. |
| `ConfirmAction` (local) em Remover chave | `Modal` | Fica: é o padrão escolhido no Configurar. |
| Lista `<dl>` do Diagnóstico | sem equivalente | Vira uma lista de verificações com `StatusChip` e uma ação por linha. |
| Bloco `<pre>` dos avisos de terceiros | sem equivalente | Fica. |
| Ajuda do `TextField` na cor primária, que parece link ou sucesso | é o estilo do próprio `TextField` | Fica: mudar é decisão da biblioteca, para todas as telas (anotado na seção 8). |
| — | `Disclosure` (local, já usado no Configurar) | Recolhe o que não é decisão: onde a chave fica, tetos de tokens, dados para o suporte. |

Nenhum componente novo na biblioteca: a proposta não exige nova versão do `@mariliz/ui-kit`.

## 7. Proposta

### Princípios

1. A primeira coisa que se lê diz o estado e a próxima ação ("Nenhum modelo conectado. Conecte um provedor para…").
2. Uma ação por vez no primeiro uso; o resto aparece quando passa a valer, e o que não é decisão fica recolhido, nunca removido.
3. Todo erro diz a causa provável e o que fazer, no lugar onde a ação aconteceu, e o foco fica onde a pessoa estava.
4. O Diagnóstico responde "está tudo certo?" antes de "o que está rodando?".
5. Termo técnico só com explicação (`Term`) ou dentro de um detalhe recolhido.

### Telas: o que muda, o que fica, o que sai

| Hoje | Proposta | Muda | Fica | Sai |
| --- | --- | --- | --- | --- |
| Minha IA / Modelos de IA | **Modelos de IA**, o mesmo nome nos dois menus (DCF2) | **No topo, o estado:** "Nenhum modelo conectado" ou "Conectado: Provedor · modelo padrão · conferida em …", com uma frase do que muda com e sem IA. **Sem chave:** um bloco "Conectar um provedor" com a escolha do provedor (`Radio`), uma linha de onde a pessoa consegue a chave naquele provedor, um campo e o botão "Conferir e guardar", que diz o que falta quando está desabilitado. **Com chave:** um cartão por provedor conectado, com estado, modelo, data da conferência, erro com o próximo passo e as ações; "Conectar outro provedor" abre a mesma escolha com os que faltam. **Modelo por finalidade** logo abaixo, e, antes da primeira chave, uma linha dizendo que ele aparece ali. **Tetos de tokens** recolhidos em "Limite de gasto por execução", com o valor atual no resumo. Retorno e erro na linha da ação; foco de volta ao botão ou ao campo. | Guardar a chave só depois de o provedor aceitar; conferir; tornar padrão; remover com confirmação; modelo por finalidade; tetos com faixa, padrão e voltar ao padrão; a explicação de onde a chave fica (recolhida). | Os cartões vazios de provedor sem chave (a escolha os substitui); o selo e a frase repetidos; a lista de ferramentas, que vai para o Diagnóstico (DCF1). |
| Diagnóstico | **Diagnóstico** | **Verificações** no topo, uma linha cada, com selo (Certo, Atenção, Problema, Normal), o que significa e a ação: *Motor* (pronto, ou parado com "Reiniciar o motor", DCF3); *Inteligência local* ("Normal: liga no primeiro pedido que precisa dela", ou indisponível com o que deixa de funcionar; "depende do motor" quando ele está parado); *Modelo de IA* (nenhum, opcional, com "Conectar"; chave recusada com "Abrir Modelos de IA"; conectado); *Onde a chave fica* (cofre do sistema, ou arquivo com o link para Conexões › Serviços); *Ferramentas de IA* (quantas foram encontradas, a lista recolhida e "Procurar de novo"). Abaixo, **Para o suporte**: versão, navegador interno recolhido, Copiar informações (com o retorno ao lado do botão e as verificações no texto copiado) e o registro com origem traduzida ("Motor", "Aplicativo"), data e hora. | Tudo o que a tela mostra hoje; nada de chave, caminho de projeto ou conteúdo do trabalho no texto copiado. | — |
| Sobre | **Sobre** | A descrição em português ("Spock Studio: engenharia de software assistida por IA"); a sigla continua, como segunda linha. O aviso diz onde fica a pasta de licenças. | Versão, contagem e textos de licença sem alteração. | — |

Nenhuma capacidade é removida: a lista de ferramentas muda de tela, os tetos ficam recolhidos, e o que sai é texto repetido ou um cartão vazio que a escolha do provedor substitui.

### Incrementos (um PR cada, na ordem)

| # | Incremento | Toca | Risco |
| --- | --- | --- | --- |
| CF1 | **Textos, retorno e foco, sem mexer na estrutura:** retorno e erro na linha da ação (G2); foco preservado depois de salvar (G3); motor parado sem "Lendo…" junto do erro, com a saída escrita (G4); `Term` nas ferramentas, no Diagnóstico e nos tetos (G5); selo e frase sem repetir (IA3); chave mal copiada como erro no campo, com o motivo (IA4); próximo passo por código de erro do provedor (IA5); "não encontrada" em vez de "não instalada" (IA8); rótulo do modelo sem parênteses duplos (IA9); origem e data no registro (D3); texto do host sem `Some(…)` (D3); retorno do copiar ao lado do botão (D5); descrição do Sobre e pasta de licenças (S1, S2). | UI de `settings`; uma linha em `desktop/host/src-tauri/src/engine/mod.rs` | Baixo |
| CF2 | **Um nome para a tela dos modelos** (DCF2): o mesmo nome e a mesma descrição nos dois menus; os textos que mandam a "Minha IA" passam a dizer o caminho do menu. | `navigation.tsx`, `model-settings-page.tsx`, `engineering-request`, `idea` | Baixo |
| CF3 | **Modelos de IA, primeiro uso e ordem:** estado no topo, "Conectar um provedor" com `Radio`, cartões só dos conectados, "Conectar outro provedor", modelo por finalidade anunciado, tetos recolhidos (IA1, IA2, IA6, IA7). Depende de DCF5. | `model-settings-page.tsx`, `components/`, `domain/provider-labels.ts` | Médio (muitos estados de provedor; só os sem chave podem ser vistos sem uma chave real) |
| CF4 | **Diagnóstico com verificações:** a lista de verificações com selo, significado e ação; ferramentas de IA vindas de Minha IA (DCF1); dados para o suporte recolhidos; o texto copiado com as verificações (D1, D4). Só usa métodos que o motor já tem (`model.connection`, `aitool.list`, `engine_status`). | `diagnostics-page.tsx`, `domain/diagnostics-report.ts`, `components/` | Médio |
| CF5 | **Reiniciar o motor pela tela** (DCF3): um comando novo no host e o botão no Diagnóstico e nos estados de motor parado (D2, G4). | `desktop/host/src-tauri/src/engine/`, `diagnostics-page.tsx`, estado de erro de Modelos de IA | Médio (Rust e ciclo de vida do motor) |

CF1 não depende de decisão e pode começar logo. CF2 espera DCF2, CF3 espera DCF5, CF4 espera DCF1 e CF5 espera DCF3; CF2, CF3 e CF4 são independentes entre si, e CF5 vem depois de CF4 porque põe o botão na tela nova. Cada PR é validado no aplicativo real, nas duas larguras, com o axe e sem erro no console. Os estados que exigem uma chave aceita ou recusada continuam sem validação no aplicativo até o dono rodar com uma chave dele; o PR diz isso.

### Decisões do dono

| # | Decisão | Recomendação |
| --- | --- | --- |
| DCF1 | Onde fica a lista "Ferramentas de IA neste computador": em Modelos de IA, como hoje, ou no Diagnóstico, como uma verificação. | **No Diagnóstico.** **Decidido em 2026-11-25: no Diagnóstico.** Ela responde "o que existe neste computador", não "qual modelo responde"; a escolha da ferramenta continua em Agentes de IA, que já mostra quais existem. |
| DCF2 | A tela dos modelos aberta por dois menus: (a) o mesmo nome e a mesma descrição nos dois; (b) só em Conexões, saindo de Configurações; (c) como hoje, dois nomes. | **(a), com o nome "Modelos de IA".** **Decidido em 2026-11-25: (a).** É o nome que o Início e a busca já usam; tirar a entrada de Configurações mudaria o destino da área e o que `workspace-unificado.md` descreve. |
| DCF3 | Um botão "Reiniciar o motor" (comando novo no host) ou manter "Reinicie o aplicativo". | **O botão.** **Decidido em 2026-11-25: o botão.** É a correção que o diagnóstico deve oferecer; hoje o aplicativo desiste depois de uma reinicialização rápida e a única saída é fechar tudo. |
| DCF4 | O Diagnóstico inclui verificações do projeto aberto (estado do Harness, índice) ou só deste computador. | **Só deste computador.** **Decidido em 2026-11-25: só deste computador.** O estado do projeto já está em Padrões do projeto › Estado e revisões; repetir ali criaria dois lugares para a mesma coisa. |
| DCF5 | Como dizer onde se consegue a chave: (a) uma linha de texto por provedor, com o caminho no site dele; (b) um link que abre o navegador (exige um plugin novo no host e uma permissão de abrir endereços externos). | **(a).** **Decidido em 2026-11-25: (a).** Não acrescenta dependência nem permissão; o link pode vir depois, se a linha não bastar. |

## 8. O que mudaria em outros documentos (não editados aqui)

- `docs/redesign/workspace-unificado.md`, seção 5: a linha "Modelos de IA | Minha IA, provedores" e o parágrafo sobre "Minha IA" nos textos que explicam onde guardar a chave mudam se DCF2 ficar em (a) ou (b). A seção 2 ("Configurações | Minha IA · Diagnóstico · Sobre") muda junto.
- `docs/redesign/workspace-unificado.md`, seção 10: um item novo apontando para este documento, como o da jornada Configurar.
- `docs/ROADMAP.md`: os incrementos CF1 a CF5 e as decisões DCF1 a DCF5, até serem entregues.
- `@mariliz/ui-kit`: a ajuda do `TextField` (`FormHelperText`) usa a cor primária quando não há erro; em texto explicativo ela parece link ou sucesso. Vale avaliar a cor neutra na biblioteca.
