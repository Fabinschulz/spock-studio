# Licenças das bibliotecas do Python embarcado (Linux) / Licenses of the bundled Python's libraries (Linux)

O Spock Studio leva no Linux o python-build-standalone 20250409 (CPython 3.12.10). As bibliotecas abaixo estão ligadas
estaticamente ao `lib/libpython3.12.so.1.0`. A licença do próprio CPython está em `lib/python3.12/LICENSE.txt`, e a do
Berkeley DB em `../berkeley-db/`.

Spock Studio ships python-build-standalone 20250409 (CPython 3.12.10) on Linux. The libraries below are statically linked
into `lib/libpython3.12.so.1.0`. CPython's own license is in `lib/python3.12/LICENSE.txt`, and Berkeley DB's in
`../berkeley-db/`.

| Biblioteca / Library | Versão / Version | Licença / License | Arquivo / File |
| --- | --- | --- | --- |
| bzip2 | 1.0.8 | bzip2-1.0.6 | LICENSE.bzip2.txt |
| expat | 2.6.3 | MIT | LICENSE.expat.txt |
| libedit | 20240808-3.1 | BSD-3-Clause | LICENSE.libedit.txt |
| libffi | 3.4.6 | MIT | LICENSE.libffi.txt |
| libX11 | 1.6.12 | MIT, X11 | LICENSE.libX11.txt |
| libXau | 1.0.11 | MIT | LICENSE.libXau.txt |
| libxcb | 1.14 | MIT | LICENSE.libxcb.txt |
| mpdecimal | 4.0.0 | BSD-2-Clause | LICENSE.mpdecimal.txt |
| ncurses | 6.5 | X11 | LICENSE.ncurses.txt |
| OpenSSL | 3.0.16 | Apache-2.0 | LICENSE.openssl-3.txt |
| SQLite | 3.47.1 | Domínio público / Public domain | LICENSE.sqlite.txt |
| Tcl, Tk | 8.6.14 | TCL | LICENSE.tcl.txt |
| Tix | 8.4.3.6 | TCL | LICENSE.tix.txt |
| libuuid | 1.0.3 | BSD-3-Clause | LICENSE.libuuid.txt |
| xz (liblzma) | 5.2.12 | Domínio público / Public domain | LICENSE.liblzma.txt |
| zlib | 1.3.1 | Zlib | LICENSE.zlib.txt |

Procedência: os textos são os que o python-build-standalone publica na tag `20250409`
(`https://github.com/astral-sh/python-build-standalone/tree/20250409`, um `LICENSE.<biblioteca>.txt` por biblioteca, conforme
`pythonbuild/downloads.py`). Eles foram copiados sem alteração. As versões e a lista de bibliotecas vêm de
`cpython-unix/targets.yml` (alvo `x86_64-unknown-linux-gnu`) na mesma tag.

Provenance: these are the texts python-build-standalone publishes at tag `20250409`, copied unchanged. Versions and the
library list come from `cpython-unix/targets.yml` (target `x86_64-unknown-linux-gnu`) at the same tag.
