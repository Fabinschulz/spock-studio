# Berkeley DB 6.0.19 no Python embarcado (Linux) / Berkeley DB 6.0.19 in the bundled Python (Linux)

O interpretador Python que o Spock Studio leva no Linux é o python-build-standalone 20261110 (CPython 3.12.16). O módulo
`_dbm` e a biblioteca Berkeley DB 6.0.19 estão ligados estaticamente ao `lib/libpython3.12.so.1.0`. A licença do Berkeley DB
está em `LICENSE-6.0.19.txt`, nesta pasta. O Spock Studio não modifica nenhum desses componentes.

The Python interpreter shipped with Spock Studio on Linux is python-build-standalone 20261110 (CPython 3.12.16). The `_dbm`
module and the Berkeley DB 6.0.19 library are statically linked into `lib/libpython3.12.so.1.0`. The Berkeley DB license is
in `LICENSE-6.0.19.txt` in this folder. Spock Studio does not modify any of these components.

## Código-fonte completo / Complete source code

| Componente / Component | Onde obter / Where to get it | SHA-256 |
| --- | --- | --- |
| Berkeley DB 6.0.19 | https://ftp.osuosl.org/pub/blfs/conglomeration/db/db-6.0.19.tar.gz | `2917c28f60903908c2ca4587ded1363b812c4e830a5326aaa77c9879d13ae18e` |
| CPython 3.12.16 | https://www.python.org/ftp/python/3.12.16/Python-3.12.16.tgz | não fixado / not pinned |
| Receita de build e demais bibliotecas do interpretador / Build recipe and the interpreter's other libraries | https://github.com/astral-sh/python-build-standalone/tree/20261110 (versões e endereços em / versions and URLs in `pythonbuild/downloads.json`) | não fixado / not pinned |
| Interpretador redistribuído / Redistributed interpreter | https://github.com/astral-sh/python-build-standalone/releases/download/20261110/cpython-3.12.16+20261110-x86_64-unknown-linux-gnu-install_only_stripped.tar.gz | `6a9e62dad033790aef722dbd370e0ce76ce17b73627ba3ae679f537049350b39` |

Todo esse código-fonte é gratuito e livremente redistribuível. Se algum endereço deixar de funcionar, o fornecedor do Spock
Studio entrega uma cópia mediante solicitação pelo canal de suporte do produto, sem cobrar mais que o custo da distribuição.

All of this source code is free of charge and freely redistributable. If any address stops working, the Spock Studio
vendor provides a copy on request through the product's support channel, for no more than the cost of distribution.
