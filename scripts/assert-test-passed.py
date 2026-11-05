#!/usr/bin/env python3
"""Falha se o teste pedido não rodou ou não passou, lendo o resultado .trx do ``dotnet test``.

Um filtro que não casa com nenhum teste termina com código 0 no ``dotnet test``, e um teste ignorado também não falha. Para
os smokes do instalador isso seria um verde sem prova: aqui o teste precisa existir, ter rodado e ter passado.

  assert-test-passed.py <arquivo.trx> <trecho do nome do teste> [--min N]
"""

from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("trx", type=Path)
    parser.add_argument("name", help="trecho do nome completo do teste")
    parser.add_argument("--min", type=int, default=1, help="quantos testes precisam ter passado (padrão: 1)")
    args = parser.parse_args()

    if not args.trx.is_file():
        print(f"Sem resultado de teste em {args.trx}.", file=sys.stderr)
        return 1

    # O .trx é o que o próprio `dotnet test` acabou de gravar na CI, não um arquivo de terceiros: o parser da biblioteca padrão basta.
    results = [
        (node.get("testName", ""), node.get("outcome", ""))
        for node in ET.parse(args.trx).iter()
        if node.tag.endswith("UnitTestResult")
    ]
    matched = [(name, outcome) for name, outcome in results if args.name in name]
    not_passed = [(name, outcome) for name, outcome in matched if outcome != "Passed"]
    passed = len(matched) - len(not_passed)

    for name, outcome in matched:
        print(f"{outcome}: {name}")
    if not_passed:
        print(f"{len(not_passed)} teste(s) que casam com '{args.name}' não passaram.", file=sys.stderr)
        return 1
    if passed < args.min:
        print(f"Esperava ao menos {args.min} teste(s) aprovado(s) com '{args.name}'; rodaram {passed}.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
