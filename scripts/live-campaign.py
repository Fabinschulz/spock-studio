#!/usr/bin/env python3
"""Campanha de validação real com modelo: roda os testes ao vivo em fases, com a chave pedida uma vez num prompt oculto.

A chave nunca passa por argumento, arquivo ou conversa: ela só existe no ambiente dos processos filhos. Um gateway compatível
(com outro endereço e outros nomes de modelo) entra por ``--gateway`` e ``--anthropic-model``/``--openai-model``; isso só vale em
build de desenvolvimento (Debug), que é o que o ``dotnet test`` usa. Rode com o mesmo Python que o runtime de IA usa (o 3.14 do
projeto), porque o pontuador das evals de gestão é importado daqui.

  python scripts/live-campaign.py
  python scripts/live-campaign.py --phases evals --baseline
  python scripts/live-campaign.py --gateway https://gateway.exemplo --anthropic-model claude-opus-5-5 --openai-model gpt-6-sol

O relatório (``relatorio.md``) traz contagens, estados, duração e tokens, nunca texto de resposta nem segredo; no fim o script
confere que a chave não foi parar em nenhum arquivo da pasta de saída.
"""

from __future__ import annotations

import argparse
import getpass
import json
import os
import re
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TESTS = "runtime/tests/Mariliz.Application.Tests"
PROVIDERS = ("anthropic", "openai")
PHASES = {
    "evals": "Evals de gestão (investigação) por provedor, com retentativa só dos casos que o gateway derrubou",
    "idea": "Da ideia ao projeto (especificação, arquitetura, backlog) por provedor",
    "boot": "Reconhecimento de intenção do Boot por modelo (holdout)",
    "validation": "Modelo no cofre e assistente gerencial (OpenAI)",
    "workflow": "Fluxo de engenharia de ponta a ponta (plano, arquitetura, juiz) e instrução plantada no projeto, por provedor",
    "iac": "Desenho de infraestrutura com IA (conta e rede dos fatos valem sobre a necessidade), por provedor",
}
_SUMMARY = re.compile(r"(?:Resumo do teste|Test summary): total: (\d+); (?:falhou|failed): (\d+); (?:bem-sucedido|succeeded): (\d+); (?:ignorado|skipped): (\d+)")
# As linhas que cada teste vivo escreve com o resultado; só elas (e o veredito de cada teste) vão para a tela e para o relatório.
_RESULT_MARKS = ("[evals de gestão]", "[validação real]", "[fluxo real]", "[iac real]", "[eval de intenção ao vivo", "léxico errou")
_SHOW = (*_RESULT_MARKS, "Aprovado", "Com falha", "Ignorado", "Falha")


def parse_summary(text: str) -> dict[str, int] | None:
    """Os totais da linha de resumo do ``dotnet test`` (em português ou em inglês); a última vale."""
    found = _SUMMARY.findall(text)
    if not found:
        return None
    total, failed, passed, skipped = (int(value) for value in found[-1])
    return {"total": total, "failed": failed, "passed": passed, "skipped": skipped}


def key_format_problem(key: str) -> str | None:
    """Uma chave com espaço, quebra de linha ou caractere fora do ASCII não cabe num cabeçalho HTTP e o transporte a recusa em silêncio."""
    if not key:
        return "vazia"
    if not key.isascii() or not key.isprintable() or any(char.isspace() for char in key):
        return f"tem espaço, quebra de linha ou caractere fora do ASCII (tamanho {len(key)})"
    return None


def preflight_request(provider: str, key: str, gateway: str | None) -> tuple[str, dict[str, str]]:
    """A listagem de modelos é a chamada mais barata que prova que a chave é aceita; o mesmo cabeçalho que o transporte usa."""
    base = (gateway or ("https://api.anthropic.com" if provider == "anthropic" else "https://api.openai.com")).rstrip("/")
    if provider == "anthropic":
        return f"{base}/v1/models", {"x-api-key": key, "anthropic-version": "2023-06-01"}
    return f"{base}/v1/models", {"Authorization": f"Bearer {key}"}


def preflight(provider: str, key: str, gateway: str | None) -> bool:
    """Diz, antes de gastar uma rodada de 40 minutos, se a chave chega e é aceita. Nunca imprime a chave."""
    problem = key_format_problem(key)
    if problem:
        print(f"  {provider}: chave inválida para cabeçalho: {problem}")
        return False
    url, headers = preflight_request(provider, key, gateway)
    try:
        with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=30) as response:
            print(f"  {provider}: chave aceita (HTTP {response.status}) em {urllib.parse.urlsplit(url).netloc}")
            return True
    except urllib.error.HTTPError as ex:
        print(f"  {provider}: o servidor respondeu HTTP {ex.code}; a chave não foi aceita ou o serviço está indisponível")
    except (urllib.error.URLError, TimeoutError, OSError) as ex:
        print(f"  {provider}: sem conexão com {urllib.parse.urlsplit(url).netloc}: {type(ex).__name__}")
    return False


def leaked(folder: Path, secrets: list[str]) -> list[Path]:
    """Arquivos da pasta de saída em que algum segredo aparece."""
    wanted = [secret for secret in secrets if secret]
    return [
        file for file in folder.rglob("*")
        if file.is_file() and any(secret in file.read_text(encoding="utf-8", errors="replace") for secret in wanted)
    ]


def run_dotnet(label: str, test_filter: str, env: dict[str, str], out: Path) -> dict:
    """Roda um grupo de testes, mostra só as linhas que importam e guarda o log completo (sem segredo: o teste confere)."""
    log_path = out / f"{label}.log"
    started = time.monotonic()
    command = ["dotnet", "test", TESTS, "--nologo", "--filter", test_filter, "--logger", "console;verbosity=detailed"]
    text: list[str] = []
    with log_path.open("w", encoding="utf-8") as log:
        process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
        assert process.stdout is not None
        for line in process.stdout:
            log.write(line)
            text.append(line)
            if any(mark in line for mark in _SHOW):
                print("  " + line.rstrip())
        code = process.wait()
    joined = "".join(text)
    return {
        "label": label,
        "code": code,
        "seconds": int(time.monotonic() - started),
        "summary": parse_summary(joined),
        "lines": [line.strip() for line in text if any(mark in line for mark in _RESULT_MARKS)],
    }


def _load_scorer():
    sys.path.insert(0, str(ROOT / "intelligence" / "packages"))
    sys.path.insert(0, str(ROOT / "intelligence"))
    os.environ.setdefault("LANGSMITH_TRACING", "false")
    from evaluations import management

    return management


def evals_phase(env: dict[str, str], out: Path, retries: int, baseline: bool, providers: tuple[str, ...] = PROVIDERS) -> list[dict]:
    management = _load_scorer()
    folder = out / "evals"
    folder.mkdir(parents=True, exist_ok=True)
    results: list[dict] = []
    for provider in providers:
        name = provider.capitalize() if provider != "openai" else "OpenAI"
        print(f"\n== Evals de gestão: {provider}")
        run = run_dotnet(f"evals-{provider}", f"FullyQualifiedName~ManagementEvalLiveTests.Evals_de_gestao_com_a_{name}", {**env, "MARILIZ_LIVE_EVAL_OUT": str(folder)}, out)
        results.append(run)
        file = folder / f"management-eval-{provider}.json"
        if not file.exists():
            print(f"  sem arquivo de resultados para {provider}")
            continue
        for attempt in range(1, retries + 1):
            report = management.score_dataset(management.load_dataset(), json.loads(file.read_text(encoding="utf-8")))
            redo = [item["id"] for item in report["cases"] if item["failed"] or item["fallback"]]
            if not redo:
                break
            print(f"\n  rodada inconclusiva: refazendo {', '.join(redo)} (tentativa {attempt} de {retries})")
            retry_folder = folder / f"retry-{provider}-{attempt}"
            retry_folder.mkdir(exist_ok=True)
            retry = run_dotnet(
                f"evals-{provider}-retry{attempt}", f"FullyQualifiedName~ManagementEvalLiveTests.Evals_de_gestao_com_a_{name}",
                {**env, "MARILIZ_LIVE_EVAL_OUT": str(retry_folder), "MARILIZ_LIVE_EVAL_CASES": ",".join(redo)}, out)
            results.append(retry)
            retry_file = retry_folder / f"management-eval-{provider}.json"
            if not retry_file.exists():
                break
            fresh = json.loads(retry_file.read_text(encoding="utf-8"))
            merged, replaced = management.merge_acks(json.loads(file.read_text(encoding="utf-8")), fresh)
            file.write_text(json.dumps(merged, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            print(f"  trocados: {', '.join(replaced) or 'nenhum'}")
            if not any(management.conclusive(fresh.get(case_id)) for case_id in redo):
                # Nenhum caso melhorou: repetir não adianta (chave recusada, gateway fora do ar); o relatório mostra o que houve.
                print("  sem progresso na retentativa: parando as retentativas deste provedor")
                break
        print(f"\n  Pontuação de {provider}:")
        management.main(["score", str(file)])
        if baseline:
            print(f"\n  Linha de base de {provider}:")
            management.main(["baseline", str(file)])
    return results


def write_report(out: Path, phases: list[str], runs: list[dict], key_leaks: list[Path]) -> Path:
    lines = ["# Campanha de validação real", "", f"Fases: {', '.join(phases)}", "", "| Grupo | Resultado | Aprovados | Falhas | Ignorados | Segundos |", "| --- | --- | --- | --- | --- | --- |"]
    for run in runs:
        summary = run["summary"] or {}
        lines.append(f"| {run['label']} | {'ok' if run['code'] == 0 else 'falhou'} | {summary.get('passed', '?')} | {summary.get('failed', '?')} | {summary.get('skipped', '?')} | {run['seconds']} |")
    lines += ["", "## Linhas dos testes (sem segredo)", ""]
    for run in runs:
        lines.append(f"### {run['label']}")
        lines += [f"- {line}" for line in run["lines"]] or ["- (sem linhas)"]
        lines.append("")
    if key_leaks:
        lines += ["## ATENÇÃO", "", *[f"- segredo em {path}" for path in key_leaks]]
    report = out / "relatorio.md"
    report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return report


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", type=Path, help="pasta do relatório")
    parser.add_argument("--phases", default=",".join(PHASES), help=f"fases, separadas por vírgula: {', '.join(PHASES)}")
    parser.add_argument("--gateway", help="endereço https de um gateway compatível (só em build de desenvolvimento)")
    parser.add_argument("--anthropic-model", help="modelo da Anthropic no gateway")
    parser.add_argument("--openai-model", help="modelo da OpenAI no gateway")
    parser.add_argument("--retries", type=int, default=3, help="retentativas dos casos das evals que o gateway derrubou")
    parser.add_argument("--providers", default=",".join(PROVIDERS), help="provedores das evals de gestão, separados por vírgula")
    parser.add_argument("--chat-timeout", type=int, help="prazo de uma chamada ao modelo em segundos (10 a 600); o padrão do produto é 120")
    parser.add_argument("--baseline", action="store_true", help="grava a linha de base das evals se a rodada ficar conclusiva")
    args = parser.parse_args()

    phases = [name.strip() for name in args.phases.split(",") if name.strip()]
    unknown = [name for name in phases if name not in PHASES]
    if unknown:
        print(f"fases desconhecidas: {', '.join(unknown)}", file=sys.stderr)
        return 2

    providers = tuple(name.strip() for name in args.providers.split(",") if name.strip())
    if not providers or any(name not in PROVIDERS for name in providers):
        print(f"provedores válidos: {', '.join(PROVIDERS)}", file=sys.stderr)
        return 2
    if args.chat_timeout is not None and not 10 <= args.chat_timeout <= 600:
        print("--chat-timeout vai de 10 a 600 segundos", file=sys.stderr)
        return 2

    anthropic = os.environ.get("MARILIZ_LIVE_ANTHROPIC_KEY") or getpass.getpass("Chave da Anthropic (oculta): ").strip()
    openai = os.environ.get("MARILIZ_LIVE_OPENAI_KEY") or getpass.getpass("Chave da OpenAI (oculta; Enter usa a mesma): ").strip() or anthropic
    if not anthropic:
        print("sem chave: nada foi executado.", file=sys.stderr)
        return 2

    env = {**os.environ, "MARILIZ_LIVE_ANTHROPIC_KEY": anthropic, "MARILIZ_LIVE_OPENAI_KEY": openai, "MARILIZ_PYTHON": sys.executable}
    if args.gateway:
        env["MARILIZ_DEV_MODEL_GATEWAY"] = args.gateway
    if args.anthropic_model:
        env["MARILIZ_DEV_ANTHROPIC_MODEL"] = args.anthropic_model
    if args.openai_model:
        env["MARILIZ_DEV_OPENAI_MODEL"] = args.openai_model
    if args.chat_timeout is not None:
        # O motor e o cliente Python do runtime de IA leem o mesmo valor (o do cliente é o do motor mais uma margem).
        env["MARILIZ_MODEL_CHAT_TIMEOUT_SECONDS"] = str(args.chat_timeout)

    print("Pré-voo (a chave é testada contra o servidor antes de qualquer rodada):")
    checked = {provider: preflight(provider, key, args.gateway) for provider, key in (("anthropic", anthropic), ("openai", openai))}
    if not any(checked.values()):
        print("Nenhuma chave foi aceita: nada foi executado.", file=sys.stderr)
        return 2
    if not all(checked.values()):
        print("Atenção: só os provedores aceitos no pré-voo terão resultado útil.")

    out = args.out or Path(tempfile.mkdtemp(prefix="spock-campanha-"))
    out.mkdir(parents=True, exist_ok=True)
    print(f"Pasta de saída: {out}\nFases: {', '.join(phases)}")

    runs: list[dict] = []
    if "evals" in phases:
        runs += evals_phase(env, out, args.retries, args.baseline, providers)
    if "idea" in phases:
        print("\n== Da ideia ao projeto")
        runs.append(run_dotnet("idea", "FullyQualifiedName~IdeaLiveTests", env, out))
    if "boot" in phases:
        print("\n== Reconhecimento de intenção do Boot")
        runs.append(run_dotnet("boot", "FullyQualifiedName~BootIntentModelEvalTests", env, out))
    if "validation" in phases:
        print("\n== Modelo no cofre e assistente gerencial")
        runs.append(run_dotnet("validation", "FullyQualifiedName~LiveValidationTests", env, out))
    if "workflow" in phases:
        print("\n== Fluxo de engenharia de ponta a ponta")
        runs.append(run_dotnet("workflow", "FullyQualifiedName~EngineeringWorkflowLiveTests", env, out))
    if "iac" in phases:
        print("\n== Desenho de infraestrutura com IA")
        runs.append(run_dotnet("iac", "FullyQualifiedName~IacArchitectLiveTests", env, out))

    leaks = leaked(out, [anthropic, openai])
    report = write_report(out, phases, runs, leaks)
    print(f"\nRelatório: {report}")
    if leaks:
        print("ATENÇÃO: a chave apareceu em arquivos da pasta de saída; apague a pasta antes de compartilhar.", file=sys.stderr)
        return 1
    return 0 if all(run["code"] == 0 for run in runs) else 1


if __name__ == "__main__":
    sys.exit(main())
