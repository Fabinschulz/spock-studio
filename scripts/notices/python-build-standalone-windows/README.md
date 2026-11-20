# Licenças das bibliotecas do Python embarcado (Windows) / Licenses of the bundled Python's libraries (Windows)

O Spock Studio leva no Windows o python-build-standalone 20261110 (CPython 3.14.10). As bibliotecas abaixo vão como DLL
(`DLLs/libcrypto-3-x64.dll`, `DLLs/libssl-3-x64.dll`, `DLLs/sqlite3.dll`, `DLLs/libffi-8.dll`, `DLLs/zlib1.dll`) ou ligadas
aos módulos `.pyd` (`_bz2`, `_lzma`, `pyexpat`, `_decimal`, `_zstd`). A licença do próprio CPython está em `../../LICENSE.txt`.

Spock Studio ships python-build-standalone 20261110 (CPython 3.14.10) on Windows. The libraries below ship as DLLs or are
linked into the `.pyd` modules listed above. CPython's own license is in `../../LICENSE.txt`.

| Biblioteca / Library | Versão / Version | Licença / License | Arquivo / File |
| --- | --- | --- | --- |
| bzip2 | 1.0.8 | bzip2-1.0.6 | LICENSE.bzip2.txt |
| expat (cópia do CPython / CPython's bundled copy) | 2.9.0 | MIT | LICENSE.expat.txt |
| libffi | 3.8.0 | MIT | LICENSE.libffi.txt |
| mpdecimal (libmpdec do CPython / CPython's bundled libmpdec) | 4.0.0 | BSD-2-Clause | LICENSE.mpdecimal.txt |
| OpenSSL | 3.5.9 | Apache-2.0 | LICENSE.openssl-3.txt |
| SQLite | 3.53.1 | Domínio público / Public domain | LICENSE.sqlite.txt |
| xz (liblzma) | 5.8.4 | 0BSD | LICENSE.liblzma.txt |
| zlib-ng (`zlib1.dll`, API zlib 1.3.1) | 2.2.4 | Zlib | LICENSE.zlib-ng.txt |
| zstd | 1.5.7 | BSD-3-Clause | LICENSE.zstd.txt |

`vcruntime140.dll` e `vcruntime140_1.dll` são o Microsoft Visual C++ Redistributable, tratado no inventário de licenças do
Spock. Tcl/Tk, `_tkinter` e `libtommath.dll` (usado só pelo Tcl) são podados do pacote.

`vcruntime140.dll` and `vcruntime140_1.dll` are the Microsoft Visual C++ Redistributable, covered by Spock's license
inventory. Tcl/Tk, `_tkinter` and `libtommath.dll` (used only by Tcl) are pruned from the package.

Procedência: os textos são os que o python-build-standalone publica na tag `20261110`, copiados sem alteração. As versões
foram lidas do próprio interpretador oficial (SHA-256 fixado em `scripts/package-intelligence.py`): `ssl.OPENSSL_VERSION`,
`sqlite3.sqlite_version`, `zlib.ZLIB_RUNTIME_VERSION` (`1.3.1.zlib-ng`), `pyexpat.EXPAT_VERSION`,
`decimal.__libmpdec_version__` e `compression.zstd.zstd_version_info`. As de bzip2, xz, libffi e zlib-ng vêm de
`pythonbuild/downloads.json` na mesma tag. O CPython usa no Windows as próprias cópias do expat e do libmpdec, e por isso a
versão do expat é diferente da do Linux.

Provenance: texts published by python-build-standalone at tag `20261110`, copied unchanged. Versions were read from the
official interpreter itself (SHA-256 pinned in `scripts/package-intelligence.py`). The bzip2, xz, libffi and zlib-ng
versions come from `pythonbuild/downloads.json` at the same tag. On Windows CPython uses its own copies of expat and
libmpdec, which is why the expat version differs from Linux.
