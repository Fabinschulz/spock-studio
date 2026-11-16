#!/usr/bin/env python3
"""Inventário de componentes de terceiros e SBOMs (SPDX 2.3 e CycloneDX 1.6) do Spock Studio.

Lê apenas o que já existe na máquina: lockfiles do repositório, metadados que os gerenciadores de pacotes deixam no cache
(node_modules, ~/.cargo, ~/.nuget, intelligence/packages) e os recursos do último build local. Não instala nada e não
consulta serviços de análise. Os componentes fora de gerenciador (binários embarcados, ferramentas baixadas sob demanda,
serviços externos) vêm da tabela MANUAL abaixo, mantida à mão a partir da auditoria (docs/compliance/LICENSE_AUDIT.md).

Pré-requisitos para rodar: pnpm install feito, `dotnet restore runtime/Mariliz.sln`, `python scripts/package-intelligence.py dev`
e o cache do Cargo com os crates de Windows e Linux (um `cargo metadata --locked --filter-platform <alvo>` com rede baixa).

  python docs/compliance/tools/generate_sbom.py            gera docs/compliance/SBOM/* e docs/compliance/data/*
  python docs/compliance/tools/generate_sbom.py --check    só confere se o que está versionado ainda bate com os lockfiles
"""

from __future__ import annotations

import argparse
import base64
import csv
import hashlib
import json
import os
import re
import subprocess
import sys
import tomllib
import uuid
from collections import Counter, defaultdict, deque
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "docs" / "compliance"
TOOL = ("spock-compliance-generator", "1.0.0")
PRODUCT = ("Spock Studio", "0.1.0")
NUGET = Path(os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget" / "packages"))
WIN, LINUX = "x86_64-pc-windows-msvc", "x86_64-unknown-linux-gnu"

# ---------------------------------------------------------------------------------------------------------------------
# Licenças: normalização, ids SPDX e detecção pelo texto do arquivo distribuído no pacote.

PERMISSIVE = {
    "MIT", "MIT-0", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "ISC", "0BSD", "Zlib", "Unlicense", "CC0-1.0",
    "Unicode-3.0", "Unicode-DFS-2016", "BSL-1.0", "PSF-2.0", "Python-2.0", "BlueOak-1.0.0", "CC-BY-4.0", "X11",
    "curl", "OpenSSL", "bzip2-1.0.6", "NCSA", "Apache-2.0 WITH LLVM-exception", "LLVM-exception",
}
WEAK_COPYLEFT = {"MPL-2.0", "LGPL-2.1-only", "LGPL-2.1-or-later", "LGPL-3.0-only", "LGPL-3.0-or-later", "EPL-2.0", "CPL-1.0", "CDDL-1.0"}
STRONG_COPYLEFT = {"GPL-2.0-only", "GPL-2.0-or-later", "GPL-3.0-only", "GPL-3.0-or-later", "AGPL-3.0-only", "AGPL-3.0-or-later"}
SOURCE_AVAILABLE = {"BUSL-1.1", "SSPL-1.0", "Elastic-2.0"}
# Ordem de preferência ao escolher um ramo de expressão "A OR B" (a obrigação mais simples primeiro).
PREFERENCE = ["MIT", "MIT-0", "0BSD", "Unlicense", "CC0-1.0", "ISC", "BSD-2-Clause", "Zlib", "BSD-3-Clause", "Apache-2.0",
              "BSL-1.0", "Unicode-3.0", "PSF-2.0", "MPL-2.0"]

DETECTORS = [
    ("Apache-2.0", re.compile(r"Apache License,?\s+Version 2\.0|Licensed under the Apache License, Version 2\.0", re.I)),
    ("MPL-2.0", re.compile(r"Mozilla Public License,?\s+(Version|v\.?)\s*2\.0", re.I)),
    ("LGPL", re.compile(r"GNU LESSER GENERAL PUBLIC LICENSE|GNU Library General Public", re.I)),
    ("AGPL", re.compile(r"GNU AFFERO GENERAL PUBLIC LICENSE", re.I)),
    ("GPL", re.compile(r"(?<!LESSER )(?<!AFFERO )GNU GENERAL PUBLIC LICENSE", re.I)),
    ("BUSL-1.1", re.compile(r"Business Source License 1\.1", re.I)),
    ("Unicode-3.0", re.compile(r"UNICODE LICENSE V3|UNICODE, INC\. LICENSE AGREEMENT", re.I)),
    ("Unlicense", re.compile(r"This is free and unencumbered software released into the public domain", re.I)),
    ("CC0-1.0", re.compile(r"CC0 1\.0 Universal|Creative Commons Zero", re.I)),
    ("BSL-1.0", re.compile(r"Boost Software License", re.I)),
    ("PSF-2.0", re.compile(r"PYTHON SOFTWARE FOUNDATION LICENSE", re.I)),
    ("Zlib", re.compile(r"This software is provided ['‘]as-is['’], without any express or implied\s+warranty", re.I)),
    ("BlueOak-1.0.0", re.compile(r"Blue Oak Model License", re.I)),
    ("CC-BY-4.0", re.compile(r"Attribution 4\.0 International", re.I)),
    ("MIT", re.compile(r"Permission is hereby granted, free of charge, to any person obtaining", re.I)),
    ("ISC/0BSD", re.compile(r"Permission to use, copy, modify, and/?or distribute this software for any\s+purpose", re.I)),
    ("BSD", re.compile(r"Redistribution and use in source and binary forms", re.I)),
]

_spdx_ids: set[str] | None = None


def spdx_ids() -> set[str]:
    global _spdx_ids
    if _spdx_ids is None:
        schema = OUT / "tools" / "spdx-license-ids.json"
        _spdx_ids = set(json.loads(schema.read_text(encoding="utf-8"))) if schema.exists() else set()
    return _spdx_ids


def normalize(expr: str | None) -> str | None:
    """Expressão SPDX a partir do que o gerenciador declara (Cargo antigo usa '/', npm às vezes usa parênteses)."""
    if not expr:
        return None
    e = expr.strip()
    if re.fullmatch(r"\(?\s*[A-Za-z0-9.+-]+(\s*/\s*[A-Za-z0-9.+-]+)+\s*\)?", e):
        e = " OR ".join(part.strip() for part in e.strip("()").split("/"))
    e = re.sub(r"\s+", " ", e)
    # Textos livres de metadados PyPI, convertidos conforme o arquivo de licença oficial (o valor original fica no inventário).
    aliases = {"Apache 2.0": "Apache-2.0", "Apache2": "Apache-2.0", "BSD": "NOASSERTION", "MIT License": "MIT", "PSF": "PSF-2.0",
               "Apache License, Version 2.0": "Apache-2.0", "Modified BSD License": "BSD-3-Clause",
               "MIT License, Apache License, Version 2.0": "MIT OR Apache-2.0"}
    return aliases.get(e, e)


def tokens(expr: str) -> list[str]:
    return [t for t in re.split(r"[\s()]+", expr) if t and t.upper() not in {"AND", "OR", "WITH"}]


def valid_expression(expr: str | None) -> bool:
    if not expr or expr in {"NOASSERTION", "NONE"}:
        return False
    ids = spdx_ids()
    return all(t in ids or t.startswith("LicenseRef-") or t == "LLVM-exception" for t in tokens(expr)) if ids else True


def effective(expr: str | None) -> list[str]:
    """Licenças cujas obrigações valem: escolhe o ramo mais simples de cada OR e mantém todos os de AND."""
    if not expr or not valid_expression(expr):
        return []
    e = expr.replace("(", " ( ").replace(")", " ) ")
    # Expressões do inventário são rasas: "A OR B", "A AND B", "(A OR B) AND C". Resolve parênteses primeiro.
    while "(" in e:
        e = re.sub(r"\(\s*([^()]+?)\s*\)", lambda m: " " + pick(m.group(1)) + " ", e)
    return [x for part in re.split(r"\s+AND\s+", e.strip()) for x in [pick(part)] if x]


def pick(alternatives: str) -> str:
    options = [o.strip() for o in re.split(r"\s+OR\s+", alternatives.strip()) if o.strip()]
    if " AND " in alternatives:
        return " AND ".join(effective(alternatives))
    return sorted(options, key=lambda o: PREFERENCE.index(o) if o in PREFERENCE else 99)[0] if options else ""


def detect_text(raw: str) -> set[str]:
    text = re.sub(r"\s+", " ", raw)
    found: set[str] = set()
    for name, rx in DETECTORS:
        if rx.search(text):
            if name == "BSD":
                found.add("BSD-3-Clause" if re.search(r"Neither the name|names of (its|the) contributors|may not be used to endorse", text, re.I) else "BSD-2-Clause")
            elif name == "ISC/0BSD":
                found.add("ISC" if re.search(r"provided that the above\s+copyright notice", text, re.I) else "0BSD")
            elif name == "GPL" and "GPL" in found:
                continue
            else:
                found.add(name)
    # Textos que citam outras licenças sem adotá-las: a MPL-2.0 lista GPL/LGPL/AGPL como "Secondary Licenses"; o arquivo da
    # PSF traz a tabela "GPL-compatible" e a 0BSD da documentação; a Unicode V3 usa o mesmo parágrafo de permissão do MIT.
    if "MPL-2.0" in found:
        found -= {"GPL", "LGPL", "AGPL"}
    if "PSF-2.0" in found:
        found -= {"GPL", "ISC", "0BSD"}
    if "Unicode-3.0" in found and not re.search(r"MIT License|to deal in the Software without restriction", text):
        found.discard("MIT")
    return found


LICENSE_FILE = re.compile(r"^(LICEN[CS]E|COPYING|COPYRIGHT|UNLICENSE|NOTICE)([-_.].*)?$", re.I)


def license_files(directory: Path, recursive: bool = False) -> list[Path]:
    if not directory.is_dir():
        return []
    entries = directory.rglob("*") if recursive else directory.iterdir()
    return sorted(p for p in entries if p.is_file() and LICENSE_FILE.match(p.name) and p.stat().st_size < 400_000)


def check(declared: str | None, files: list[Path]) -> tuple[str, list[str], bool]:
    """Compara a licença declarada com o texto dos arquivos de licença distribuídos no pacote."""
    detected: set[str] = set()
    notice = False
    for f in files:
        if f.name.upper().startswith("NOTICE"):
            notice = True
            continue
        try:
            detected |= detect_text(f.read_text(encoding="utf-8", errors="replace"))
        except OSError:
            pass
    if not files:
        return "SEM ARQUIVO", [], notice
    if not detected:
        return "TEXTO NÃO RECONHECIDO", [], notice
    declared_ids = set(tokens(declared)) if declared else set()

    def covered(d: str) -> bool:
        if d in declared_ids:
            return True
        if d in {"LGPL", "GPL", "AGPL"}:
            return any(t.startswith(d) for t in declared_ids)
        # MIT-0 usa o mesmo parágrafo de permissão do MIT; fora isso, licença no arquivo que não está na expressão é divergência.
        return d == "MIT" and "MIT-0" in declared_ids

    extra = sorted(d for d in detected if not covered(d))
    return ("CONFERE" if not extra else "DIVERGE"), sorted(detected), notice


# ---------------------------------------------------------------------------------------------------------------------
# Modelo do inventário.

@dataclass
class Component:
    key: str
    name: str
    version: str
    ecosystem: str
    ctype: str  # library | framework | application | runtime | tool | data | asset | service | installer
    purl: str | None = None
    origin: str | None = None
    usage: str = ""
    direct: bool = False
    environment: str = "execução"  # execução | desenvolvimento | build | instalação sob demanda | externo
    distribution: str = "não distribuído"
    artifacts: set[str] = field(default_factory=set)
    declared: str | None = None
    detected: list[str] = field(default_factory=list)
    status: str = "N/A"
    notice_file: bool = False
    risk: str | None = None
    risk_reason: str = ""
    obligations: str = ""
    evidence: list[str] = field(default_factory=list)
    hashes: list[tuple[str, str]] = field(default_factory=list)
    depends_on: set[str] = field(default_factory=set)
    parent: str | None = None
    note: str = ""

    @property
    def spdx_declared(self) -> str:
        n = normalize(self.declared)
        if n in {"NOASSERTION", "NONE"}:
            return n
        return n if valid_expression(n) else ("NOASSERTION" if not n else "LicenseRef-" + re.sub(r"[^A-Za-z0-9.-]+", "-", n).strip("-"))


def b64hex(value: str) -> str:
    return base64.b64decode(value).hex()


def run(cmd: list[str], cwd: Path = ROOT) -> str:
    shell = os.name == "nt" and cmd[0] in {"pnpm", "npx"}
    return subprocess.run(cmd, cwd=cwd, check=True, capture_output=True, shell=shell).stdout.decode("utf-8")


def rel(path: Path | str) -> str:
    p = Path(path)
    try:
        return p.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        home = Path.home()
        try:
            return "~/" + p.resolve().relative_to(home).as_posix()
        except ValueError:
            return p.as_posix()


# ---------------------------------------------------------------------------------------------------------------------
# Coletores por ecossistema.

NPM_USAGE = {
    "react": "Biblioteca de interface", "react-dom": "Renderização da interface no WebView", "react-router": "Rotas da interface",
    "@mui/material": "Componentes visuais (base do ui-kit)", "@emotion/react": "CSS-in-JS do MUI", "@emotion/styled": "CSS-in-JS do MUI",
    "@mariliz/ui-kit": "Biblioteca de componentes própria (publicada no npm)", "@tauri-apps/api": "Ponte da interface com o host Tauri",
    "@xterm/xterm": "Terminal embutido", "@xterm/addon-fit": "Ajuste do terminal ao painel", "react-hook-form": "Formulários",
    "@hookform/resolvers": "Validação de formulários com zod", "zod": "Validação de esquemas e contratos", "clsx": "Classes CSS condicionais",
    "@tauri-apps/cli": "CLI de build e empacotamento do Tauri (gera o instalador)", "vite": "Build da interface", "vitest": "Testes da interface",
    "typescript": "Compilador TypeScript", "eslint": "Lint", "prettier": "Formatação", "sass-embedded": "Compilação de SCSS (inclui binário Dart Sass)",
    "jsdom": "DOM para testes", "axe-core": "Testes de acessibilidade",
}
CARGO_USAGE = {
    "tauri": "Framework do host desktop (janela, IPC, WebView)", "tauri-build": "Script de build do Tauri", "portable-pty": "Pseudo-terminal do terminal embutido",
    "serde": "Serialização", "serde_json": "JSON", "tauri-plugin-dialog": "Diálogos nativos", "tauri-plugin-shell": "Processo do sidecar (motor .NET)",
    "tauri-plugin-single-instance": "Instância única do app", "tokio": "Runtime assíncrono",
}
NUGET_USAGE = {
    "YamlDotNet": "Leitura de mariliz.yaml e frontmatter", "Microsoft.ML.Tokenizers": "Contagem de tokens para teto por finalidade",
    "Microsoft.ML.Tokenizers.Data.O200kBase": "Vocabulário o200k_base (dados do tokenizer)",
    "System.Security.Cryptography.ProtectedData": "DPAPI: cofre de credenciais no Windows", "Azure.Identity": "Credenciais do Entra ID (conector Azure)",
    "Azure.ResourceManager": "Leitura de recursos Azure", "Google.Apis.Auth": "Application Default Credentials (conector GCP)",
    "coverlet.collector": "Cobertura de testes", "JsonSchema.Net": "Validação de contratos nos testes", "Microsoft.NET.Test.Sdk": "Execução de testes",
    "xunit": "Framework de testes", "xunit.runner.visualstudio": "Runner de testes",
}
PY_USAGE = {"langgraph": "Orquestração dos agentes e workflows (grafo)", "langgraph-checkpoint-sqlite": "Checkpoints dos grafos em SQLite"}


class Inventory:
    def __init__(self) -> None:
        self.items: dict[str, Component] = {}
        self.roots: dict[str, list[str]] = defaultdict(list)  # artefato -> chaves diretas

    def add(self, c: Component) -> Component:
        existing = self.items.get(c.key)
        if existing:
            existing.artifacts |= c.artifacts
            existing.depends_on |= c.depends_on
            existing.direct = existing.direct or c.direct
            if c.distribution != "não distribuído" and existing.distribution == "não distribuído":
                existing.distribution, existing.environment = c.distribution, c.environment
            return existing
        self.items[c.key] = c
        return c


def collect_npm(inv: Inventory) -> None:
    prod = json.loads(run(["pnpm", "licenses", "list", "--prod", "--json"]))
    every = json.loads(run(["pnpm", "licenses", "list", "--json"]))
    integrity: dict[str, str] = {}
    current = None
    for line in (ROOT / "pnpm-lock.yaml").read_text(encoding="utf-8").splitlines():
        m = re.match(r"^  '?(@?[^@\s']+)@([^'\s(:]+)'?:\s*$", line)
        if m:
            current = f"{m.group(1)}@{m.group(2)}"
            continue
        m = re.search(r"resolution: \{integrity: (sha(\d+))-([A-Za-z0-9+/=]+)", line)
        if m and current and current not in integrity:
            integrity[current] = f"SHA-{m.group(2)}:{b64hex(m.group(3))}"
    prod_keys = {f"{p['name']}@{v}" for group in prod.values() for p in group for v in p["versions"]}
    ui = json.loads((ROOT / "desktop/ui/package.json").read_text(encoding="utf-8"))
    host = json.loads((ROOT / "desktop/host/package.json").read_text(encoding="utf-8"))
    direct_prod = set(ui.get("dependencies", {}))
    direct_dev = set(ui.get("devDependencies", {})) | set(host.get("devDependencies", {}))

    for declared, group in every.items():
        for p in group:
            for version, path in zip(p["versions"], p["paths"]):
                key = f"npm:{p['name']}@{version}"
                is_prod = f"{p['name']}@{version}" in prod_keys
                status, detected, notice = check(normalize(declared), license_files(Path(path)))
                c = Component(
                    key, p["name"], version, "npm", "library", purl=f"pkg:npm/{p['name'].replace('@', '%40')}@{version}",
                    origin=p.get("homepage"), usage=NPM_USAGE.get(p["name"], ""),
                    direct=p["name"] in (direct_prod if is_prod else direct_dev),
                    environment="execução" if is_prod else "desenvolvimento",
                    distribution="incorporado (bundle da interface)" if is_prod else "não distribuído",
                    artifacts={"windows-x64", "linux-x64"} if is_prod else {"desenvolvimento"},
                    declared=declared, detected=detected, status=status, notice_file=notice,
                    evidence=["pnpm-lock.yaml", rel(path)],
                )
                if f"{p['name']}@{version}" in integrity:
                    alg, value = integrity[f"{p['name']}@{version}"].split(":")
                    c.hashes.append((alg, value))
                inv.add(c)

    # Grafo e pai de cada transitiva, a partir das árvores do pnpm.
    def walk(deps: dict, parent: str | None, root: str, seen: set) -> None:
        for name, info in (deps or {}).items():
            key = f"npm:{name}@{info.get('version')}"
            if key not in inv.items:
                continue
            if parent:
                inv.items[parent].depends_on.add(key)
                inv.items[key].parent = inv.items[key].parent or inv.items[parent].name
            else:
                inv.roots[root].append(key)
            if key in seen:
                continue
            seen.add(key)
            walk(info.get("dependencies"), key, root, seen)

    for project in json.loads(run(["pnpm", "-r", "list", "--depth", "Infinity", "--json"])):
        walk(project.get("dependencies"), None, "ui" if project["name"].endswith("desktop-ui") else "host", set())
        walk(project.get("devDependencies"), None, "dev", set())
    # Direta é a versão resolvida pelo package.json, não toda versão com o mesmo nome no lock.
    roots = {k for r in ("ui", "host", "dev") for k in inv.roots[r]}
    for c in inv.items.values():
        if c.ecosystem == "npm":
            c.direct = c.key in roots


def collect_cargo(inv: Inventory) -> None:
    lock = tomllib.loads((ROOT / "desktop/host/src-tauri/Cargo.lock").read_text(encoding="utf-8"))
    checksums = {(p["name"], p["version"]): p.get("checksum") for p in lock["package"]}
    cwd = ROOT / "desktop/host/src-tauri"
    for triple, artifact in ((WIN, "windows-x64"), (LINUX, "linux-x64")):
        meta = json.loads(run(["cargo", "metadata", "--locked", "--offline", "--format-version", "1", "--filter-platform", triple], cwd))
        packages = {p["id"]: p for p in meta["packages"]}
        nodes = {n["id"]: n for n in meta["resolve"]["nodes"]}
        root = meta["resolve"]["root"]
        # Distribuído = alcançável por dependências normais, sem passar por proc-macro (código que só roda no compilador).
        runtime_ids, build_ids = set(), set()
        queue = deque([(root, True)])
        visited = set()
        while queue:
            pid, normal = queue.popleft()
            if (pid, normal) in visited:
                continue
            visited.add((pid, normal))
            pkg = packages[pid]
            proc_macro = any("proc-macro" in t["kind"] for t in pkg["targets"])
            (runtime_ids if normal and not proc_macro else build_ids).add(pid)
            for dep in nodes[pid]["deps"]:
                kinds = {k["kind"] for k in dep["dep_kinds"]}
                if kinds <= {"dev"}:
                    continue
                queue.append((dep["pkg"], normal and not proc_macro and (None in kinds or "normal" in kinds)))
        for pid in runtime_ids | build_ids:
            if pid == root:
                continue
            pkg = packages[pid]
            shipped = pid in runtime_ids
            directory = Path(pkg["manifest_path"]).parent
            status, detected, notice = check(normalize(pkg.get("license")), license_files(directory))
            key = f"cargo:{pkg['name']}@{pkg['version']}"
            c = Component(
                key, pkg["name"], pkg["version"], "cargo", "library", purl=f"pkg:cargo/{pkg['name']}@{pkg['version']}",
                origin=pkg.get("repository") or pkg.get("homepage"), usage=CARGO_USAGE.get(pkg["name"], ""),
                direct=pkg["name"] in CARGO_USAGE,
                environment="execução" if shipped else "build",
                distribution="incorporado (binário do host, link estático)" if shipped else "não distribuído (build/proc-macro)",
                artifacts={artifact} if shipped else {"desenvolvimento"},
                declared=pkg.get("license") or (f"LicenseRef-file:{pkg['license_file']}" if pkg.get("license_file") else None),
                detected=detected, status=status, notice_file=notice,
                evidence=["desktop/host/src-tauri/Cargo.lock", rel(directory)],
            )
            if checksums.get((pkg["name"], pkg["version"])):
                c.hashes.append(("SHA-256", checksums[(pkg["name"], pkg["version"])]))
            c = inv.add(c)
            for dep in nodes[pid]["deps"]:
                if {k["kind"] for k in dep["dep_kinds"]} <= {"dev"}:
                    continue
                d = packages[dep["pkg"]]
                c.depends_on.add(f"cargo:{d['name']}@{d['version']}")
        for dep in nodes[root]["deps"]:
            d = packages[dep["pkg"]]
            inv.roots["host"].append(f"cargo:{d['name']}@{d['version']}")
    # Ordem fixa: o pai mostrado de um transitivo não pode mudar a cada execução.
    for c in sorted(inv.items.values(), key=lambda item: item.key):
        for child in sorted(c.depends_on):
            if child in inv.items and inv.items[child].parent is None and child.startswith("cargo:"):
                inv.items[child].parent = c.name


def collect_nuget(inv: Inventory) -> None:
    direct: set[str] = set()
    test_direct: set[str] = set()
    for csproj in sorted((ROOT / "runtime").rglob("*.csproj")):
        refs = set(re.findall(r'<PackageReference Include="([^"]+)"', csproj.read_text(encoding="utf-8-sig")))
        (test_direct if "tests" in csproj.parts else direct).update(refs)
    assets = [(ROOT / "runtime/Mariliz.Runtime/obj/project.assets.json", True)] + [
        (p, False) for p in sorted((ROOT / "runtime/tests").glob("*/obj/project.assets.json"))]
    for path, runtime in assets:
        data = json.loads(path.read_text(encoding="utf-8"))
        target = next(iter(data["targets"].values()))
        for lib_key, lib in data["libraries"].items():
            if lib["type"] != "package":
                continue
            name, version = lib_key.split("/")
            key = f"nuget:{name}@{version}"
            if not runtime and key in inv.items:
                continue
            folder = NUGET / name.lower() / version.lower()
            nuspec = next(folder.glob("*.nuspec"), None)
            declared, origin = None, None
            if nuspec:
                text = nuspec.read_text(encoding="utf-8-sig")
                m = re.search(r'<license type="expression">([^<]+)</license>', text)
                declared = m.group(1) if m else None
                m = re.search(r'<license type="file">([^<]+)</license>', text)
                if m and not declared and "OSMF" in m.group(1).upper():
                    # Open Source Maintenance Fee: fonte MIT, mas o binário publicado vem com EULA de taxa para uso com receita.
                    declared = "MIT AND LicenseRef-OSMF-EULA"
                elif m and not declared:
                    found = detect_text((folder / m.group(1)).read_text(encoding="utf-8", errors="replace")) if (folder / m.group(1)).is_file() else set()
                    declared = " AND ".join(sorted(found)) or f"LicenseRef-file-{m.group(1)}"
                m = re.search(r'<repository [^>]*url="([^"]+)"', text) or re.search(r"<projectUrl>([^<]+)</projectUrl>", text)
                origin = m.group(1) if m else None
            status, detected, notice = check(declared, license_files(folder))
            if status == "SEM ARQUIVO" and declared:
                status = "SÓ EXPRESSÃO"  # pacotes NuGet podem declarar só a expressão SPDX no nuspec, sem arquivo.
            c = Component(
                key, name, version, "nuget", "library", purl=f"pkg:nuget/{name}@{version}", origin=origin,
                usage=NUGET_USAGE.get(name, ""), direct=name in (direct if runtime else test_direct),
                environment="execução" if runtime else "desenvolvimento",
                distribution="incorporado (sidecar .NET single-file)" if runtime else "não distribuído",
                artifacts={"windows-x64", "linux-x64"} if runtime else {"desenvolvimento"},
                declared=declared, detected=detected, status=status, notice_file=notice,
                evidence=[rel(path), rel(folder)],
            )
            if lib.get("sha512"):
                c.hashes.append(("SHA-512", b64hex(lib["sha512"])))
            c = inv.add(c)
            deps = target.get(lib_key, {}).get("dependencies", {})
            for dep_name in deps:
                match = next((k for k in data["libraries"] if k.split("/")[0].lower() == dep_name.lower()), None)
                if match:
                    c.depends_on.add(f"nuget:{match.split('/')[0]}@{match.split('/')[1]}")
        for name in (direct if runtime else test_direct):
            match = next((k for k in data["libraries"] if k.split("/")[0] == name), None)
            if match:
                inv.roots["runtime" if runtime else "dev"].append(f"nuget:{name}@{match.split('/')[1]}")
    # Ordem fixa: o pai mostrado de um transitivo não pode mudar a cada execução.
    for c in sorted(inv.items.values(), key=lambda item: item.key):
        for child in sorted(c.depends_on):
            if child in inv.items and inv.items[child].parent is None and child.startswith("nuget:"):
                inv.items[child].parent = c.name


def canonical(name: str) -> str:
    return re.sub(r"[-_.]+", "-", name).lower()


def collect_python(inv: Inventory) -> None:
    lock = (ROOT / "intelligence/requirements.lock").read_text(encoding="utf-8")
    pinned: dict[str, tuple[str, str | None, int]] = {}
    for block in re.split(r"\n(?=[a-zA-Z0-9])", lock):
        m = re.match(r"([A-Za-z0-9_.-]+)==([^\s;\\]+)(?:\s*;\s*([^\\\n]+))?", block)
        if m:
            pinned[canonical(m.group(1))] = (m.group(2), (m.group(3) or "").strip() or None, len(re.findall(r"--hash=sha256:", block)))
    packages = ROOT / "intelligence/packages"
    dists = {}
    for dist in packages.glob("*.dist-info"):
        meta = (dist / "METADATA").read_text(encoding="utf-8", errors="replace")
        header = meta.split("\n\n", 1)[0]
        name = re.search(r"^Name: (.+)$", header, re.M).group(1).strip()
        dists[canonical(name)] = (dist, header)
    direct = {canonical(line.split("==")[0]) for line in (ROOT / "intelligence/requirements.in").read_text().splitlines() if "==" in line}
    for cname, (version, marker, hashes) in pinned.items():
        dist, header = dists.get(cname, (None, ""))
        declared = None
        if header:
            m = re.search(r"^License-Expression: (.+)$", header, re.M)
            declared = m.group(1).strip() if m else None
            if not declared:
                m = re.search(r"^License: (.+)$", header, re.M)
                raw = m.group(1).strip() if m else ""
                classifiers = re.findall(r"^Classifier: License :: OSI Approved :: (.+)$", header, re.M)
                guess = {"MIT License": "MIT", "Apache Software License": "Apache-2.0", "BSD License": "BSD",
                         "Mozilla Public License 2.0 (MPL 2.0)": "MPL-2.0", "Python Software Foundation License": "PSF-2.0",
                         "ISC License (ISCL)": "ISC"}
                if raw and len(raw) < 60 and "\n" not in raw:
                    declared = raw
                elif classifiers:
                    declared = " OR ".join(guess.get(c, c) for c in classifiers)
        files = license_files(dist, recursive=True) if dist else []
        status, detected, notice = check(normalize(declared), files)
        if declared == "BSD" and detected:
            declared = " OR ".join(d for d in detected if d.startswith("BSD")) or declared
            status, detected, notice = check(declared, files)
        installed = dist is not None
        origin = None
        if header:
            m = re.search(r"^Project-URL: (?:Source|Repository|Source Code|Homepage|homepage|repository|source), (.+)$", header, re.M | re.I) \
                or re.search(r"^Home-page: (.+)$", header, re.M)
            origin = m.group(1).strip() if m else None
        name = re.search(r"^Name: (.+)$", header, re.M).group(1).strip() if header else cname
        c = Component(
            f"pypi:{cname}@{version}", name, version, "pypi", "library", purl=f"pkg:pypi/{cname}@{version}", origin=origin,
            usage=PY_USAGE.get(cname, ""), direct=cname in direct, environment="execução" if installed else "não instalado",
            distribution="redistribuído (bytecode em resources/intelligence/packages)" if installed else f"não instalado (marcador {marker})",
            artifacts={"windows-x64", "linux-x64"} if installed else set(), declared=declared, detected=detected, status=status,
            notice_file=notice, evidence=["intelligence/requirements.lock"] + ([rel(dist)] if dist else []),
            note=f"lock fixa {hashes} hash(es) sha256 de artefatos permitidos" + (f"; marcador: {marker}" if marker else ""),
        )
        c = inv.add(c)
        if header:
            for req in re.findall(r"^Requires-Dist: ([A-Za-z0-9_.-]+)([^\n]*)$", header, re.M):
                if "extra ==" in req[1]:
                    continue
                child = canonical(req[0])
                if child in pinned:
                    c.depends_on.add(f"pypi:{child}@{pinned[child][0]}")
        if cname in direct:
            inv.roots["intelligence"].append(c.key)
    # Ordem fixa: o pai mostrado de um transitivo não pode mudar a cada execução.
    for c in sorted(inv.items.values(), key=lambda item: item.key):
        for child in sorted(c.depends_on):
            if child in inv.items and inv.items[child].parent is None and child.startswith("pypi:"):
                inv.items[child].parent = c.name


# ---------------------------------------------------------------------------------------------------------------------
# Componentes fora de gerenciador de pacotes: mantidos à mão a partir da auditoria. Cada entrada cita a evidência.

def sha256(path: Path) -> str | None:
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None


def manual(inv: Inventory) -> None:
    py = ROOT / "desktop/host/src-tauri/resources/python"
    entries = [
        # (chave, nome, versão, ecossistema, tipo, purl, origem, uso, ambiente, distribuição, artefatos, licença, risco, motivo, evidências, hash-path, nota)
        ("bin:cpython-embed", "CPython (Windows embeddable package)", "3.12.10", "binário", "runtime",
         "pkg:generic/cpython-embed-amd64@3.12.10?download_url=https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip",
         "https://www.python.org/", "Interpretador da inteligência (LangGraph) no Windows", "execução",
         "redistribuído (resources/python no instalador)", {"windows-x64"}, "Python-2.0", "MÉDIO",
         "Redistribuição exige manter o LICENSE.txt e, pelo código MSVC embutido, termos de usuário final que protejam o Distributable Code da Microsoft",
         ["scripts/build-runtime.ps1:66", "desktop/host/src-tauri/resources/python/LICENSE.txt:283"], py / "python312.dll",
         "Inclui OpenSSL 3.0.16 (Apache-2.0), libffi (MIT), bzip2 1.0.8, SQLite (domínio público), expat 2.7.1 (MIT), mpdecimal, "
         "xz/liblzma, zlib; vcruntime140.dll e vcruntime140_1.dll (Microsoft Visual C++ Redistributable)."),
        ("bin:openssl", "OpenSSL (dentro do CPython)", "3.0.16", "binário", "library", "pkg:generic/openssl@3.0.16", "https://www.openssl.org/",
         "TLS do Python embarcado (libssl-3.dll, libcrypto-3.dll)", "execução", "redistribuído (resources/python)", {"windows-x64"},
         "Apache-2.0", "BAIXO", "Apache-2.0: manter licença", ["desktop/host/src-tauri/resources/python/libcrypto-3.dll"], py / "libcrypto-3.dll", ""),
        ("bin:vcruntime", "Microsoft Visual C++ Runtime (vcruntime140.dll, vcruntime140_1.dll)", "14.x", "binário", "runtime",
         "pkg:generic/microsoft-vcruntime@14", "https://learn.microsoft.com/cpp/windows/redistributing-visual-cpp-files",
         "Runtime C do CPython embarcado", "execução", "redistribuído (resources/python)", {"windows-x64"},
         "LicenseRef-Microsoft-Visual-Cpp-Redistributable", "MÉDIO",
         "Distributable Code da Microsoft: exige EULA do Spock com proteção equivalente; só para plataformas Microsoft",
         ["desktop/host/src-tauri/resources/python/vcruntime140.dll", "desktop/host/src-tauri/resources/python/LICENSE.txt:286"],
         py / "vcruntime140.dll", ""),
        ("bin:python-build-standalone", "CPython (python-build-standalone, install_only_stripped)", "3.12.10+20250409", "binário", "runtime",
         "pkg:github/astral-sh/python-build-standalone@20250409", "https://github.com/astral-sh/python-build-standalone",
         "Interpretador da inteligência no Linux (e macOS fora da 1ª versão)", "execução", "redistribuído (resources/python no AppImage/deb)",
         {"linux-x64"}, "Python-2.0", "MÉDIO",
         "Redistribuição exige os avisos da PSF e dos componentes nativos (OpenSSL, SQLite, ncurses, libedit, libffi, expat, mpdecimal, zlib, xz, libX11); "
         "o PYTHON.json/licenças do arquivo baixado não foram conferidos localmente",
         ["scripts/package-intelligence.py:31", "https://raw.githubusercontent.com/astral-sh/python-build-standalone/20250409/cpython-unix/targets.yml"], None,
         "SHA-256 do arquivo fixado no script: 8c59b9ac6bff2dc3934181d7bc82594f9f59a613afed8d72c9e89d7194e790ee. readline (GPL) não é usado (USE_LIBEDIT) e _gdbm é desativado."),
        ("bin:berkeley-db", "Berkeley DB (dentro do python-build-standalone, módulo _dbm)", "6.0.19", "binário", "library",
         "pkg:generic/berkeley-db@6.0.19", "https://github.com/astral-sh/python-build-standalone/blob/20250409/pythonbuild/downloads.py",
         "Backend do módulo dbm do Python embarcado no Linux (não usado pela inteligência)", "execução", "redistribuído (resources/python no AppImage/deb)",
         {"linux-x64"}, "Sleepycat", "ALTO",
         "A licença Sleepycat exige informar como obter o código-fonte completo do DB e de software que o use; o script de poda não remove _dbm",
         ["scripts/package-intelligence.py:144", "https://raw.githubusercontent.com/astral-sh/python-build-standalone/20250409/pythonbuild/downloads.py"], None,
         "Presença do _dbm*.so no pacote Linux a confirmar num build Linux."),
        ("bin:dotnet-runtime-win", "Microsoft .NET Runtime para win-x64 (self-contained)", "10.0.x", "nuget", "runtime",
         "pkg:nuget/Microsoft.NETCore.App.Runtime.win-x64@10.0.13", "https://github.com/dotnet/runtime",
         "Runtime do motor .NET embutido no sidecar single-file (Windows)", "execução", "incorporado (sidecar .NET single-file)", {"windows-x64"},
         "MIT AND LicenseRef-DotNet-Library-License", "MÉDIO",
         "O pacote de runtime do Windows é distribuído sob a .NET Library License (coreclr.dll, DiaSymReader) além do MIT: exige termos de usuário final "
         "protetivos, não remover avisos e não usar marcas da Microsoft; o single-file não leva LICENSE.TXT nem THIRD-PARTY-NOTICES.TXT",
         ["scripts/build-runtime.ps1:31", "~/.nuget/packages/microsoft.netcore.app.runtime.win-x64/10.0.13/LICENSE.TXT",
          "https://raw.githubusercontent.com/dotnet/core/main/license-information-windows.md"], None,
         "Versão exata depende do SDK 10.0.x do build (10.0.13 no cache local)."),
        ("bin:dotnet-runtime-linux", "Microsoft .NET Runtime para linux-x64 (self-contained)", "10.0.x", "nuget", "runtime",
         "pkg:nuget/Microsoft.NETCore.App.Runtime.linux-x64@10.0", "https://github.com/dotnet/runtime",
         "Runtime do motor .NET embutido no sidecar single-file (Linux)", "execução", "incorporado (sidecar .NET single-file)", {"linux-x64"},
         "MIT", "MÉDIO", "MIT exige o aviso em todas as cópias; o single-file não leva LICENSE.TXT nem THIRD-PARTY-NOTICES.TXT do runtime",
         [".github/workflows/ci.yml:117", "https://raw.githubusercontent.com/dotnet/core/main/license-information.md"], None, ""),
        ("bin:nsis", "NSIS (stub do instalador e plugins)", "3.x", "binário", "installer", "pkg:generic/nsis@3", "https://nsis.sourceforge.io/",
         "Gera o instalador .exe do Windows (stub embutido no setup)", "build", "incorporado (stub do setup.exe)", {"windows-x64"},
         "Zlib AND bzip2-1.0.6 AND CPL-1.0", "BAIXO",
         "zlib/libpng sem obrigação de aviso em binário; módulo LZMA (CPL-1.0) com exceção expressa para código vinculado",
         ["~/AppData/Local/tauri/NSIS/COPYING:141", "desktop/host/src-tauri/tauri.conf.json:52"], None, "Baixado pelo tauri-bundler durante o build."),
        ("bin:nsis-tauri-utils", "nsis_tauri_utils (plugin NSIS do Tauri)", "conforme tauri-bundler", "binário", "installer",
         "pkg:github/tauri-apps/nsis-tauri-utils", "https://github.com/tauri-apps/nsis-tauri-utils", "Plugin do instalador (processos, idioma)",
         "build", "incorporado (setup.exe)", {"windows-x64"}, "MIT OR Apache-2.0", "BAIXO", "Permissiva",
         ["~/AppData/Local/tauri/NSIS/Plugins/x86-unicode/additional/nsis_tauri_utils.dll"], None, ""),
        ("ext:webview2", "Microsoft Edge WebView2 Runtime (Evergreen)", "do sistema", "sistema operacional", "runtime",
         "pkg:generic/microsoft-webview2-runtime", "https://developer.microsoft.com/microsoft-edge/webview2/",
         "Motor web da janela no Windows", "instalação sob demanda", "baixado sob demanda (bootstrapper, se ausente) ou do sistema",
         {"windows-x64", "sob-demanda"}, "LicenseRef-Microsoft-WebView2", "BAIXO",
         "Padrão do Tauri (downloadBootstrapper): o instalador baixa o bootstrapper da própria Microsoft se o runtime faltar; nada é redistribuído pelo Spock. "
         "Embutir o bootstrapper ou o instalador offline passaria a exigir os termos de distribuição da Microsoft",
         ["desktop/host/src-tauri/tauri.conf.json:48"], None, "Pré-instalado no Windows 11 e na maioria dos Windows 10 atualizados."),
        ("ext:webkitgtk", "WebKitGTK 4.1 + GTK 3 e dependências", "do sistema / embutidas no AppImage", "sistema operacional", "runtime",
         "pkg:generic/webkit2gtk@4.1", "https://webkitgtk.org/", "Motor web da janela no Linux", "execução",
         "redistribuído no AppImage; dependência do sistema no .deb", {"linux-x64"}, "LGPL-2.1-or-later", "ALTO",
         "No AppImage as bibliotecas LGPL são redistribuídas pelo Spock: exige texto da licença, oferta do código-fonte correspondente e permitir substituição das bibliotecas",
         [".github/workflows/ci.yml:107", ".github/workflows/ci.yml:79"], None, "A lista real de bibliotecas do AppImage não foi inspecionada (sem build Linux local)."),
        ("dl:opentofu", "OpenTofu (tofu)", "dinâmica (última estável compatível; testes usam 1.13.9)", "binário", "tool",
         "pkg:github/opentofu/opentofu", "https://github.com/opentofu/opentofu", "Motor padrão de infraestrutura como código",
         "instalação sob demanda", "baixado sob demanda da fonte oficial, com consentimento", {"sob-demanda"}, "MPL-2.0", "BAIXO",
         "O Spock não redistribui: baixa do GitHub oficial na máquina do usuário. Obrigações da MPL recaem sobre quem distribui",
         ["runtime/Mariliz.Infrastructure/Iac/OpenTofuToolchain.cs:13", "runtime/Mariliz.Application/Iac/IacRuntimeService.cs:269"], None,
         "A extração descarta LICENSE e README do pacote oficial (TERRAFORM_DEPENDENCY_MANAGEMENT.md:132)."),
        ("dl:terraform", "Terraform (HashiCorp)", "dinâmica (≥ 1.6 sob BUSL; testes usam 1.17.2)", "binário", "tool",
         "pkg:generic/hashicorp/terraform", "https://github.com/hashicorp/terraform", "Motor alternativo de IaC, por escolha explícita do usuário",
         "instalação sob demanda", "baixado sob demanda de releases.hashicorp.com, com consentimento; ou instalação do usuário", {"sob-demanda"},
         "BUSL-1.1", "ALTO",
         "Licença source-available com restrição de oferta competitiva; o Spock instala e opera o Terraform dentro de um produto comercial pago",
         ["runtime/Mariliz.Infrastructure/Iac/HashiCorpTerraformToolchain.cs:8", "docs/plataforma/TERRAFORM_DEPENDENCY_MANAGEMENT.md:15"], None, ""),
        ("dl:provider-aws", "Provider hashicorp/aws", "6.66.0 (modelo de layout)", "registro Terraform/OpenTofu", "tool",
         "pkg:terraform/hashicorp/aws@6.66.0", "https://github.com/hashicorp/terraform-provider-aws",
         "Provider da AWS nos projetos de IaC (baixado pelo tofu/terraform init no projeto do usuário)", "instalação sob demanda",
         "baixado sob demanda pelo motor de IaC (cache em <dados>/iac/plugin-cache)", {"sob-demanda"}, "MPL-2.0", "BAIXO",
         "Não redistribuído pelo Spock; o OpenTofu busca em registry.opentofu.org e o Terraform em registry.terraform.io "
         "(termos deste restringem o uso a 'HashiCorp Terraform', o que é o caso)",
         ["docs/plataforma/TERRAFORM_PROJECT_LAYOUT.md:111", "runtime/Mariliz.Infrastructure/Iac/CliIacRuntime.cs:133"], None, ""),
        ("dl:agent-skills", "@tech-leads-club/agent-skills (skill tlc-spec-driven)", "dinâmica (validadas 3.3.0 e 4.0.0)", "npm", "tool",
         "pkg:npm/%40tech-leads-club/agent-skills", "https://www.npmjs.com/package/@tech-leads-club/agent-skills",
         "Instalador da estrutura de especificação no projeto do usuário (npx)", "instalação sob demanda",
         "baixado e executado sob demanda via npx no projeto do usuário", {"sob-demanda"}, "MIT AND CC-BY-4.0", "MÉDIO",
         "CLI MIT (npm), mas o conteúdo da skill tlc-spec-driven é CC-BY-4.0 (exige atribuição); o Spock não redistribui, o pacote vem do npm para o projeto do usuário",
         ["runtime/Mariliz.Application/Specification/SpecificationManifest.cs:13"], None, ""),
        ("ext:git", "Git", "do usuário", "externo", "tool", "pkg:generic/git", "https://git-scm.com/", "Detecção de repositório e status",
         "externo", "externo (instalação do usuário, invocado como processo)", {"externo"}, "GPL-2.0-only", "BAIXO",
         "Execução de um programa separado, sem link nem redistribuição", ["runtime/Mariliz.Application/Discovery/SystemProbe.cs:21"], None, ""),
        ("ext:claude-code", "Claude Code CLI", "do usuário", "externo", "tool", "pkg:npm/%40anthropic-ai/claude-code", "https://www.anthropic.com/",
         "Agente de IA invocado em modo somente leitura", "externo", "externo (instalação e conta do usuário)", {"externo"},
         "LicenseRef-Anthropic-Commercial-Terms", "MÉDIO",
         "Permitido pelos termos da Anthropic com binário sem modificação, login do próprio usuário e sem intermediar credenciais nem uso; não usar 'Claude Code' na marca",
         ["runtime/Mariliz.Application/AiTools/AiToolCatalog.cs:16"], None, ""),
        ("ext:codex", "OpenAI Codex CLI", "do usuário", "externo", "tool", "pkg:npm/%40openai/codex", "https://github.com/openai/codex",
         "Agente de IA invocado com sandbox somente leitura", "externo", "externo (instalação e conta do usuário)", {"externo"}, "Apache-2.0", "BAIXO",
         "Programa separado do usuário", ["runtime/Mariliz.Application/AiTools/AiToolCatalog.cs:22"], None, ""),
        ("ext:cursor-agent", "Cursor Agent CLI", "do usuário", "externo", "tool", "pkg:generic/cursor-agent", "https://cursor.com/",
         "Agente de IA invocado com sandbox", "externo", "externo (instalação e conta do usuário)", {"externo"}, "LicenseRef-Cursor-Terms", "PENDENTE",
         "Software proprietário; termos de uso por integração de terceiros não verificados", ["runtime/Mariliz.Application/AiTools/AiToolCatalog.cs:19"], None, ""),
        ("ext:gemini-cli", "Gemini CLI", "do usuário", "externo", "tool", "pkg:npm/%40google/gemini-cli", "https://github.com/google-gemini/gemini-cli",
         "Agente de IA invocado sem --yolo", "externo", "externo (instalação e conta do usuário)", {"externo"}, "Apache-2.0", "BAIXO",
         "Programa separado do usuário", ["runtime/Mariliz.Application/AiTools/AiToolCatalog.cs:25"], None, ""),
        ("ext:node-npx", "Node.js / npx", "do usuário", "externo", "tool", "pkg:generic/nodejs", "https://nodejs.org/",
         "Executa o instalador da estrutura de especificação", "externo", "externo (instalação do usuário)", {"externo"}, "MIT", "BAIXO",
         "Programa separado do usuário", ["runtime/Mariliz.Infrastructure/SystemProcessRunner.cs:84"], None, ""),
        ("svc:llm-apis", "APIs de modelo (OpenAI, Anthropic, Google Gemini)", "n/a", "serviço", "service", None,
         "https://api.openai.com / https://api.anthropic.com / https://generativelanguage.googleapis.com",
         "Provedores de IA com chave do próprio usuário (cofre local)", "externo", "externo (serviço)", {"externo"}, None, "PENDENTE",
         "Termos comerciais de cada provedor; o Spock não revende acesso", ["runtime/Mariliz.Application/Models/ModelProviders.cs:12"], None, ""),
        ("svc:cloud-apis", "APIs de nuvem e integrações (AWS, Azure, GCP, GitHub, Jira)", "n/a", "serviço", "service", None, None,
         "Leitura de recursos de nuvem e integrações com credenciais do usuário", "externo", "externo (serviço)", {"externo"}, None, "BAIXO",
         "Uso pelo próprio usuário sob os termos de cada serviço", ["runtime/Mariliz.Infrastructure.Cloud", "runtime/Mariliz.Application/Integrations/Connectors"],
         None, ""),
        ("asset:icons", "Ícones e logotipo Spock Studio", "0.1.0", "asset", "asset", None, None, "Ícone do app, instalador e favicon",
         "execução", "incorporado (instalador e interface)", {"windows-x64", "linux-x64"}, "LicenseRef-Proprietary-Spock", "PENDENTE",
         "Desenho declarado original (comentário no SVG); a marca 'Spock' e a sobrancelha erguida podem remeter a personagem de terceiros (questão de marca, não de copyright)",
         ["desktop/host/src-tauri/icons/source/spock-studio.svg:2", "desktop/ui/public/favicon.svg"], ROOT / "desktop/host/src-tauri/icons/icon.ico", ""),
        ("asset:release-keys", "Chaves públicas OpenPGP de releases (HashiCorp, OpenTofu)", "embutidas", "asset", "data", None, None,
         "Verificação da assinatura dos binários baixados", "execução", "incorporado (sidecar)", {"windows-x64", "linux-x64"},
         "NOASSERTION", "BAIXO", "Chave pública: dado factual publicado para verificação, sem restrição de uso conhecida",
         ["runtime/Mariliz.Infrastructure/Iac/hashicorp-release-key.asc", "runtime/Mariliz.Infrastructure/Iac/opentofu-release-key.asc"], None, ""),
        ("asset:harness-content", "Conteúdo de agentes e skills adaptado de harness-engineering", "embutido", "asset", "data", None,
         "https://github.com/Fabinschulz/harness-engineering", "Agentes, skill e regras gerados nos projetos do usuário", "execução",
         "incorporado (recurso do sidecar) e copiado para projetos do usuário", {"windows-x64", "linux-x64"}, "MIT", "BAIXO",
         "Repositório do mesmo titular, MIT; manter a atribuição já presente",
         ["runtime/Mariliz.Application/Assets/agents/code-reviewer.md:10", "runtime/Mariliz.Application/Assets/skills/harness-contract/SKILL.md:8"], None, ""),
        ("ci:github-actions", "GitHub Actions (checkout, setup-*, upload-artifact, pnpm/action-setup, dtolnay/rust-toolchain, swatinem/rust-cache)",
         "v2–v5", "GitHub Actions", "tool", None, "https://github.com/actions", "CI e build dos instaladores", "build", "não distribuído",
         {"desenvolvimento"}, None, "BAIXO", "Executados só no CI; swatinem/rust-cache é LGPL-3.0, sem efeito no produto",
         [".github/workflows/ci.yml"], None, ""),
        ("ci:ruff", "Ruff", "0.17.4", "pypi", "tool", "pkg:pypi/ruff@0.17.4", "https://github.com/astral-sh/ruff", "Lint do Python no CI",
         "desenvolvimento", "não distribuído", {"desenvolvimento"}, "MIT", "BAIXO", "Só desenvolvimento", [".github/workflows/ci.yml:36"], None, ""),
    ]
    for (key, name, version, eco, ctype, purl, origin, usage, env, dist, artifacts, lic, risk, reason, evidence, hpath, note) in entries:
        c = Component(key, name, version, eco, ctype, purl=purl, origin=origin, usage=usage, direct=True, environment=env,
                      distribution=dist, artifacts=set(artifacts), declared=lic, status="CONFERIDO MANUALMENTE" if lic else "PENDENTE",
                      risk=risk, risk_reason=reason, evidence=evidence, note=note)
        digest = sha256(hpath) if hpath else None
        if digest:
            c.hashes.append(("SHA-256", digest))
            c.note = (c.note + " " if c.note else "") + f"Hash do arquivo {rel(hpath)} do último build local."
        inv.add(c)
        for artifact in artifacts:
            inv.roots[{"windows-x64": "bin-windows", "linux-x64": "bin-linux"}.get(artifact, artifact)].append(key)


# ---------------------------------------------------------------------------------------------------------------------
# Classificação de risco e obrigações dos componentes vindos de gerenciador.

def classify(c: Component) -> None:
    if c.risk:
        if not c.obligations:
            if c.environment in {"externo", "instalação sob demanda"} and not c.distribution.startswith(("incorporado", "redistribuído")):
                c.obligations = MANUAL_OBLIGATIONS.get(c.key, "Nenhuma obrigação de redistribuição (o Spock não entrega o componente); respeitar os termos de uso do titular.")
            else:
                c.obligations = MANUAL_OBLIGATIONS.get(c.key) or obligations_for(effective(normalize(c.declared)) if c.declared else [], c)
        return
    shipped = c.distribution.startswith(("incorporado", "redistribuído"))
    if c.declared and "LicenseRef-OSMF-EULA" in c.declared:
        c.risk = "ALTO"
        c.risk_reason = ("Binário NuGet sob o EULA Open Source Maintenance Fee: taxa mensal para usuários com atividade geradora de receita "
                         "e faturamento anual ≥ US$10.000; não distribuído, mas usado no desenvolvimento de produto comercial")
        c.obligations = "Pagar a taxa ao projeto json-everything, compilar do fonte (MIT) ou substituir por alternativa sem taxa"
        return
    eff = effective(normalize(c.declared))
    c.obligations = obligations_for(eff, c) if shipped else "Nenhuma para o produto (não distribuído)."
    if not shipped:
        c.risk = "BAIXO" if eff or c.environment == "não instalado" else "PENDENTE"
        c.risk_reason = "Não distribuído ao usuário final" + ("" if eff else "; licença não declarada")
        if c.risk == "PENDENTE" and c.environment in {"desenvolvimento", "build"}:
            c.risk, c.risk_reason = "BAIXO", "Não distribuído; licença não declarada (verificar só se passar a ser distribuído)"
        return
    if not eff:
        c.risk, c.risk_reason = "PENDENTE", f"Licença declarada ausente ou fora do SPDX ({c.declared or 'nenhuma'})"
    elif any(t in STRONG_COPYLEFT for t in eff):
        c.risk, c.risk_reason = "CRÍTICO", "Copyleft forte em componente distribuído"
    elif any(t in SOURCE_AVAILABLE for t in eff):
        c.risk, c.risk_reason = "ALTO", "Licença source-available com restrições comerciais"
    elif any(t in WEAK_COPYLEFT for t in eff):
        c.risk, c.risk_reason = "MÉDIO", "Copyleft fraco (por arquivo/biblioteca): informar onde obter o código-fonte e manter a licença"
    else:
        c.risk, c.risk_reason = "BAIXO", "Permissiva: manter aviso de copyright e texto da licença"
    if c.status == "DIVERGE":
        c.risk_reason += f"; arquivo de licença indica {', '.join(c.detected)} (declarado: {c.declared})"
        if c.risk == "BAIXO" and any(d in STRONG_COPYLEFT | WEAK_COPYLEFT | {"GPL", "LGPL", "AGPL", "BUSL-1.1"} for d in c.detected):
            c.risk = "PENDENTE"


MANUAL_OBLIGATIONS = {
    "bin:cpython-embed": "Incluir LICENSE.txt do CPython (já vai em resources/python) nos avisos visíveis; EULA do Spock com proteção ao "
                         "Distributable Code da Microsoft; não usar marcas da PSF/Microsoft",
    "bin:vcruntime": "Termos de usuário final protetivos; só plataformas Microsoft; não alterar avisos",
    "bin:python-build-standalone": "Reproduzir a licença do CPython e de cada biblioteca nativa listada no PYTHON.json da release",
    "bin:berkeley-db": "Sleepycat: informar como obter o código-fonte completo do Berkeley DB e de software que o use; recomendado remover o _dbm do pacote",
    "bin:dotnet-runtime-win": "Incluir LICENSE.TXT e THIRD-PARTY-NOTICES.TXT do runtime; EULA com termos protetivos (.NET Library License); "
                              "distribuir só dentro do app; não usar marcas da Microsoft",
    "bin:dotnet-runtime-linux": "Incluir LICENSE.TXT e THIRD-PARTY-NOTICES.TXT do runtime nos avisos",
    "bin:nsis": "Nenhuma obrigatória em binário (atribuição recomendada); não modificar o módulo LZMA sem cumprir a CPL-1.0",
    "bin:nsis-tauri-utils": "Aviso MIT nos avisos de terceiros",
    "bin:openssl": "Texto da Apache-2.0 nos avisos",
    "ext:webkitgtk": "AppImage: textos da LGPL, código-fonte correspondente (ou oferta escrita por 3 anos) das bibliotecas embutidas e "
                     "bibliotecas substituíveis (já são .so); .deb: nenhuma (dependência do sistema)",
    "asset:icons": "Nenhuma de licença (ativo próprio); pendência de marca",
    "asset:harness-content": "Manter a linha de atribuição MIT já presente nos arquivos",
    "dl:terraform": "Usar só dentro do Additional Use Grant da BUSL 1.1 ou com licença comercial da HashiCorp/IBM; informar a licença ao usuário (já feito)",
    "dl:agent-skills": "Se algum conteúdo da skill passar a ser embarcado ou gerado pelo Spock: atribuição CC-BY-4.0 (autor, licença, alterações)",
    "ext:claude-code": "Invocar o binário sem modificação, com login do próprio usuário, sem ler/armazenar tokens; não usar 'Claude Code' no nome do produto",
}


def obligations_for(eff: list[str], c: Component) -> str:
    out = []
    for lic in eff:
        if lic in {"MIT", "MIT-0", "ISC", "BSD-2-Clause", "BSD-3-Clause", "Zlib", "Unicode-3.0", "Unicode-DFS-2016", "BSL-1.0", "BlueOak-1.0.0"}:
            out.append({"MIT-0": "nenhuma", "Zlib": "não deturpar a origem; em binário o aviso é recomendado",
                        "BSL-1.0": "aviso só em distribuição de fonte"}.get(lic, "aviso de copyright e texto da licença nos avisos de terceiros"))
            if lic == "BSD-3-Clause":
                out.append("não usar nomes dos autores para endossar")
        elif lic == "Apache-2.0":
            out.append("texto da Apache-2.0 e conteúdo do NOTICE (se houver) nos avisos; marcar arquivos modificados")
        elif lic in {"PSF-2.0", "Python-2.0"}:
            out.append("manter o aviso de copyright da PSF e o texto da licença")
        elif lic == "MPL-2.0":
            out.append("manter a licença; informar onde obter o código-fonte dos arquivos MPL; modificações nesses arquivos ficam MPL")
        elif lic.startswith("LGPL"):
            out.append("texto da LGPL, código-fonte correspondente (ou oferta) e permitir substituir a biblioteca")
        elif lic in {"0BSD", "Unlicense", "CC0-1.0"}:
            out.append("nenhuma")
        elif lic == "BUSL-1.1":
            out.append("respeitar o Additional Use Grant; uso fora dele exige licença comercial do titular")
        else:
            out.append(f"verificar termos de {lic}")
    if c.notice_file:
        out.append("reproduzir o arquivo NOTICE")
    return "; ".join(dict.fromkeys(out)) or "a definir"


# ---------------------------------------------------------------------------------------------------------------------
# Saídas: inventário (JSON/CSV/Markdown) e SBOMs.

ARTIFACTS = {
    "windows-x64": ("Spock Studio para Windows x64 (instalador NSIS)", {"windows-x64"}),
    "linux-x64": ("Spock Studio para Linux x64 (AppImage e .deb)", {"linux-x64"}),
}


def ref(key: str) -> str:
    return re.sub(r"[^A-Za-z0-9.-]", "-", key)


def cdx(inv: Inventory, name: str, members: list[Component], timestamp: str, scope_note: str) -> dict:
    keys = {c.key for c in members}
    root_ref = "spock-studio"
    comps = []
    for c in members:
        item: dict = {"type": {"runtime": "platform", "tool": "application", "installer": "application", "service": "application",
                               "asset": "file", "data": "data"}.get(c.ctype, "library"),
                      "bom-ref": ref(c.key), "name": c.name, "version": c.version}
        if c.ctype == "data":
            item["type"] = "data"
            item["data"] = [{"type": "other", "name": c.name}]
        if c.purl:
            item["purl"] = c.purl
        item["scope"] = "required" if c.distribution.startswith(("incorporado", "redistribuído")) else (
            "optional" if c.environment in {"instalação sob demanda", "externo"} else "excluded")
        if c.hashes:
            item["hashes"] = [{"alg": a, "content": v} for a, v in c.hashes]
        expr = c.spdx_declared
        if expr != "NOASSERTION":
            if expr.startswith("LicenseRef-") and " " not in expr:
                item["licenses"] = [{"license": {"name": expr.removeprefix("LicenseRef-")}}]
            else:
                item["licenses"] = [{"expression": expr}]
        if c.origin and re.match(r"^https?://", c.origin.split(" ")[0]):
            item["externalReferences"] = [{"type": "vcs" if "github.com" in c.origin else "website", "url": c.origin.split(" ")[0]}]
        props = {"spock:ecosystem": c.ecosystem, "spock:environment": c.environment, "spock:distribution": c.distribution,
                 "spock:dependency": "direta" if c.direct else "transitiva", "spock:license-file-check": c.status,
                 "spock:license-file-detected": ", ".join(c.detected), "spock:risk": c.risk or "", "spock:obligations": c.obligations,
                 "spock:evidence": " | ".join(c.evidence), "spock:usage": c.usage, "spock:note": c.note}
        item["properties"] = [{"name": k, "value": v} for k, v in props.items() if v]
        comps.append(item)
    deps = [{"ref": root_ref, "dependsOn": sorted({ref(k) for r in inv.roots.values() for k in r if k in keys} & {ref(c.key) for c in members if c.direct})}]
    for c in members:
        children = sorted(ref(k) for k in c.depends_on if k in keys)
        deps.append({"ref": ref(c.key), "dependsOn": children})
    return {
        "bomFormat": "CycloneDX", "specVersion": "1.6", "serialNumber": f"urn:uuid:{uuid.uuid4()}", "version": 1,
        "metadata": {
            "timestamp": timestamp,
            "lifecycles": [{"phase": "build"}],
            "tools": {"components": [{"type": "application", "name": TOOL[0], "version": TOOL[1]}]},
            "component": {"type": "application", "bom-ref": root_ref, "name": PRODUCT[0], "version": PRODUCT[1],
                          "description": name, "licenses": [{"license": {"name": "Proprietary"}}]},
            "properties": [{"name": "spock:coverage", "value": scope_note}],
        },
        "components": comps, "dependencies": deps,
    }


def spdx(inv: Inventory, name: str, members: list[Component], timestamp: str, scope_note: str) -> dict:
    keys = {c.key for c in members}
    root = "SPDXRef-Package-spock-studio"
    packages = [{
        "SPDXID": root, "name": PRODUCT[0], "versionInfo": PRODUCT[1], "downloadLocation": "NOASSERTION", "filesAnalyzed": False,
        "licenseConcluded": "LicenseRef-Proprietary", "licenseDeclared": "LicenseRef-Proprietary", "copyrightText": "NOASSERTION",
        "primaryPackagePurpose": "APPLICATION", "comment": name,
    }]
    rels = [{"spdxElementId": "SPDXRef-DOCUMENT", "relationshipType": "DESCRIBES", "relatedSpdxElement": root}]
    for c in members:
        sid = "SPDXRef-Package-" + ref(c.key)
        declared = c.spdx_declared
        pkg = {
            "SPDXID": sid, "name": c.name, "versionInfo": c.version,
            "downloadLocation": c.origin.split(" ")[0] if c.origin and re.match(r"^https?://", c.origin) else "NOASSERTION",
            "filesAnalyzed": False, "licenseDeclared": declared,
            "licenseConcluded": declared if c.status in {"CONFERE", "CONFERIDO MANUALMENTE", "SÓ EXPRESSÃO"} and declared != "NOASSERTION" else "NOASSERTION",
            "copyrightText": "NOASSERTION",
            "primaryPackagePurpose": {"runtime": "OPERATING_SYSTEM" if c.ecosystem == "sistema operacional" else "LIBRARY", "tool": "APPLICATION",
                                      "installer": "INSTALL", "asset": "FILE", "data": "OTHER", "service": "OTHER"}.get(c.ctype, "LIBRARY"),
            "comment": f"Uso: {c.usage or 'transitiva' + (' de ' + c.parent if c.parent else '')}. Ambiente: {c.environment}. "
                       f"Distribuição: {c.distribution}. Conferência do arquivo de licença: {c.status}"
                       + (f" ({', '.join(c.detected)})" if c.detected else "") + f". Risco: {c.risk}. Evidência: {' | '.join(c.evidence)}."
                       + (f" {c.note}" if c.note else ""),
        }
        if c.purl:
            pkg["externalRefs"] = [{"referenceCategory": "PACKAGE-MANAGER", "referenceType": "purl", "referenceLocator": c.purl}]
        if c.hashes:
            pkg["checksums"] = [{"algorithm": a.replace("-", ""), "checksumValue": v} for a, v in c.hashes]
        packages.append(pkg)
        if c.direct:
            rels.append({"spdxElementId": root, "relationshipType": "DEPENDS_ON" if c.environment != "externo" else "OTHER",
                         "relatedSpdxElement": sid, **({"comment": "processo ou serviço externo"} if c.environment == "externo" else {})})
        for child in sorted(c.depends_on):
            if child in keys:
                rels.append({"spdxElementId": sid, "relationshipType": "DEPENDS_ON", "relatedSpdxElement": "SPDXRef-Package-" + ref(child)})
    refs = sorted({t for p in packages for t in tokens(p["licenseDeclared"]) if t.startswith("LicenseRef-")})
    extracted = [{"licenseId": r, "name": r.removeprefix("LicenseRef-"),
                  "extractedText": "Licença não-SPDX ou proprietária; ver docs/compliance/DEPENDENCY_INVENTORY.md e LICENSE_RISK_REGISTER.md."}
                 for r in refs]
    return {
        "spdxVersion": "SPDX-2.3", "dataLicense": "CC0-1.0", "SPDXID": "SPDXRef-DOCUMENT", "name": f"{PRODUCT[0]} {PRODUCT[1]} - {name}",
        "documentNamespace": f"https://spdx.org/spdxdocs/spock-studio-{ref(name).lower()}-{uuid.uuid4()}",
        "creationInfo": {"created": timestamp, "creators": [f"Tool: {TOOL[0]}-{TOOL[1]}"], "comment": scope_note},
        "packages": packages, "relationships": rels, "hasExtractedLicensingInfos": extracted,
    }


def write_outputs(inv: Inventory) -> None:
    timestamp = datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    items = sorted(inv.items.values(), key=lambda c: (c.ecosystem, c.name.lower(), c.version))
    (OUT / "SBOM").mkdir(parents=True, exist_ok=True)
    (OUT / "data").mkdir(parents=True, exist_ok=True)
    coverage = ("Cobre dependências de npm, Cargo, NuGet e PyPI resolvidas pelos lockfiles e binários embarcados conhecidos. "
                "Não cobre: bibliotecas do sistema embutidas no AppImage (sem build Linux inspecionado), componentes internos do "
                "python-build-standalone, versões exatas das ferramentas baixadas sob demanda (escolhidas em tempo de execução). "
                "Ver docs/compliance/LICENSE_AUDIT.md, seção Limitações.")
    for art, (title, tags) in ARTIFACTS.items():
        members = [c for c in items if c.artifacts & tags]
        (OUT / "SBOM" / f"spock-{art}.cdx.json").write_text(json.dumps(cdx(inv, title, members, timestamp, coverage), indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        (OUT / "SBOM" / f"spock-{art}.spdx.json").write_text(json.dumps(spdx(inv, title, members, timestamp, coverage), indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    title = "Inventário consolidado (distribuído, sob demanda, externo e desenvolvimento)"
    (OUT / "SBOM" / "spock.cdx.json").write_text(json.dumps(cdx(inv, title, items, timestamp, coverage), indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    (OUT / "SBOM" / "spock.spdx.json").write_text(json.dumps(spdx(inv, title, items, timestamp, coverage), indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    rows = [{
        "nome": c.name, "versao": c.version, "tipo": c.ctype, "ecossistema": c.ecosystem, "origem": c.origin or "",
        "uso": c.usage or (f"transitiva de {c.parent}" if c.parent else ""), "dependencia": "direta" if c.direct else "transitiva",
        "ambiente": c.environment, "artefatos": ",".join(sorted(c.artifacts)), "licenca_declarada": c.declared or "",
        "licenca_confirmada": ", ".join(c.detected) if c.status in {"CONFERE", "DIVERGE"} else (c.declared if c.status in {"CONFERIDO MANUALMENTE", "SÓ EXPRESSÃO"} else "DESCONHECIDA / PENDENTE DE VERIFICAÇÃO"),
        "conferencia": c.status, "distribuicao": c.distribution, "obrigacoes": c.obligations, "risco": c.risk, "motivo": c.risk_reason,
        "evidencias": " | ".join(c.evidence), "hashes": " ".join(f"{a}:{v}" for a, v in c.hashes), "nota": c.note,
    } for c in items]
    with open(OUT / "data" / "inventory.csv", "w", newline="", encoding="utf-8") as fh:
        writer = csv.DictWriter(fh, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    (OUT / "data" / "inventory.json").write_text(json.dumps(rows, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")

    write_distributed_table(items)
    write_notices(items)
    stats = summarize(items)
    (OUT / "data" / "summary.json").write_text(json.dumps(stats, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(stats, indent=2, ensure_ascii=False))


def shipped(c: Component) -> bool:
    return c.distribution.startswith(("incorporado", "redistribuído"))


def write_distributed_table(items: list[Component]) -> None:
    lines = ["# Componentes distribuídos (gerado)", "",
             "Gerado por `docs/compliance/tools/generate_sbom.py`; não edite à mão. Campos completos (origem, obrigações, evidências,",
             "hashes) em `inventory.csv` / `inventory.json`. W = Windows x64, L = Linux x64.", "",
             "| Componente | Versão | Ecossistema | Dep. | W | L | Licença declarada | Conferência do arquivo | Risco |",
             "| --- | --- | --- | --- | :-: | :-: | --- | --- | --- |"]
    for c in items:
        if not shipped(c):
            continue
        lic = (c.declared or "—").replace("|", "\\|")
        conf = c.status + (f" ({', '.join(c.detected)})" if c.status == "DIVERGE" else "")
        lines.append(f"| {c.name} | {c.version} | {c.ecosystem} | {'direta' if c.direct else 'transitiva'} | "
                     f"{'x' if 'windows-x64' in c.artifacts else ''} | {'x' if 'linux-x64' in c.artifacts else ''} | {lic} | {conf} | {c.risk} |")
    (OUT / "data" / "distributed-components.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def package_dir(c: Component) -> Path | None:
    if not c.evidence or c.ecosystem not in {"npm", "cargo", "nuget", "pypi"} or ":" not in c.key or c.key.startswith(("bin:", "dl:", "ci:")):
        return None
    p = c.evidence[-1]
    path = Path.home() / p[2:] if p.startswith("~/") else ROOT / p
    return path if path.is_dir() else None


# Textos grandes que vão como arquivo à parte (copiados sem alteração de onde o build os obtém).
NOTICE_FILES = [
    ("CPython 3.12.10 (Windows embeddable package)", ROOT / "desktop/host/src-tauri/resources/python/LICENSE.txt", "cpython-3.12.10-LICENSE.txt"),
    (".NET Runtime 10 (LICENSE)", NUGET / "microsoft.netcore.app.runtime.win-x64/10.0.13/LICENSE.TXT", "dotnet-runtime-LICENSE.TXT"),
    (".NET Runtime 10 (THIRD-PARTY-NOTICES)", NUGET / "microsoft.netcore.app.runtime.win-x64/10.0.13/THIRD-PARTY-NOTICES.TXT", "dotnet-runtime-THIRD-PARTY-NOTICES.TXT"),
    ("NSIS (COPYING)", Path(os.environ.get("LOCALAPPDATA", "")) / "tauri/NSIS/COPYING", "nsis-COPYING.txt"),
]


def write_notices(items: list[Component]) -> None:
    notices_dir = OUT / "notices"
    notices_dir.mkdir(exist_ok=True)
    by_text: dict[str, tuple[list[str], list[Component]]] = {}
    missing: list[Component] = []
    for c in items:
        if not shipped(c):
            continue
        directory = package_dir(c)
        if directory is None:
            continue
        files = license_files(directory, recursive=c.ecosystem == "pypi")
        texts = []
        for f in files:
            try:
                texts.append((f.name, f.read_text(encoding="utf-8", errors="replace").strip()))
            except OSError:
                pass
        if not texts:
            missing.append(c)
            continue
        for name, text in texts:
            digest = hashlib.sha256(text.encode("utf-8")).hexdigest()
            entry = by_text.setdefault(digest, ([name, text], []))
            entry[1].append(c)
    copied = []
    for title, source, target in NOTICE_FILES:
        if source.is_file():
            (notices_dir / target).write_bytes(source.read_bytes())
            copied.append((title, target))
    out = ["# Avisos de terceiros — Spock Studio 0.1.0", "",
           "> **Rascunho gerado** por `docs/compliance/tools/generate_sbom.py` a partir dos arquivos de licença distribuídos dentro de cada",
           "> pacote (textos reproduzidos sem alteração). Cobre os componentes incorporados ou redistribuídos nos instaladores de",
           "> Windows x64 e Linux x64. Ver as lacunas na seção final antes de usar este arquivo num lançamento.", "",
           "Este produto contém software de terceiros licenciado sob os termos abaixo. Os direitos autorais pertencem aos respectivos",
           "titulares.", "", "## Textos completos distribuídos como arquivos", ""]
    for title, target in copied:
        out.append(f"- {title}: [`notices/{target}`](notices/{target})")
    out += ["", "## Componentes de pacotes (npm, Cargo, NuGet, PyPI)", ""]
    for digest, ((name, text), comps) in sorted(by_text.items(), key=lambda kv: (kv[1][1][0].name.lower(), kv[0])):
        names = ", ".join(sorted({f"{c.name} {c.version}" for c in comps}))
        out += [f"### {names}", "", f"Arquivo `{name}`:", "", "```text", text.replace("```", "ʼʼʼ"), "```", ""]
    out += ["## Lacunas (PENDENTE)", "",
            "Componentes distribuídos cujo pacote não traz arquivo de licença. O texto precisa ser obtido no repositório oficial,",
            "na versão correspondente, antes do lançamento (não foi redigido aqui):", ""]
    out += [f"- {c.name} {c.version} — declarado `{c.declared}` — origem: {c.origin or 'não informada'}" for c in missing]
    out += ["", "Componentes fora de gerenciador de pacotes cujos textos ainda não foram reunidos:", "",
            "- Bibliotecas nativas do python-build-standalone 20250409 (Linux): licenças no PYTHON.json/`licenses` do arquivo da release.",
            "- Berkeley DB 6.0.19 (Sleepycat), se o `_dbm` continuar no pacote Linux.",
            "- WebKitGTK, GTK, GLib e demais bibliotecas embutidas no AppImage (LGPL e outras): listar a partir do AppImage gerado.",
            "- .NET Runtime para linux-x64: mesmos LICENSE.TXT/THIRD-PARTY-NOTICES.TXT do pacote linux-x64 (o arquivo copiado é o do win-x64).",
            "- nsis_tauri_utils (MIT OR Apache-2.0) e o runtime do AppImage: textos do repositório oficial.", ""]
    (OUT / "THIRD_PARTY_NOTICES.md").write_text("\n".join(out), encoding="utf-8")


def summarize(items: list[Component]) -> dict:
    pm = [c for c in items if c.ecosystem in {"npm", "cargo", "nuget", "pypi"} and not c.key.startswith(("bin:", "dl:", "ext:", "ci:"))]
    shipped = [c for c in items if c.distribution.startswith(("incorporado", "redistribuído"))]
    return {
        "gerado_em": datetime.now(timezone.utc).date().isoformat(),
        "total": len(items),
        "por_ecossistema": dict(Counter(c.ecosystem for c in items)),
        "diretas": sum(c.direct for c in items),
        "transitivas": sum(not c.direct for c in items),
        "distribuidos": len(shipped),
        "distribuidos_por_ecossistema": dict(Counter(c.ecosystem for c in shipped)),
        "sob_demanda": sum(c.environment == "instalação sob demanda" for c in items),
        "externos": sum(c.environment == "externo" for c in items),
        "desenvolvimento_build": sum(c.environment in {"desenvolvimento", "build"} for c in items),
        "nao_instalados": sum(c.environment == "não instalado" for c in items),
        "risco": dict(Counter(c.risk for c in items)),
        "risco_distribuidos": dict(Counter(c.risk for c in shipped)),
        "licencas_distribuidos": dict(Counter(" AND ".join(effective(normalize(c.declared))) or (c.declared or "DESCONHECIDA") for c in shipped).most_common()),
        "licencas_declaradas_todos": dict(Counter(normalize(c.declared) or "DESCONHECIDA" for c in pm).most_common()),
        "conferencia_arquivo": dict(Counter(c.status for c in items)),
        "conferencia_distribuidos": dict(Counter(c.status for c in shipped)),
        "divergentes_distribuidos": sorted(f"{c.name}@{c.version}: declarado {c.declared}, arquivo {', '.join(c.detected)}" for c in shipped if c.status == "DIVERGE"),
        "sem_arquivo_distribuidos": sorted(f"{c.name}@{c.version} ({c.declared})" for c in shipped if c.status in {"SEM ARQUIVO", "TEXTO NÃO RECONHECIDO"}),
        "copyleft_distribuidos": sorted(f"{c.name}@{c.version}: {c.declared}" for c in shipped if any(t in WEAK_COPYLEFT | STRONG_COPYLEFT for t in effective(normalize(c.declared)))),
        "pendentes": sorted(f"{c.name}@{c.version}: {c.risk_reason}" for c in items if c.risk == "PENDENTE"),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="falha se o inventário versionado não bater com os lockfiles atuais")
    args = parser.parse_args()
    inv = Inventory()
    collect_npm(inv)
    collect_cargo(inv)
    collect_nuget(inv)
    collect_python(inv)
    manual(inv)
    for c in inv.items.values():
        classify(c)
    if args.check:
        current = {(r["nome"], r["versao"]) for r in json.loads((OUT / "data" / "inventory.json").read_text(encoding="utf-8"))}
        fresh = {(c.name, c.version) for c in inv.items.values()}
        added, removed = sorted(fresh - current), sorted(current - fresh)
        if added or removed:
            print("Inventário desatualizado.\n  novos: " + ", ".join(f"{n}@{v}" for n, v in added) + "\n  removidos: " + ", ".join(f"{n}@{v}" for n, v in removed))
            sys.exit(1)
        print("Inventário em dia com os lockfiles.")
        return
    write_outputs(inv)


if __name__ == "__main__":
    main()
