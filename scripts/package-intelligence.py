#!/usr/bin/env python3
"""Dependências e pacote da inteligência do Mariliz.

  dev                 instala intelligence/requirements.lock em intelligence/packages (fonte .py, para depurar)
  bundle --resources  copia a inteligência para o pacote do Tauri com as dependências em bytecode sem fonte e,
                      com --python <triple>, baixa e poda o CPython do python-build-standalone (Windows, macOS e Linux).

Precisa rodar com o mesmo Python 3.14 do runtime: o bytecode só carrega na mesma versão menor.
"""

from __future__ import annotations

import argparse
import compileall
import hashlib
import shutil
import subprocess
import sys
import tarfile
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
INTELLIGENCE = ROOT / "intelligence"
LOCK = INTELLIGENCE / "requirements.lock"
SOURCES = ("server.py", "agents", "knowledge", "memory", "workflows", "contracts", "infrastructure")
MB = 1024 * 1024
PACKAGES_BUDGET = 30 * MB

# python-build-standalone 20261110, CPython 3.14.10, nas três plataformas. A 3.14 é a linha que ainda recebe correções
# gerais (a 3.13 passou a só segurança em 2026-10); a 3.15 tem um mês. Hashes do SHA256SUMS da release.
PY_MINOR = "3.14"
PBS_RELEASE = "https://github.com/astral-sh/python-build-standalone/releases/download/20261110"
PBS = {
    "aarch64-apple-darwin": (
        "cpython-3.14.10+20261110-aarch64-apple-darwin-install_only_stripped.tar.gz",
        "f15ec3d15460dd66c2eb88639b152cd9040e7d1ece11e38bf89e8f53f0171e02",
        30 * MB,
    ),
    "x86_64-pc-windows-msvc": (
        "cpython-3.14.10+20261110-x86_64-pc-windows-msvc-install_only_stripped.tar.gz",
        "32b6cf90a03e2bc15fda8d2ab7ff78c4870f138887d612932650ce764efaab8f",
        40 * MB,
    ),
    "x86_64-unknown-linux-gnu": (
        "cpython-3.14.10+20261110-x86_64-unknown-linux-gnu-install_only_stripped.tar.gz",
        "c2ff2e97422f5d3896a16e0317fd212ba977bbf2688d93a6f7a03d817506bd30",
        45 * MB,
    ),
}
# Licenças das bibliotecas nativas que o PBS liga ao interpretador (textos da tag da release).
PBS_LINUX_NOTICES = ROOT / "scripts" / "notices" / "python-build-standalone"
PBS_WINDOWS_NOTICES = ROOT / "scripts" / "notices" / "python-build-standalone-windows"
# Sem PYTHONPATH, PYTHONHOME nem registro: o mesmo isolamento que o embeddable oficial dava. "import site" fica ligado
# porque as dependências da inteligência dependem dos .pth que o site processa.
WINDOWS_PTH = "Lib\nDLLs\n.\nimport site\n"
PBS_PRUNE_STDLIB = {"test", "idlelib", "tkinter", "turtledemo", "ensurepip", "lib2to3", "pydoc_data", "site-packages"}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("dev")
    bundle = commands.add_parser("bundle")
    bundle.add_argument("--resources", required=True, type=Path)
    bundle.add_argument("--python", choices=sorted(PBS))
    args = parser.parse_args()

    if f"{sys.version_info[0]}.{sys.version_info[1]}" != PY_MINOR:
        raise SystemExit(f"Use Python {PY_MINOR} (o mesmo do runtime); este é {sys.version.split()[0]}.")
    if args.command == "dev":
        install(INTELLIGENCE / "packages", bytecode=False)
        return
    copy_sources(args.resources / "intelligence")
    install(args.resources / "intelligence" / "packages", bytecode=True)
    if args.python:
        fetch_python(args.python, args.resources / "python")


def install(target: Path, bytecode: bool) -> None:
    if target.exists():
        shutil.rmtree(target)
    subprocess.run(
        [sys.executable, "-m", "pip", "install", "--disable-pip-version-check", "--require-hashes", "--only-binary=:all:",
         "--no-deps", "--no-compile", "--target", str(target), "-r", str(LOCK)],
        check=True,
    )
    shutil.rmtree(target / "bin", ignore_errors=True)
    if bytecode and sys.platform == "linux" and shutil.which("strip"):
        # As rodas manylinux trazem símbolos (zstandard: 12 MB por módulo; sem eles, < 1 MB). No macOS o strip
        # invalidaria a assinatura dos binários arm64, então lá eles ficam como vêm.
        modules = [str(path) for path in target.rglob("*.so")]
        if modules:
            subprocess.run(["strip", "--strip-unneeded", *modules], check=True)
    if bytecode:
        if not compileall.compile_dir(str(target), quiet=1, legacy=True, workers=0):
            raise SystemExit("Falha ao compilar as dependências.")
        for source in target.rglob("*.py"):
            source.unlink()
        for cache in list(target.rglob("__pycache__")):
            shutil.rmtree(cache)
    size = _size(target)
    if bytecode and size > PACKAGES_BUDGET:
        raise SystemExit(f"As dependências têm {size / MB:.1f} MB e passam do orçamento de {PACKAGES_BUDGET // MB} MB.")
    print(f"Dependências da inteligência: {target} ({size / MB:.1f} MB)")


def copy_sources(target: Path) -> None:
    _clear(target)
    ignore = shutil.ignore_patterns("__pycache__", "*.pyc", "tests")
    for name in SOURCES:
        source = INTELLIGENCE / name
        if source.is_dir():
            shutil.copytree(source, target / name, ignore=ignore)
        elif source.is_file():
            shutil.copy2(source, target / name)
    print(f"Inteligência: {target} ({_size(target) / 1024:.0f} KB)")


def fetch_python(triple: str, target: Path) -> None:
    name, digest, budget = PBS[triple]
    cache = ROOT / ".cache" / "python-build-standalone"
    cache.mkdir(parents=True, exist_ok=True)
    archive = cache / name
    if not archive.exists():
        print(f"Baixando {PBS_RELEASE}/{name}")
        urllib.request.urlretrieve(f"{PBS_RELEASE}/{name}", archive)
    if hashlib.sha256(archive.read_bytes()).hexdigest() != digest:
        archive.unlink()
        raise SystemExit(f"Hash inesperado em {name}.")

    windows = "windows" in triple
    extracted = cache / triple
    if extracted.exists():
        shutil.rmtree(extracted)
    with tarfile.open(archive) as tar:
        # O que será podado nem sai do arquivo (terminfo tem milhares de links e nomes que só diferem na caixa).
        members = [member for member in tar.getmembers() if not _pruned_on_extract(member.name, windows)]
        tar.extractall(extracted, members=members, filter="data")
    home = extracted / "python"
    _prune_windows(home) if windows else _prune(home)

    _clear(target)
    # O Tauri copia recursos como arquivos; links simbólicos viram cópias reais.
    shutil.copytree(home, target, symlinks=False, dirs_exist_ok=True)
    if windows:
        (target / f"python{PY_MINOR.replace('.', '')}._pth").write_text(WINDOWS_PTH, encoding="ascii", newline="\r\n")
        shutil.copytree(PBS_WINDOWS_NOTICES, target / "licenses" / "python-build-standalone", dirs_exist_ok=True)
    if "linux" in triple:
        shutil.copytree(PBS_LINUX_NOTICES, target / "licenses" / "python-build-standalone", dirs_exist_ok=True)
    size = _size(target)
    if size > budget:
        raise SystemExit(f"O interpretador tem {size / MB:.1f} MB e passa do orçamento de {budget // MB} MB.")
    print(f"Interpretador embarcado: {target} ({size / MB:.1f} MB)")


def _pruned_on_extract(name: str, windows: bool) -> bool:
    parts = name.split("/")
    if windows:
        if len(parts) > 1 and parts[1] in {"include", "libs", "Scripts", "tcl"}:
            return True
        return len(parts) > 2 and parts[1] == "Lib" and parts[2] in PBS_PRUNE_STDLIB
    if len(parts) > 1 and parts[1] in {"include", "share"}:
        return True
    return len(parts) > 3 and parts[1] == "lib" and parts[2] == f"python{PY_MINOR}" and parts[3] in PBS_PRUNE_STDLIB


def _prune_windows(home: Path) -> None:
    (home / "pythonw.exe").unlink(missing_ok=True)
    for entry in (home / "DLLs").iterdir():
        if entry.name.startswith(("_tkinter", "_test", "_ctypes_test", "xxlimited", "tcl", "tk", "libtommath")):
            entry.unlink()


def _prune(home: Path) -> None:
    for name in ("include", "share"):
        shutil.rmtree(home / name, ignore_errors=True)
    # Na 3.14 o executável do PBS leva o interpretador inteiro (libpython estático) e não carrega o libpython. Fica só
    # ele, com o nome que o host e o motor procuram (bin/python3); o libpython, que só serve a quem embute o Python, sai.
    executable = home / "bin" / f"python{PY_MINOR}"
    for entry in (home / "bin").iterdir():
        if entry != executable:
            entry.unlink()
    executable.rename(home / "bin" / "python3")
    # O Tauri copia links simbólicos como arquivos inteiros.
    for link in [item for item in home.rglob("*") if item.is_symlink()]:
        link.unlink()
    lib = home / "lib"
    for entry in lib.iterdir():
        if entry.name.startswith(("tcl", "tk", "libtcl", "libpython", "Tix", "itcl", "thread", "pkgconfig")) or entry.suffix == ".a":
            shutil.rmtree(entry) if entry.is_dir() else entry.unlink()
    stdlib = lib / f"python{PY_MINOR}"
    for entry in stdlib.iterdir():
        if entry.name in PBS_PRUNE_STDLIB or entry.name.startswith("config-"):
            shutil.rmtree(entry)
    # Na 3.14 o Berkeley DB do Linux fica só no _dbm, não mais no libpython: sem o módulo, o pacote não leva o DB
    # nem a obrigação da Sleepycat, e o dbm cai no dbm.sqlite3.
    for entry in (stdlib / "lib-dynload").iterdir():
        if entry.name.startswith(("_dbm.", "_tkinter", "_test", "_ctypes_test", "xxlimited")):
            entry.unlink()


def _clear(target: Path) -> None:
    """Esvazia a pasta de recursos sem apagar o .gitkeep versionado."""
    target.mkdir(parents=True, exist_ok=True)
    for entry in target.iterdir():
        if entry.name == ".gitkeep":
            continue
        shutil.rmtree(entry) if entry.is_dir() and not entry.is_symlink() else entry.unlink()


def _size(path: Path) -> int:
    return sum(item.stat().st_size for item in path.rglob("*") if item.is_file() and not item.is_symlink())


if __name__ == "__main__":
    main()
