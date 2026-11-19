# Evals

Medições que viram gate nos testes e, quando alguém as grava, entram no histórico (`history.jsonl`, uma linha por
medição, com a data). O histórico mostra a tendência; o teste só diz se a meta do dia foi atingida.

| Eval | O que mede | Dataset | Gate | Gravar no histórico |
| --- | --- | --- | --- | --- |
| Recuperação sintética | Recall@k e MRR num corpus pequeno | `datasets/recall.json` | `tests/test_recall_eval.py` | não grava |
| Recuperação no próprio Spock | Recall@5, MRR e trechos de teste no topo | `datasets/spock-questions.json` | manual | não grava |
| Referências da Organização | Recall@3, MRR e contaminação (trecho de outra stack), com e sem o filtro de stack | `datasets/references.json` sobre `datasets/reference-corpus.json` | `tests/test_reference_eval.py` (recall@3 ≥ 0,8 e contaminação 0) | `python intelligence/evaluations/references.py --record` |
| Análise de arquitetura | Violações esperadas encontradas e falsos positivos, contra um gabarito | `datasets/architecture.json` | `ArchitectureEvaluationTests` (recall 1 e nenhum falso positivo) | `MARILIZ_RECORD_EVALS=1 dotnet test runtime/tests/Mariliz.Application.Tests --filter ArchitectureEvaluationTests` |
| Gestão | Fatos e agrupamentos dos relatórios gerenciais | `datasets/management.json` | `tests/test_management_eval.py` | não grava |

O corpus de referências é gerado pelo motor a partir dos documentos `@reference/` que ele indexa. Mudou a pasta
`docs/organizacao`? Regenere com `MARILIZ_UPDATE_GOLDEN=1 dotnet test runtime/tests/Mariliz.Application.Tests --filter
ReferenceCorpusTests` e rode o eval de referências; o teste `ReferenceCorpusTests` falha enquanto o corpus estiver velho.

Os casos de arquitetura reproduzem a forma dos repositórios de referência do catálogo (ids como `NET-DOC`, `EMP-BE`), sem o
código deles e sem nome de organização.

Os datasets foram escritos por quem conhece o catálogo: as perguntas tendem a usar as palavras dele. Uma pergunta real que
fale de outro jeito e não ache o padrão vira caso novo.
