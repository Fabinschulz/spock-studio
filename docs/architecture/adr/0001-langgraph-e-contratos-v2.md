# ADR-0001 — LangGraph na inteligência e contratos v2 entre motor e inteligência

**Status:** aceita em 2026-11-02 (Fase 2). **Contexto completo:** [auditoria-orquestracao-agentic.md](../auditoria-orquestracao-agentic.md).

## Contexto

O módulo `intelligence/` já executa o workflow `engineering.request.v1` como uma sequência fixa de funções em Python puro, sem
dependências. Ele responde de uma vez no fim da execução, não chama modelo, e o motor reconstrói os eventos depois.

A arquitetura aprovada pede quatro coisas:

- um grafo explícito com interrupção e retomada;
- eventos transmitidos durante a execução;
- agentes que usem modelo sem que a chave saia do motor;
- uma fronteira em que a inteligência só **propõe** efeitos.

A restrição que pesava contra o LangGraph era o tamanho. O orçamento do interpretador embarcado é de 25 MB
(`PythonRuntimeLayout.PackagedInterpreterBudgetBytes`), e os scripts eram só stdlib.

## Medição (spike D1)

Ambiente:

- CPython `3.12.10-embed-amd64` oficial, o mesmo de `scripts/build-runtime.ps1`, com `._pth` apontando para uma pasta de pacotes;
- `pip install --only-binary=:all: --target` em 2026-11-02: `langgraph 1.2.19`, `langgraph-checkpoint 4.3.1`,
  `langgraph-checkpoint-sqlite 3.1.3`, `langchain-core 1.7.4`, `langsmith 0.14.17`, `pydantic 2.14.2` e `pydantic-core 2.50.2`,
  com dependências transitivas (41 pacotes).

| Medida | Valor |
| --- | --- |
| Interpretador embed extraído | 21,5 MB (≈ 8,8 MB comprimido com LZMA) |
| Dependências, só `.py` + binários | 25,7 MB (≈ 4,7 MB LZMA) |
| Dependências, `.py` + `__pycache__` | 41,8 MB |
| Dependências, **só bytecode** (`compileall -b`, sem `.py`) | **26,6 MB** |
| Maiores pacotes | `pydantic_core` 5,7 MB, `langsmith` 3,6 MB, `langchain_core` 2,3 MB, `pydantic` 1,7 MB, `langgraph` 1,4 MB, `zstandard` 1,3 MB |
| `langgraph-checkpoint-sqlite` sobre o núcleo | +0,5 MB (traz `aiosqlite` e `sqlite-vec 0.1.9`) |
| Importação: primeira vez, gravando `.pyc` | 2,5 s |
| Importação sem `.pyc` e pasta somente leitura (`-B`) | 1,7 s |
| Importação com bytecode pré-compilado | **0,67 a 0,76 s** |
| `StateGraph` de 3 nós + `interrupt()` + `SqliteSaver` + retomada em grafo e conexão novos | funciona; 40 ms |
| Conexões de rede durante importação e execução (`socket.connect`/`getaddrinfo` interceptados, `LANGSMITH_TRACING=false`) | **nenhuma** |

O `langsmith` e o `httpx` são importados pelo `langchain-core`, mas ficam ociosos com o tracing desligado.

## Decisão

1. **Usar LangGraph** (`langgraph` 1.2.x e `langgraph-checkpoint-sqlite` 3.1.x) como motor do grafo em `intelligence/`. A regra
   aprovada na auditoria era usar LangGraph se o pacote ficasse abaixo de ~60 MB. O pacote mede **48 MB** no disco
   (interpretador 21,5 MB + dependências 26,6 MB) e acrescenta cerca de **8 MB** ao instalador.
2. **Empacotar as dependências como bytecode sem fonte** (`compileall -b`, sem `.py`). O resultado ocupa praticamente o mesmo espaço
   que só os `.py`, não precisa gravar em `Program Files` e importa em ~0,7 s.
3. **Orçamentos separados:** interpretador continua em 25 MB; dependências da inteligência ganham 30 MB próprios. O build falha se
   qualquer um passar.
4. **Versões fixadas com hash** (`pip install --require-hashes`) num lock em `intelligence/`. Atualizar o lock é uma mudança revisada
   como qualquer outra.
5. **Telemetria desligada no ambiente do processo** (`LANGSMITH_TRACING=false`, `LANGCHAIN_TRACING_V2=false`), mais um teste que
   intercepta sockets e falha em qualquer conexão fora do loopback.
6. **Checkpointer:** `SqliteSaver` sobre o mesmo `projects/<id>/execution/checkpoint.sqlite`. A validação de checkpoint adulterado
   (`_checkpoint_ok`) continua como validação do estado restaurado.
7. **Contratos v2** em [`contracts/schemas/intelligence/`](../../../contracts/schemas/intelligence/), com fixtures válidas e inválidas
   em `contracts/fixtures/intelligence/`:
   - `ai.workflow.start.v2` e `ai.workflow.resume.v2` (motor → inteligência);
   - `ai.event.v1` (uma linha NDJSON por evento durante a execução) e `ai.ack.v2` (última linha);
   - `ai.agent.result.v1`, o envelope comum dos agentes;
   - `ai.model.request.v1` e `ai.model.response.v1`, para o proxy de modelo do motor;
   - `engineering.plan.v1` e `engineering.impact.v1`, que formalizam o que o código já valida.

   Os dados dos eventos novos da interface ficam em `contracts/schemas/ai/`: `agent.started`, `agent.progress`, `tool.execution`,
   `approval.required` e `eval`. Os contratos v1 atuais **não mudam** e continuam aceitos até a Fase 3 terminar.

Regras que os schemas fixam:

- uma ação proposta nunca carrega comando, executável ou conteúdo; `command.run` exige `commandId` resolvido pelo motor;
- ferramentas na inteligência só têm `effect: none`;
- `agent.progress` só aceita código, nunca texto livre;
- o veredito é sempre determinístico, e o juiz por modelo apenas registra;
- a requisição de modelo não escolhe modelo nem carrega chave;
- o proxy só existe em `127.0.0.1`, com token por execução;
- `interrupted` exige `approval`, e `failed` exige ao menos um erro.

## Consequências

- O instalador cresce cerca de 8 MB, e a primeira chamada à inteligência ganha ~0,7 s de importação.
- A inteligência deixa de ser só stdlib. No macOS e no Linux, onde se usava o Python do `PATH`, ela deixaria de funcionar
  sem um interpretador empacotado. Por isso a D2 foi decidida antes da Fase 3 (abaixo).

## D2 — Python empacotado no macOS e no Linux (decidida em 2026-11-02)

**Decisão:** python-build-standalone, release `20250409`, CPython `3.12.10` (a mesma versão do embed do Windows), variante
`install_only_stripped`. Os hashes vêm do `SHA256SUMS` da release, ficam fixados em `scripts/package-intelligence.py`, e o build falha
se não baterem.

Poda: `include/`, `share/`, Tcl/Tk, `test`, `idlelib`, `tkinter`, `ensurepip`, `lib2to3`, `site-packages` e links simbólicos
(`libpython3.12.so` → `.so.1.0` viraria uma cópia de 38 MB). O que é podado nem sai do arquivo. `bin/python3` vira um arquivo real,
porque o Tauri copia recursos como arquivos.

| Medida (2026-11-02) | macOS arm64 | Linux x64 | Windows x64 (embed) |
| --- | --- | --- | --- |
| Arquivo baixado | 15 MB | 21 MB | 10,6 MB |
| Interpretador podado | 27,6 MB (estimado) | **50,3 MB** (medido no WSL) | 21,5 MB |
| Orçamento do interpretador | 30 MB | 55 MB | 25 MB |
| Dependências em bytecode | a medir na CI | **26,5 MB** (após `strip`) | **26,0 MB** |
| Importação do grafo com bytecode | a medir na CI | 0,78 s | 0,7 s |

No Linux as rodas `manylinux` trazem símbolos (`zstandard`: 12 MB por módulo), então o build roda `strip --strip-unneeded` nos `.so`.
Sem isso, as dependências dariam 50,6 MB. No macOS o `strip` invalidaria a assinatura dos binários arm64, e eles ficam como vêm.

**Validado:**

- Windows: `build-runtime.ps1 -FetchPythonEmbed` + `PackagedAiSmokeTests`, que agora executa o grafo a partir do pacote.
- Linux: pacote montado num disco Linux (WSL Ubuntu 22.04) e grafo executado com `python3 -I` num ambiente vazio.
- macOS: só na CI, no runner `macos-latest`. A assinatura e a notarização do interpretador ficam para a Fase 8.
- O `langsmith` entra no pacote, mesmo ocioso. O teste de rede é obrigatório.

## Estado das demais decisões da auditoria

| # | Estado |
| --- | --- |
| D1 | **Decidida** por esta ADR |
| D2 | **Decidida**: python-build-standalone (seção D2 acima) |
| D3 | Pendente: embeddings. Recomendação: adiar até a eval de recall mostrar ganho; o `sqlite-vec` já vem com o checkpointer |
| D4 | Pendente: provedor e limites do modelo. Bloqueia a Fase 6; a Fase 3 usa modelo falso nos testes |
| D5 | Pendente: Harness Engineer dentro ou fora do produto (reavaliar na Fase 6) |
| D6 | **Feita**: `docs/blueprints/architecture-blueprint.md` §1 e §10 atualizados |
| D7 | Pendente: `workspaceId` reservado como `null` no `ai.workflow.start.v2` |
| D8 | Pendente: acompanhamento ao vivo por `seq` na página de execução já na Fase 3 |
