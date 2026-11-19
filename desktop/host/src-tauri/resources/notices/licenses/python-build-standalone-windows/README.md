# Licenças das bibliotecas do Python embarcado (Windows) / Licenses of the bundled Python's libraries (Windows)

O Spock Studio leva no Windows o python-build-standalone 20261110 (CPython 3.12.16). As bibliotecas abaixo vão como DLL
(`DLLs/libcrypto-3-x64.dll`, `DLLs/libssl-3-x64.dll`, `DLLs/sqlite3.dll`, `DLLs/libffi-8.dll`, `DLLs/zlib1.dll`) ou ligadas
aos módulos `.pyd` (`_bz2`, `_lzma`, `pyexpat`, `_decimal`). A licença do próprio CPython está em `../../LICENSE.txt`.

Spock Studio ships python-build-standalone 20261110 (CPython 3.12.16) on Windows. The libraries below ship as DLLs or are
linked into the `.pyd` modules listed above. CPython's own license is in `../../LICENSE.txt`.

| Biblioteca / Library | Versão / Version | Licença / License | Arquivo / File |
| --- | --- | --- | --- |
| bzip2 | 1.0.8 | bzip2-1.0.6 | LICENSE.bzip2.txt |
| expat (cópia do CPython / CPython's bundled copy) | 2.9.0 | MIT | LICENSE.expat.txt |
| libffi | 3.8.0 | MIT | LICENSE.libffi.txt |
| mpdecimal (libmpdec do CPython / CPython's bundled libmpdec) | 2.5.1 | BSD-2-Clause | LICENSE.mpdecimal.txt |
| OpenSSL | 3.5.9 | Apache-2.0 | LICENSE.openssl-3.txt |
| SQLite | 3.53.1 | Domínio público / Public domain | LICENSE.sqlite.txt |
| xz (liblzma) | 5.8.4 | 0BSD | LICENSE.liblzma.txt |
| zlib | 1.4 | Zlib | LICENSE.zlib.txt |

`vcruntime140.dll` e `vcruntime140_1.dll` são o Microsoft Visual C++ Redistributable, tratado no inventário de licenças do
Spock. Tcl/Tk e `_tkinter` são podados do pacote.

`vcruntime140.dll` and `vcruntime140_1.dll` are the Microsoft Visual C++ Redistributable, covered by Spock's license
inventory. Tcl/Tk and `_tkinter` are pruned from the package.

Procedência: os textos são os que o python-build-standalone publica na tag `20261110`, copiados sem alteração. As versões
foram lidas do próprio interpretador oficial (SHA-256 fixado em `scripts/package-intelligence.py`): `ssl.OPENSSL_VERSION`,
`sqlite3.sqlite_version`, `zlib.ZLIB_RUNTIME_VERSION`, `pyexpat.EXPAT_VERSION` e `decimal.__libmpdec_version__`. As de bzip2,
xz e libffi vêm de `pythonbuild/downloads.json` na mesma tag. O CPython 3.12 usa as próprias cópias do expat e do libmpdec,
e por isso as versões são diferentes das do Linux.

Provenance: texts published by python-build-standalone at tag `20261110`, copied unchanged. Versions were read from the
official interpreter itself (SHA-256 pinned in `scripts/package-intelligence.py`). The bzip2, xz and libffi versions come
from `pythonbuild/downloads.json` at the same tag. CPython 3.12 uses its own copies of expat and libmpdec, which is why
those versions differ from Linux.
