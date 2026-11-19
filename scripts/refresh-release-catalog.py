#!/usr/bin/env python3
"""Gera o catálogo de versões que o motor embute (runtime/Mariliz.Application/Assets/versions/release-catalog.json).

Só fontes oficiais, cada uma com o seu formato:
  .NET    builds.dotnet.microsoft.com (releases-index.json)
  Node.js github.com/nodejs/Release (schedule.json) e nodejs.org/dist/index.json
  Python  peps.python.org/api/release-cycle.json
  Go      go.dev/dl/?mode=json
  Rust    static.rust-lang.org/dist/channel-rust-stable.toml
  Java    api.adoptium.net (Eclipse Adoptium)
  React, Angular, Next.js, Vite, TypeScript: registry.npmjs.org

Uma fonte que falha, devolve dado inválido ou regride a linha mais nova mantém a entrada anterior do catálogo, marcada
como desatualizada: o catálogo nunca piora por causa de uma consulta ruim. Atualizar o catálogo não muda projeto nenhum.

Uso:
  python scripts/refresh-release-catalog.py            # consulta as fontes e regrava o catálogo
  python scripts/refresh-release-catalog.py --check    # só valida o arquivo atual, sem rede
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Callable

ROOT = Path(__file__).resolve().parent.parent
CATALOG = ROOT / "runtime" / "Mariliz.Application" / "Assets" / "versions" / "release-catalog.json"
SCHEMA_VERSION = 1
USER_AGENT = "spock-release-catalog/1"
STATUSES = {"active", "maintenance", "eol", "preview", "vendor", "unknown"}
CHANNELS = {"lts", "sts", "current", "stable", "preview"}

# Versão padrão do C# por .NET: learn.microsoft.com/dotnet/csharp/language-reference/configure-language-version.
CSHARP = {"11.0": "C# 15", "10.0": "C# 14", "9.0": "C# 13", "8.0": "C# 12", "7.0": "C# 11", "6.0": "C# 10", "5.0": "C# 9",
          "3.1": "C# 8", "3.0": "C# 8"}


class SourceError(Exception):
    pass


def fetch(url: str, accept: str | None = None, timeout: float = 20, attempts: int = 3) -> bytes:
    headers = {"User-Agent": USER_AGENT}
    if accept:
        headers["Accept"] = accept
    last: Exception | None = None
    for attempt in range(attempts):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=timeout) as response:
                return response.read()
        except (urllib.error.URLError, TimeoutError, ConnectionError) as error:
            last = error
            if attempt + 1 < attempts:
                time.sleep(2 ** attempt)
    raise SourceError(f"{url}: {last}")


def fetch_json(url: str, accept: str | None = None) -> object:
    try:
        return json.loads(fetch(url, accept))
    except json.JSONDecodeError as error:
        raise SourceError(f"{url}: JSON inválido ({error})") from error


def day_status(today: dt.date, start: str | None, maintenance: str | None, end: str | None) -> str:
    def parse(value: str | None) -> dt.date | None:
        return dt.date.fromisoformat(value) if value else None

    start_d, maintenance_d, end_d = parse(start), parse(maintenance), parse(end)
    if start_d and today < start_d:
        return "preview"
    if end_d and today > end_d:
        return "eol"
    if maintenance_d and today >= maintenance_d:
        return "maintenance"
    return "active"


def version_key(version: str) -> tuple[int, ...]:
    return tuple(int(part) for part in re.findall(r"\d+", version))


# ---------- parsers: recebem o documento da fonte e devolvem linhas no formato do catálogo ----------

def parse_dotnet(index: object) -> list[dict]:
    phases = {"active": "active", "maintenance": "maintenance", "eol": "eol", "preview": "preview", "go-live": "preview"}
    lines = []
    for item in index["releases-index"]:  # type: ignore[index]
        cycle = item["channel-version"]
        status = phases.get(item.get("support-phase", ""), "unknown")
        lines.append({
            "cycle": cycle,
            "channel": "preview" if status == "preview" else item.get("release-type", "stable"),
            "status": status,
            "releaseDate": None,
            "eol": item.get("eol-date") or None,
            "latest": item.get("latest-release"),
            "latestDate": item.get("latest-release-date"),
            "language": CSHARP.get(cycle),
        })
    return lines


def parse_node(schedule: dict, releases: list, today: dt.date) -> list[dict]:
    newest_patch: dict[str, str] = {}
    for release in releases:
        major = release["version"].lstrip("v").split(".")[0]
        if "-" in release["version"]:
            continue
        current = newest_patch.get(major)
        if current is None or version_key(release["version"]) > version_key(current):
            newest_patch[major] = release["version"].lstrip("v")
    lines = []
    for key, item in schedule.items():
        cycle = key.lstrip("v")
        if cycle.startswith("0."):
            continue
        status = day_status(today, item.get("start"), item.get("maintenance"), item.get("end"))
        lts = item.get("lts")
        channel = "preview" if status == "preview" else "lts" if lts and today >= dt.date.fromisoformat(lts) else "current"
        lines.append({
            "cycle": cycle, "channel": channel, "status": status, "releaseDate": item.get("start"), "eol": item.get("end"),
            "latest": newest_patch.get(cycle), "latestDate": None, "language": None,
        })
    return lines


def parse_python(cycles: dict) -> list[dict]:
    statuses = {"feature": "preview", "prerelease": "preview", "bugfix": "active", "security": "maintenance", "end-of-life": "eol"}
    lines = []
    for cycle, item in cycles.items():
        if not cycle.startswith("3."):
            continue
        status = statuses.get(item.get("status", ""), "unknown")
        lines.append({
            "cycle": cycle, "channel": "preview" if status == "preview" else "stable", "status": status,
            "releaseDate": item.get("first_release"), "eol": item.get("end_of_life"), "latest": None, "latestDate": None,
            "language": None,
        })
    return lines


def parse_go(releases: list) -> list[dict]:
    # A política do Go (go.dev/doc/devel/release): cada versão é suportada até sair a segunda versão mais nova. A lista
    # oficial só traz as suportadas e as de teste; o motor trata o que é mais antigo que ela como sem suporte.
    lines: dict[str, dict] = {}
    for release in releases:
        version = release["version"].removeprefix("go")
        cycle = ".".join(re.findall(r"\d+", version)[:2])
        stable = bool(release.get("stable"))
        if cycle in lines and lines[cycle]["status"] == "active":
            continue
        lines[cycle] = {
            "cycle": cycle, "channel": "stable" if stable else "preview", "status": "active" if stable else "preview",
            "releaseDate": None, "eol": None, "latest": version if stable else None, "latestDate": None, "language": None,
        }
    return list(lines.values())


def parse_rust(manifest: str) -> list[dict]:
    match = re.search(r'^\[pkg\.rust\]\s*\nversion = "(\d+\.\d+\.\d+)', manifest, re.MULTILINE)
    if not match:
        raise SourceError("channel-rust-stable.toml sem [pkg.rust].version")
    version = match.group(1)
    date = re.search(r'^date = "([\d-]+)"', manifest, re.MULTILINE)
    return [{
        "cycle": ".".join(version.split(".")[:2]), "channel": "stable", "status": "active", "releaseDate": date.group(1) if date else None,
        "eol": None, "latest": version, "latestDate": None, "language": None,
    }]


def parse_java(info: dict) -> list[dict]:
    lts = set(info["available_lts_releases"])
    newest_lts, newest_feature, tip = info["most_recent_lts"], info["most_recent_feature_release"], info["tip_version"]
    lines = []
    for feature in sorted(set(info["available_releases"]) | {tip}):
        if feature in (newest_lts, newest_feature):
            status = "active"
        elif feature > newest_feature:
            status = "preview"
        elif feature in lts:
            status = "vendor"  # o fim do suporte de uma LTS antiga depende do fornecedor do JDK
        else:
            status = "eol"  # versão de recurso sem LTS: deixa de receber correção quando a seguinte sai
        lines.append({
            "cycle": str(feature), "channel": "preview" if status == "preview" else "lts" if feature in lts else "stable",
            "status": status, "releaseDate": None, "eol": None, "latest": None, "latestDate": None, "language": None,
        })
    return lines


def parse_npm(document: dict) -> tuple[list[dict], str | None]:
    latest = document["dist-tags"]["latest"]
    newest_major = latest.split(".")[0]
    by_major: dict[str, str] = {}
    for version in document["versions"]:
        if not re.fullmatch(r"\d+\.\d+\.\d+", version):
            continue
        major = version.split(".")[0]
        if major == "0":
            continue
        if major not in by_major or version_key(version) > version_key(by_major[major]):
            by_major[major] = version
    lines = [{
        "cycle": major, "channel": "stable", "status": "active" if major == newest_major else "unknown", "releaseDate": None,
        "eol": None, "latest": version, "latestDate": None, "language": None,
    } for major, version in by_major.items() if int(major) <= int(newest_major)]
    prereleases = [tag for name, tag in document["dist-tags"].items()
                   if name != "latest" and "-" in tag and version_key(tag.split("-")[0]) > version_key(latest)]
    return lines, max(prereleases, key=lambda tag: version_key(tag.split("-")[0]), default=None)


# ---------- fontes ----------

def npm_source(package: str) -> Callable[[dt.date], tuple[list[dict], str | None]]:
    def load(_: dt.date) -> tuple[list[dict], str | None]:
        return parse_npm(fetch_json(f"https://registry.npmjs.org/{package}", "application/vnd.npm.install-v1+json"))  # type: ignore[arg-type]
    return load


SOURCES: list[dict] = [
    {"id": "dotnet", "name": ".NET", "policy": "lifecycle", "cycle": "major.minor",
     "url": "https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json",
     "load": lambda today: (parse_dotnet(fetch_json("https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json")), None)},
    {"id": "node", "name": "Node.js", "policy": "lifecycle", "cycle": "major",
     "url": "https://github.com/nodejs/Release/blob/main/schedule.json",
     "load": lambda today: (parse_node(fetch_json("https://raw.githubusercontent.com/nodejs/Release/main/schedule.json"),  # type: ignore[arg-type]
                                       fetch_json("https://nodejs.org/dist/index.json"), today), None)},  # type: ignore[arg-type]
    {"id": "python", "name": "Python", "policy": "lifecycle", "cycle": "major.minor",
     "url": "https://peps.python.org/api/release-cycle.json",
     "load": lambda today: (parse_python(fetch_json("https://peps.python.org/api/release-cycle.json")), None)},  # type: ignore[arg-type]
    {"id": "go", "name": "Go", "policy": "supported-window", "cycle": "major.minor", "url": "https://go.dev/dl/?mode=json",
     "load": lambda today: (parse_go(fetch_json("https://go.dev/dl/?mode=json")), None)},  # type: ignore[arg-type]
    {"id": "rust", "name": "Rust", "policy": "rolling", "cycle": "major.minor",
     "url": "https://static.rust-lang.org/dist/channel-rust-stable.toml",
     "load": lambda today: (parse_rust(fetch("https://static.rust-lang.org/dist/channel-rust-stable.toml").decode()), None)},
    {"id": "java", "name": "Java (Eclipse Adoptium)", "policy": "vendor", "cycle": "major",
     "url": "https://api.adoptium.net/v3/info/available_releases",
     "load": lambda today: (parse_java(fetch_json("https://api.adoptium.net/v3/info/available_releases")), None)},  # type: ignore[arg-type]
    {"id": "react", "name": "React", "policy": "latest-major", "cycle": "major", "url": "https://registry.npmjs.org/react", "load": npm_source("react")},
    {"id": "angular", "name": "Angular", "policy": "latest-major", "cycle": "major", "url": "https://registry.npmjs.org/@angular/core",
     "load": npm_source("@angular/core")},
    {"id": "nextjs", "name": "Next.js", "policy": "latest-major", "cycle": "major", "url": "https://registry.npmjs.org/next", "load": npm_source("next")},
    {"id": "vite", "name": "Vite", "policy": "latest-major", "cycle": "major", "url": "https://registry.npmjs.org/vite", "load": npm_source("vite")},
    {"id": "typescript", "name": "TypeScript", "policy": "latest-major", "cycle": "major", "url": "https://registry.npmjs.org/typescript",
     "load": npm_source("typescript")},
]


# ---------- validação e montagem ----------

def validate_lines(technology: str, lines: list[dict]) -> list[str]:
    errors = []
    if not lines:
        errors.append(f"{technology}: nenhuma linha")
    if not any(line["status"] == "active" for line in lines):
        errors.append(f"{technology}: nenhuma linha ativa")
    for line in lines:
        if not re.fullmatch(r"\d+(\.\d+)?", line["cycle"]):
            errors.append(f"{technology}: ciclo inválido {line['cycle']!r}")
        if line["status"] not in STATUSES:
            errors.append(f"{technology} {line['cycle']}: situação inválida {line['status']!r}")
        if line["channel"] not in CHANNELS:
            errors.append(f"{technology} {line['cycle']}: canal inválido {line['channel']!r}")
        if line["eol"] and not re.fullmatch(r"\d{4}-\d{2}(-\d{2})?", line["eol"]):
            errors.append(f"{technology} {line['cycle']}: fim de suporte inválido {line['eol']!r}")
        if line["status"] == "active" and line["channel"] == "preview":
            errors.append(f"{technology} {line['cycle']}: pré-lançamento marcado como ativo")
    return errors


def newest_active(lines: list[dict]) -> tuple[int, ...]:
    return max((version_key(line["cycle"]) for line in lines if line["status"] == "active"), default=())


def build_entry(source: dict, lines: list[dict], prerelease: str | None, checked_at: str) -> dict:
    return {
        "id": source["id"], "name": source["name"], "policy": source["policy"], "cycle": source["cycle"], "source": source["url"],
        "checkedAt": checked_at, "stale": False, "error": None, "prerelease": prerelease,
        "lines": sorted(lines, key=lambda line: version_key(line["cycle"]), reverse=True),
    }


def refresh(previous: dict | None, today: dt.date, now: str, sources: list[dict] = SOURCES) -> tuple[dict, list[str]]:
    old = {entry["id"]: entry for entry in (previous or {}).get("technologies", [])}
    entries, report = [], []
    for source in sources:
        prior = old.get(source["id"])
        try:
            lines, prerelease = source["load"](today)
            errors = validate_lines(source["id"], lines)
            if prior and newest_active(lines) < newest_active(prior["lines"]):
                errors.append(f"{source['id']}: a linha ativa mais nova regrediu em relação ao catálogo anterior")
            if errors:
                raise SourceError("; ".join(errors))
            entry = build_entry(source, lines, prerelease, now)
            report.extend(diff_lines(source["name"], prior, entry))
        except (SourceError, KeyError, TypeError, ValueError) as error:
            if prior is None:
                raise SourceError(f"{source['id']}: sem catálogo anterior para manter ({error})") from error
            entry = {**prior, "stale": True, "error": str(error)[:300]}
            report.append(f"- {source['name']}: fonte indisponível ou inválida, mantida a consulta de {prior['checkedAt']} ({error})")
        entries.append(entry)
    return {"schemaVersion": SCHEMA_VERSION, "generatedAt": now, "technologies": entries}, report


def diff_lines(name: str, prior: dict | None, entry: dict) -> list[str]:
    if prior is None:
        return [f"- {name}: entrou no catálogo"]
    before = {line["cycle"]: line for line in prior["lines"]}
    changes = []
    for line in entry["lines"]:
        was = before.get(line["cycle"])
        if was is None:
            changes.append(f"- {name} {line['cycle']}: nova linha ({line['channel']}, {line['status']})")
        elif (was["status"], was["latest"], was["eol"]) != (line["status"], line["latest"], line["eol"]):
            changes.append(f"- {name} {line['cycle']}: {was['status']}/{was['latest']}/{was['eol']} -> {line['status']}/{line['latest']}/{line['eol']}")
    return changes


def check(catalog: dict) -> list[str]:
    errors = [] if catalog.get("schemaVersion") == SCHEMA_VERSION else ["schemaVersion inesperado"]
    for entry in catalog.get("technologies", []):
        errors.extend(validate_lines(entry["id"], entry["lines"]))
    return errors


def write(catalog: dict, path: Path = CATALOG) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="só valida o catálogo atual, sem rede")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[union-attr]
    previous = json.loads(CATALOG.read_text(encoding="utf-8")) if CATALOG.exists() else None
    if args.check:
        errors = check(previous) if previous else ["catálogo ausente"]
        print("\n".join(errors) or "catálogo válido")
        return 1 if errors else 0
    now = dt.datetime.now(dt.timezone.utc).replace(microsecond=0)
    catalog, report = refresh(previous, now.date(), now.isoformat().replace("+00:00", "Z"))
    write(catalog)
    print("\n".join(report) or "Nenhuma mudança nas linhas do catálogo.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
