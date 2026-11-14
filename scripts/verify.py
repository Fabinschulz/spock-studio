#!/usr/bin/env python3
"""Roda na sua máquina os mesmos gates da CI (motor, inteligência, interface e host) e resume o resultado.

A CI do repositório está sem orçamento: este script é a prova antes do push. Cada gate grava o log completo numa pasta
temporária; no terminal aparece só o resumo e, quando um gate falha, o fim do log dele.

  python scripts/verify.py                    todos os gates
  python scripts/verify.py --only ui host     só os grupos pedidos (motor, inteligencia, ui, host)
  python scripts/verify.py --fail-fast        para no primeiro gate que falhar
  python scripts/verify.py --install-hook     instala um pre-push que roda este script
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import tempfile
import time
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
HOST = ROOT / "desktop" / "host" / "src-tauri"
UI = ["pnpm", "--filter", "@mariliz/desktop-ui"]


@dataclass(frozen=True)
class Gate:
    group: str
    name: str
    command: list[str]
    cwd: Path = ROOT


GATES = [
    Gate("motor", "testes do motor .NET", ["dotnet", "test", "runtime/Mariliz.sln", "--nologo"]),
    Gate("inteligencia", "lint da inteligência (ruff)", [sys.executable, "-m", "ruff", "check", "intelligence"]),
    Gate("inteligencia", "testes da inteligência", [sys.executable, "-m", "unittest", "discover", "-s", "intelligence/tests", "-p", "test_*.py"]),
    Gate("ui", "tipos da interface", [*UI, "typecheck"]),
    Gate("ui", "lint da interface", [*UI, "lint"]),
    Gate("ui", "formatação da interface", [*UI, "format:check"]),
    Gate("ui", "testes da interface", [*UI, "test"]),
    Gate("ui", "build da interface", [*UI, "build"]),
    Gate("host", "formatação do host", ["cargo", "fmt", "--check"], HOST),
    Gate("host", "clippy do host", ["cargo", "clippy", "--locked", "--all-targets", "--all-features", "--", "-D", "warnings"], HOST),
    Gate("host", "testes do host", ["cargo", "test", "--locked"], HOST),
]
GROUPS = sorted({gate.group for gate in GATES})

# Sem o pacote da inteligência, os testes do motor que sobem o Python falham por um motivo que não é o código.
PREREQUISITES = {
    "motor": (ROOT / "intelligence" / "packages", "python scripts/package-intelligence.py dev"),
    "ui": (ROOT / "node_modules", "pnpm install --frozen-lockfile"),
}

PRE_PUSH = """#!/bin/sh
# Instalado por scripts/verify.py --install-hook. Para pular uma vez: git push --no-verify
exec python scripts/verify.py --fail-fast
"""


def resolve(command: list[str]) -> list[str] | None:
    # No Windows pnpm é um .cmd: o caminho completo evita depender de shell=True.
    found = shutil.which(command[0])
    return None if found is None else [found, *command[1:]]


def tail(path: Path, lines: int = 40) -> str:
    return "\n".join(path.read_text(encoding="utf-8", errors="replace").splitlines()[-lines:])


def run(gate: Gate, logs: Path) -> tuple[str, float, Path | None]:
    command = resolve(gate.command)
    if command is None:
        return f"faltou o programa {gate.command[0]}", 0.0, None
    log = logs / (gate.name.replace(" ", "-").replace("(", "").replace(")", "") + ".log")
    started = time.monotonic()
    with log.open("w", encoding="utf-8") as out:
        code = subprocess.call(command, cwd=gate.cwd, stdout=out, stderr=subprocess.STDOUT)
    return ("ok" if code == 0 else f"falhou (código {code})"), time.monotonic() - started, log


def install_hook() -> int:
    hooks = Path(subprocess.check_output(["git", "rev-parse", "--git-path", "hooks"], cwd=ROOT, text=True).strip())
    hook = (ROOT / hooks if not hooks.is_absolute() else hooks) / "pre-push"
    if hook.exists():
        print(f"Já existe um pre-push em {hook}; nada foi alterado.", file=sys.stderr)
        return 1
    hook.parent.mkdir(parents=True, exist_ok=True)
    hook.write_text(PRE_PUSH, encoding="utf-8", newline="\n")
    hook.chmod(0o755)
    print(f"pre-push instalado em {hook}.")
    return 0


def main() -> int:
    # Num pipe do Windows o padrão seria cp1252 e os acentos sairiam trocados.
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--only", nargs="+", choices=GROUPS, help="grupos a rodar")
    parser.add_argument("--fail-fast", action="store_true", help="para no primeiro gate que falhar")
    parser.add_argument("--install-hook", action="store_true", help="instala um pre-push que roda este script")
    args = parser.parse_args()

    if args.install_hook:
        return install_hook()

    selected = [gate for gate in GATES if args.only is None or gate.group in args.only]
    for group in sorted({gate.group for gate in selected}):
        if group in PREREQUISITES and not PREREQUISITES[group][0].exists():
            path, fix = PREREQUISITES[group]
            print(f"Falta {path.relative_to(ROOT)} para o grupo {group}. Rode antes: {fix}", file=sys.stderr)
            return 2

    logs = Path(tempfile.mkdtemp(prefix="spock-verify-"))
    results: list[tuple[Gate, str, float, Path | None]] = []
    for gate in selected:
        print(f"... {gate.name}", flush=True)
        status, seconds, log = run(gate, logs)
        results.append((gate, status, seconds, log))
        if status != "ok" and args.fail_fast:
            break

    print()
    width = max(len(gate.name) for gate, *_ in results)
    for gate, status, seconds, _ in results:
        print(f"{gate.name.ljust(width)}  {status}  ({seconds:.0f} s)")
    skipped = len(selected) - len(results)
    if skipped:
        print(f"{skipped} gate(s) não rodaram por causa do --fail-fast.")

    failed = [(gate, log) for gate, status, _, log in results if status != "ok"]
    for gate, log in failed:
        if log is not None:
            print(f"\n--- fim do log de {gate.name} ({log}) ---\n{tail(log)}")
    print(f"\nLogs em {logs}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
