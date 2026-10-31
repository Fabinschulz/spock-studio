"""Memórias do runtime.

Cinco contextos, dois donos. Este pacote só guarda a memória de execução
(checkpoint para retomar o grafo). As outras ficam de fora:

- trabalho: estado do processo, em ``workflows.engineering.graph``
- conhecimento do projeto: índice lexical em ``knowledge``
- histórico de engenharia: diário do motor .NET
- avaliação: resultado dos evals no mesmo diário, nunca no índice
"""
