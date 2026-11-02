#!/usr/bin/env python3
"""Dependências e pacote da inteligência do Mariliz.

  dev                 instala intelligence/requirements.lock em intelligence/packages (fonte .py, para depurar)
  bundle --resources  copia a inteligência para o pacote do Tauri com as dependências em bytecode sem fonte e,
                      com --python <triple>, baixa e poda o CPython do python-build-standalone (macOS e Linux).
                      No Windows o interpretador continua vindo do CPython embed oficial (scripts/build-runtime.ps1).

Precisa rodar com o mesmo Python 3.12 do runtime: o bytecode só carrega na mesma versão menor.
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
SOURCES = ("server.py", "knowledge", "memory", "workflows", "contracts", "infrastructure")
MB = 1024 * 1024
PACKAGES_BUDGET = 30 * MB

# python-build-standalone 20250409, CPython 3.12.10 (a mesma versão do embed do Windows). Hashes do SHA256SUMS da release.
PBS_RELEASE = "https://github.com/astral-sh/python-build-standalone/releases/download/20250409"
PBS = {
    "aarch64-apple-darwin": (
        "cpython-3.12.10+20250409-aarch64-apple-darwin-install_only_stripped.tar.gz",
        "0be1fe0b35a4d3c382141764ef16ed3b8cc2b4620b657f678daa7b7f8df39699",
        30 * MB,
    ),
    "x86_64-unknown-linux-gnu": (
        "cpython-3.12.10+20250409-x86_64-unknown-linux-gnu-install_only_stripped.tar.gz",
        "8c59b9ac6bff2dc3934181d7bc82594f9f59a613afed8d72c9e89d7194e790ee",
        55 * MB,
    ),
}
PBS_PRUNE_STDLIB = {"test", "idlelib", "tkinter", "turtledemo", "ensurepip", "lib2to3", "pydoc_data", "site-packages"}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("dev")
    bundle = commands.add_parser("bundle")
    bundle.add_argument("--resources", required=True, type=Path)
    bundle.add_argument("--python", choices=sorted(PBS))
    args = parser.parse_args()

    if sys.version_info[:2] != (3, 12):
        raise SystemExit(f"Use Python 3.12 (o mesmo do runtime); este é {sys.version.split()[0]}.")
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

    extracted = cache / triple
    if extracted.exists():
        shutil.rmtree(extracted)
    with tarfile.open(archive) as tar:
        # O que será podado nem sai do arquivo (terminfo tem milhares de links e nomes que só diferem na caixa).
        members = [member for member in tar.getmembers() if not _pruned_on_extract(member.name)]
        tar.extractall(extracted, members=members, filter="data")
    home = extracted / "python"
    _prune(home)

    _clear(target)
    # O Tauri copia recursos como arquivos; links simbólicos viram cópias reais.
    shutil.copytree(home, target, symlinks=False, dirs_exist_ok=True)
    python3 = target / "bin" / "python3"
    if python3.is_symlink() or not python3.exists():
        python3.unlink(missing_ok=True)
        shutil.copy2(target / "bin" / "python3.12", python3)
    size = _size(target)
    if size > budget:
        raise SystemExit(f"O interpretador tem {size / MB:.1f} MB e passa do orçamento de {budget // MB} MB.")
    print(f"Interpretador embarcado: {target} ({size / MB:.1f} MB)")


def _pruned_on_extract(name: str) -> bool:
    parts = name.split("/")
    if len(parts) > 1 and parts[1] in {"include", "share"}:
        return True
    return len(parts) > 3 and parts[1] == "lib" and parts[2] == "python3.12" and parts[3] in PBS_PRUNE_STDLIB


def _prune(home: Path) -> None:
    for name in ("include", "share"):
        shutil.rmtree(home / name, ignore_errors=True)
    for entry in (home / "bin").iterdir():
        if entry.name not in {"python3", "python3.12"}:
            entry.unlink()
    # Links como libpython3.12.so -> libpython3.12.so.1.0 virariam cópias inteiras; o interpretador só carrega o SONAME.
    for link in [item for item in home.rglob("*") if item.is_symlink() and item != home / "bin" / "python3"]:
        link.unlink()
    lib = home / "lib"
    for entry in lib.iterdir():
        if entry.name.startswith(("tcl", "tk", "Tix", "itcl", "thread", "pkgconfig")) or entry.suffix == ".a":
            shutil.rmtree(entry) if entry.is_dir() else entry.unlink()
    stdlib = lib / "python3.12"
    for entry in stdlib.iterdir():
        if entry.name in PBS_PRUNE_STDLIB or entry.name.startswith("config-"):
            shutil.rmtree(entry)
    for entry in (stdlib / "lib-dynload").iterdir():
        if entry.name.startswith(("_tkinter", "_test", "_ctypes_test", "xxlimited")):
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
