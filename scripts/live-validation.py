#!/usr/bin/env python3
"""Roda a validação real (modelo, GitHub e Jira de verdade) com as credenciais definidas no seu terminal.

As credenciais nunca passam por argumento, arquivo ou conversa: o script só lê quais variáveis existem e repassa o
ambiente ao ``dotnet test``. Grupo sem as variáveis aparece como ignorado. O relatório traz contagens e estados, nunca
segredos, e fica em --out (padrão: uma pasta temporária nova).

  python scripts/live-validation.py
  python scripts/live-validation.py --out C:/validacao
"""

from __future__ import annotations

import argparse
import os
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TESTS = "runtime/tests/Mariliz.Application.Tests"

GROUPS = [
    ("GitHub", ["MARILIZ_LIVE_GITHUB_TOKEN", "MARILIZ_LIVE_GITHUB_REPO"]),
    ("Jira", ["MARILIZ_LIVE_JIRA_SITE", "MARILIZ_LIVE_JIRA_EMAIL", "MARILIZ_LIVE_JIRA_TOKEN", "MARILIZ_LIVE_JIRA_PROJECT"]),
    ("OpenAI (assistente, evals de gestão e fase A)", ["MARILIZ_LIVE_OPENAI_KEY"]),
    ("Anthropic (evals de gestão e fase A)", ["MARILIZ_LIVE_ANTHROPIC_KEY"]),
    ("Google Gemini (fase A)", ["MARILIZ_LIVE_GOOGLE_KEY"]),
]
FILTER = "FullyQualifiedName~LiveValidationTests|FullyQualifiedName~ManagementEvalLiveTests|FullyQualifiedName~IdeaLiveTests"


def main() -> int:
    # Num pipe do Windows o padrão seria cp1252 e os acentos sairiam trocados.
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", type=Path, help="pasta do relatório")
    args = parser.parse_args()

    ready = []
    for name, variables in GROUPS:
        missing = [variable for variable in variables if not os.environ.get(variable, "").strip()]
        print(f"{name}: {'vai rodar' if not missing else 'ignorado, falta ' + ', '.join(missing)}")
        if not missing:
            ready.append(name)
    if not ready:
        print("\nNenhuma credencial definida. Defina as variáveis no seu terminal (nunca no chat) e rode de novo.")
        return 2

    out = args.out or Path(tempfile.mkdtemp(prefix="spock-validacao-"))
    out.mkdir(parents=True, exist_ok=True)
    env = {**os.environ, "MARILIZ_LIVE_EVAL_OUT": str(out)}
    report = out / "relatorio.txt"
    command = ["dotnet", "test", TESTS, "--nologo", "--filter", FILTER, "--logger", "console;verbosity=detailed"]
    with report.open("w", encoding="utf-8") as log:
        process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
        assert process.stdout is not None
        for line in process.stdout:
            log.write(line)
            if "[validação real]" in line or "[evals de gestão]" in line or "Ignorado" in line or "Falha" in line or "Aprovado" in line:
                print(line.rstrip())
        code = process.wait()

    for result in sorted(out.glob("management-eval-*.json")):
        print(f"\nPontuação de {result.name}:")
        code |= subprocess.call([sys.executable, "intelligence/evaluations/management.py", "score", str(result)], cwd=ROOT)

    secrets = [os.environ[variable] for _, variables in GROUPS for variable in variables
               if variable.endswith(("_TOKEN", "_KEY")) and os.environ.get(variable)]
    for file in out.rglob("*"):
        if file.is_file() and any(secret in file.read_text(encoding="utf-8", errors="replace") for secret in secrets):
            print(f"\nATENÇÃO: um segredo apareceu em {file}. Apague a pasta {out} antes de compartilhar.", file=sys.stderr)
            return 1

    print(f"\nRelatório completo em {report} (sem segredos). Resultado: {'passou' if code == 0 else 'falhou'}.")
    evals = sorted(out.glob("management-eval-*.json"))
    if evals:
        # Gravar é decisão de quem leu o resultado: uma rodada ruim não pode virar a régua sem querer.
        print("\nSe a rodada for aceita como linha de base das evals de gestão:")
        for result in evals:
            print(f"  python intelligence/evaluations/management.py baseline {result}")
    return 0 if code == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
