# Berkeley DB 6.0.19 no Python embarcado (Linux) / Berkeley DB 6.0.19 in the bundled Python (Linux)

O interpretador Python que o Spock Studio leva no Linux é o python-build-standalone 20250409 (CPython 3.12.10). O módulo
`_dbm` e a biblioteca Berkeley DB 6.0.19 estão ligados estaticamente ao `lib/libpython3.12.so.1.0`. A licença do Berkeley DB
está em `LICENSE-6.0.19.txt`, nesta pasta. O Spock Studio não modifica nenhum desses componentes.

The Python interpreter shipped with Spock Studio on Linux is python-build-standalone 20250409 (CPython 3.12.10). The `_dbm`
module and the Berkeley DB 6.0.19 library are statically linked into `lib/libpython3.12.so.1.0`. The Berkeley DB license is
in `LICENSE-6.0.19.txt` in this folder. Spock Studio does not modify any of these components.

## Código-fonte completo / Complete source code

| Componente / Component | Onde obter / Where to get it | SHA-256 |
| --- | --- | --- |
| Berkeley DB 6.0.19 | https://ftp.osuosl.org/pub/blfs/conglomeration/db/db-6.0.19.tar.gz | `2917c28f60903908c2ca4587ded1363b812c4e830a5326aaa77c9879d13ae18e` |
| CPython 3.12.10 | https://www.python.org/ftp/python/3.12.10/Python-3.12.10.tgz | não fixado / not pinned |
| Receita de build e demais bibliotecas do interpretador / Build recipe and the interpreter's other libraries | https://github.com/astral-sh/python-build-standalone/tree/20250409 (versões e endereços em / versions and URLs in `pythonbuild/downloads.py`) | não fixado / not pinned |
| Interpretador redistribuído / Redistributed interpreter | https://github.com/astral-sh/python-build-standalone/releases/download/20250409/cpython-3.12.10+20250409-x86_64-unknown-linux-gnu-install_only_stripped.tar.gz | `8c59b9ac6bff2dc3934181d7bc82594f9f59a613afed8d72c9e89d7194e790ee` |

Todo esse código-fonte é gratuito e livremente redistribuível. Se algum endereço deixar de funcionar, o fornecedor do Spock
Studio entrega uma cópia mediante solicitação pelo canal de suporte do produto, sem cobrar mais que o custo da distribuição.

All of this source code is free of charge and freely redistributable. If any address stops working, the Spock Studio
vendor provides a copy on request through the product's support channel, for no more than the cost of distribution.
