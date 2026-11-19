# Licenças das bibliotecas do Python embarcado (Linux) / Licenses of the bundled Python's libraries (Linux)

O Spock Studio leva no Linux o python-build-standalone 20261110 (CPython 3.12.16). As bibliotecas abaixo estão ligadas
estaticamente ao `lib/libpython3.12.so.1.0`. A licença do próprio CPython está em `lib/python3.12/LICENSE.txt`, e a do
Berkeley DB em `../berkeley-db/`.

Spock Studio ships python-build-standalone 20261110 (CPython 3.12.16) on Linux. The libraries below are statically linked
into `lib/libpython3.12.so.1.0`. CPython's own license is in `lib/python3.12/LICENSE.txt`, and Berkeley DB's in
`../berkeley-db/`.

| Biblioteca / Library | Versão / Version | Licença / License | Arquivo / File |
| --- | --- | --- | --- |
| bzip2 | 1.0.8 | bzip2-1.0.6 | LICENSE.bzip2.txt |
| expat | 2.10.0 | MIT | LICENSE.expat.txt |
| libedit | 20260512-3.1 | BSD-3-Clause | LICENSE.libedit.txt |
| libffi | 3.8.0 | MIT | LICENSE.libffi.txt |
| libX11 | 1.8.14 | MIT, X11 | LICENSE.libX11.txt |
| libXau | 1.0.12 | MIT | LICENSE.libXau.txt |
| libxcb | 1.17.0 | MIT | LICENSE.libxcb.txt |
| mpdecimal | 4.0.0 | BSD-2-Clause | LICENSE.mpdecimal.txt |
| ncurses | 6.5 | X11 | LICENSE.ncurses.txt |
| OpenSSL | 3.5.9 | Apache-2.0 | LICENSE.openssl-3.txt |
| SQLite | 3.53.1 | Domínio público / Public domain | LICENSE.sqlite.txt |
| Tcl, Tk | 9.0.4 | TCL | LICENSE.tcl.txt |
| libuuid | 1.0.3 | BSD-3-Clause | LICENSE.libuuid.txt |
| xz (liblzma) | 5.8.4 | 0BSD | LICENSE.liblzma.txt |
| zlib | 1.4 | Zlib | LICENSE.zlib.txt |
| zstd | 1.5.7 | BSD-3-Clause | LICENSE.zstd.txt |

Procedência: os textos são os que o python-build-standalone publica na tag `20261110`
(`https://github.com/astral-sh/python-build-standalone/tree/20261110`, um `LICENSE.<biblioteca>.txt` por biblioteca, conforme
`pythonbuild/downloads.json`). Eles foram copiados sem alteração. As versões vêm de `pythonbuild/downloads.json`, e a lista
de bibliotecas vem de `cpython-unix/targets.yml` (alvo `x86_64-unknown-linux-gnu`), na mesma tag. O zstd está na lista do
alvo e vai listado por precaução: o CPython 3.12 não tem o módulo `_zstd`.

Provenance: these are the texts python-build-standalone publishes at tag `20261110`, copied unchanged. Versions come from
`pythonbuild/downloads.json`, and the library list from `cpython-unix/targets.yml` (target `x86_64-unknown-linux-gnu`), at
the same tag. zstd is listed as a precaution because it is in the target list: CPython 3.12 has no `_zstd` module.
