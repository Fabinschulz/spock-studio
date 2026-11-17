#!/usr/bin/env python3
"""Baixa, dos repositórios oficiais, o texto de licença dos componentes distribuídos cujo pacote não traz o arquivo.

Cada texto é fixado num commit: o do nuspec quando o pacote o declara, a tag da versão quando existe, ou o commit da branch
padrão no dia da consulta. O resultado vai para docs/compliance/notices/upstream/ com um sources.json que diz de onde veio
cada arquivo (URL, commit e SHA-256) e quais componentes ele cobre. Os textos não são editados.

  python docs/compliance/tools/fetch_upstream_licenses.py
"""

from __future__ import annotations

import hashlib
import json
import subprocess
import sys
import urllib.request
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
TARGET = ROOT / "docs" / "compliance" / "notices" / "upstream"

# (id, repositório, ref ou None para a branch padrão, arquivos candidatos, chaves cobertas: prefixo "ecossistema:nome")
SOURCES = [
    ("aws-sdk-net", "aws/aws-sdk-net", None, [["License.txt", "LICENSE"], ["Notice.txt", "NOTICE.txt", "NOTICE"]], ["nuget:AWSSDK."]),
    ("azure-sdk-for-net", "Azure/azure-sdk-for-net", "c94768fafa4dd93eef9f7c90c92522434fcedb32", [["LICENSE.txt", "LICENSE"]],
     ["nuget:Azure.Core", "nuget:Azure.Identity", "nuget:Azure.ResourceManager", "nuget:System.ClientModel"]),
    ("dotnet", "dotnet/dotnet", "c2435c3e0f46de784341ac3ed62863ce77e117b4", [["LICENSE.TXT", "LICENSE.txt", "LICENSE"]],
     ["nuget:Microsoft.Bcl.", "nuget:Microsoft.Extensions.", "nuget:System.Memory.Data", "nuget:System.Formats.Cbor", "nuget:System.IO.Hashing"]),
    ("msal-dotnet", "AzureAD/microsoft-authentication-library-for-dotnet", "d5d7de6b103f0d9dd7bca9bf13cbb9f3da37bc9f", [["LICENSE", "LICENSE.txt"]],
     ["nuget:Microsoft.Identity.Client"]),
    ("identitymodel", "AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet", "c8f7d87bcda35557a68f6cb9c55856a2ee733856",
     [["LICENSE.txt", "LICENSE"]], ["nuget:Microsoft.IdentityModel."]),
    ("yamldotnet", "aaubry/YamlDotNet", "748334a8fa7c227740018b284b71ad95cc6b7fc7", [["LICENSE.txt", "LICENSE"]], ["nuget:YamlDotNet"]),
    ("alloc-no-stdlib", "dropbox/rust-alloc-no-stdlib", None, [["LICENSE", "LICENSE.md", "LICENSE.txt"]], ["cargo:alloc-stdlib"]),
    ("dlopen2", "OpenByteDev/dlopen2", None, [["LICENSE", "LICENSE-MIT", "LICENSE.md"]], ["cargo:dlopen2"]),
    ("libappindicator-rs", "tauri-apps/libappindicator-rs", None, [["LICENSE-MIT", "LICENSE_MIT"], ["LICENSE-APACHE", "LICENSE_APACHE-2.0"]],
     ["cargo:libappindicator-sys"]),
    ("webview2-rs", "wravery/webview2-rs", None, [["LICENSE", "LICENSE-MIT", "LICENSE.md"]], ["cargo:webview2-com"]),
    ("mariliz-ui-kit", "Fabinschulz/mariliz-ui-kit", None, [["LICENSE", "LICENSE.md", "LICENSE.txt"]], ["npm:@mariliz/ui-kit"]),
    ("sqlite-vec", "asg017/sqlite-vec", "v0.1.9", [["LICENSE-MIT"], ["LICENSE-APACHE"]], ["pypi:sqlite-vec"]),
    ("nsis-tauri-utils", "tauri-apps/nsis-tauri-utils", None, [["LICENSE_MIT"], ["LICENSE_APACHE-2.0"]], ["bin:nsis-tauri-utils"]),
    # O pacote declara Apache-2.0 sem trazer o texto; o repositório não tem NOTICE (ADR-0009).
    ("mcp-csharp-sdk", "modelcontextprotocol/csharp-sdk", "v2.4.0", [["LICENSE", "LICENSE.txt"]], ["nuget:ModelContextProtocol"]),
]


# Sem arquivo de licença no repositório (os fontes trazem só o cabeçalho da licença): o texto oficial publicado pelo titular dela.
OFFICIAL_TEXTS = [
    ("mpl-2.0", "https://www.mozilla.org/media/MPL/2.0/index.txt", "MPL-2.0.txt",
     ["cargo:selectors"], "servo/stylo não tem arquivo de licença; os fontes de selectors têm o cabeçalho da MPL 2.0"),
]


def gh(path: str) -> str:
    return subprocess.run(["gh", "api", path, "-q", ".sha"], check=True, capture_output=True, text=True).stdout.strip()


def resolve(repo: str, ref: str | None) -> str:
    """Commit exato: tag/branch viram SHA para o texto não mudar com o tempo."""
    return gh(f"repos/{repo}/commits/{ref or 'HEAD'}")


def fetch(repo: str, commit: str, candidates: list[str]) -> tuple[str, bytes] | None:
    for name in candidates:
        url = f"https://raw.githubusercontent.com/{repo}/{commit}/{name}"
        try:
            with urllib.request.urlopen(url, timeout=30) as response:
                return url, response.read()
        except Exception:  # noqa: BLE001 - tenta o próximo nome
            continue
    return None


def main() -> int:
    TARGET.mkdir(parents=True, exist_ok=True)
    manifest, failures = [], []
    for source_id, repo, ref, groups, covers in SOURCES:
        try:
            commit = resolve(repo, ref)
        except subprocess.CalledProcessError:
            failures.append(f"{source_id}: ref {ref or 'padrão'} não encontrada em {repo}")
            continue
        for candidates in groups:
            found = fetch(repo, commit, candidates)
            if found is None:
                failures.append(f"{source_id}: nenhum de {candidates} em {repo}@{commit[:12]}")
                continue
            url, data = found
            file_name = f"{source_id}-{url.rsplit('/', 1)[1]}"
            (TARGET / file_name).write_bytes(data)
            manifest.append({"id": source_id, "file": file_name, "repository": f"https://github.com/{repo}", "ref": ref or "branch padrão",
                             "commit": commit, "url": url, "sha256": hashlib.sha256(data).hexdigest(), "covers": covers})
            print(f"ok {file_name} ({repo}@{commit[:12]})")
    for source_id, url, name, covers, reason in OFFICIAL_TEXTS:
        try:
            with urllib.request.urlopen(url, timeout=30) as response:
                data = response.read()
        except Exception as error:  # noqa: BLE001
            failures.append(f"{source_id}: {error}")
            continue
        file_name = f"{source_id}-{name}"
        (TARGET / file_name).write_bytes(data)
        manifest.append({"id": source_id, "file": file_name, "url": url, "sha256": hashlib.sha256(data).hexdigest(), "covers": covers,
                         "note": reason})
        print(f"ok {file_name} ({url})")
    (TARGET / "sources.json").write_text(json.dumps({"retrieved": date.today().isoformat(), "files": manifest}, indent=2) + "\n",
                                         encoding="utf-8")
    for failure in failures:
        print("FALHA", failure)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
