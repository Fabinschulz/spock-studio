# Licenças das bibliotecas do Python embarcado (Linux) / Licenses of the bundled Python's libraries (Linux)

O Spock Studio leva no Linux o python-build-standalone 20261110 (CPython 3.14.10). As bibliotecas abaixo estão ligadas
estaticamente ao executável `bin/python3`. A licença do próprio CPython está em `lib/python3.14/LICENSE.txt`.

Spock Studio ships python-build-standalone 20261110 (CPython 3.14.10) on Linux. The libraries below are statically linked
into the `bin/python3` executable. CPython's own license is in `lib/python3.14/LICENSE.txt`.

| Biblioteca / Library | Versão / Version | Licença / License | Arquivo / File |
| --- | --- | --- | --- |
| bzip2 | 1.0.8 | bzip2-1.0.6 | LICENSE.bzip2.txt |
| expat | 2.10.0 | MIT | LICENSE.expat.txt |
| libedit | 20260512-3.1 | BSD-3-Clause | LICENSE.libedit.txt |
| libffi | 3.8.0 | MIT | LICENSE.libffi.txt |
| mpdecimal | 4.0.0 | BSD-2-Clause | LICENSE.mpdecimal.txt |
| ncurses | 6.5 | X11 | LICENSE.ncurses.txt |
| OpenSSL | 3.5.9 | Apache-2.0 | LICENSE.openssl-3.txt |
| SQLite | 3.53.1 | Domínio público / Public domain | LICENSE.sqlite.txt |
| libuuid | 1.0.3 | BSD-3-Clause | LICENSE.libuuid.txt |
| xz (liblzma) | 5.8.4 | 0BSD | LICENSE.liblzma.txt |
| zlib | 1.4 | Zlib | LICENSE.zlib.txt |
| zstd | 1.5.7 | BSD-3-Clause | LICENSE.zstd.txt |

Fora do pacote: o arquivo oficial também traz o `_dbm` (com o Berkeley DB 6.0.19, licença Sleepycat), o `_tkinter` (com
Tcl/Tk e X11) e o `libpython3.14.so`. Os três ficam em arquivos separados do executável e são podados pelo
`scripts/package-intelligence.py`.

Not shipped: the official archive also contains `_dbm` (with Berkeley DB 6.0.19, Sleepycat license), `_tkinter` (with
Tcl/Tk and X11) and `libpython3.14.so`. All three are separate from the executable and are pruned by
`scripts/package-intelligence.py`.

Procedência: os textos são os que o python-build-standalone publica na tag `20261110`
(`https://github.com/astral-sh/python-build-standalone/tree/20261110`, um `LICENSE.<biblioteca>.txt` por biblioteca, conforme
`pythonbuild/downloads.json`). Eles foram copiados sem alteração. As versões vêm de `pythonbuild/downloads.json`, e a lista
de bibliotecas vem de `cpython-unix/targets.yml` (alvo `x86_64-unknown-linux-gnu`), na mesma tag. A presença de cada uma foi
conferida nos símbolos do executável oficial (`PyInit__zstd`, `PyInit__curses`, `uuid_generate`, `el_init`).

Provenance: these are the texts python-build-standalone publishes at tag `20261110`, copied unchanged. Versions come from
`pythonbuild/downloads.json`, and the library list from `cpython-unix/targets.yml` (target `x86_64-unknown-linux-gnu`), at
the same tag. Each library's presence was checked against the official executable's symbols.
