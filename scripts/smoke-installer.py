#!/usr/bin/env python3
"""Abre o instalador que o Tauri acabou de gerar e acha, dentro dele, os recursos de IA (interpretador e inteligência).

O smoke do pacote (``PackagedAiSmokeTests``) roda sobre uma pasta de recursos. Apontá-lo para a pasta que a CI montou antes do
Tauri prova o script de empacotamento, não o instalador: o Tauri pode copiar os recursos de outro jeito (links simbólicos,
permissão de execução, arquivos omitidos). Por isso esta etapa extrai o instalador de verdade e entrega a pasta que o app
instalado usaria.

  smoke-installer.py --bundle <pasta bundle do Tauri> --out <pasta vazia>

  Linux    extrai o .deb com ``dpkg-deb -x``
  macOS    usa Contents/Resources do .app (o .dmg só embrulha o .app)
  Windows  roda o instalador NSIS em modo silencioso (/S) numa pasta própria; a instalação é por usuário e não toca no registro
           além da entrada de desinstalação

Escreve ``MARILIZ_PACKAGED_RESOURCES=<pasta>`` em ``$GITHUB_ENV`` (quando existe) e imprime a pasta.
"""

from __future__ import annotations

import argparse
import os
import platform
import subprocess
import sys
from pathlib import Path

SCRIPT = Path("intelligence") / "server.py"


def only(matches: list[Path], what: str, where: Path) -> Path:
    if len(matches) != 1:
        found = ", ".join(sorted(path.name for path in matches)) or "nenhum"
        raise SystemExit(f"Esperava exatamente um {what} em {where}; encontrei: {found}.")
    return matches[0]


def extract(bundle: Path, out: Path) -> Path:
    system = platform.system()
    if system == "Darwin":
        app = only(sorted(bundle.glob("macos/*.app")), ".app", bundle / "macos")
        return app / "Contents" / "Resources"

    if out.exists() and any(out.iterdir()):
        raise SystemExit(f"{out} precisa estar vazia.")
    out.mkdir(parents=True, exist_ok=True)

    if system == "Linux":
        deb = only(sorted(bundle.glob("deb/*.deb")), ".deb", bundle / "deb")
        subprocess.run(["dpkg-deb", "-x", str(deb), str(out)], check=True)
        return out
    if system == "Windows":
        setup = only(sorted(bundle.glob("nsis/*-setup.exe")), "instalador NSIS", bundle / "nsis")
        if " " in str(out):
            raise SystemExit("O NSIS não aceita espaço em /D=: use uma pasta sem espaços.")
        # /D tem de ser o último argumento e não pode ter aspas.
        subprocess.run([str(setup), "/S", f"/D={out}"], check=True, timeout=600)
        return out
    raise SystemExit(f"Sistema sem smoke de instalador: {system}.")


def locate(root: Path) -> Path:
    """A pasta que tem ``intelligence/server.py`` e ``python``, onde quer que o instalador a tenha posto."""
    candidates = sorted({path.parent.parent for path in root.rglob("server.py") if path.parent.name == "intelligence"})
    candidates = [path for path in candidates if (path / SCRIPT).is_file() and (path / "python").is_dir()]
    if len(candidates) != 1:
        listing = "\n".join(f"  {path}" for path in candidates) or "  nenhuma"
        raise SystemExit(f"Esperava uma pasta de recursos de IA dentro do instalador em {root}; encontrei:\n{listing}")
    return candidates[0]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--bundle", required=True, type=Path)
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()

    if not args.bundle.is_dir():
        raise SystemExit(f"Sem a pasta do instalador: {args.bundle}.")
    root = locate(extract(args.bundle.resolve(), args.out.resolve()))
    print(root)
    env_file = os.environ.get("GITHUB_ENV")
    if env_file:
        with open(env_file, "a", encoding="utf-8") as handle:
            handle.write(f"MARILIZ_PACKAGED_RESOURCES={root}\n")


if __name__ == "__main__":
    try:
        main()
    except subprocess.CalledProcessError as error:
        print(f"Falha ao abrir o instalador: {error}", file=sys.stderr)
        raise SystemExit(1) from error
    except subprocess.TimeoutExpired as error:
        print(f"O instalador não terminou a tempo: {error}", file=sys.stderr)
        raise SystemExit(1) from error
