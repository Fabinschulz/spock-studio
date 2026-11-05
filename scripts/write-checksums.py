#!/usr/bin/env python3
"""Grava SHA256SUMS.txt com o hash de cada instalador que o Tauri gerou.

O instalador do Windows não é assinado na primeira versão (decisão de 2026-11-05), então a pessoa que o baixa precisa de outro
jeito de saber que o arquivo é o que a CI gerou. O formato é o do ``sha256sum``: ``<hash>  <nome>``, conferível com
``sha256sum -c SHA256SUMS.txt`` no Linux e com ``Get-FileHash`` no Windows.

  write-checksums.py <pasta bundle do Tauri>
"""

from __future__ import annotations

import hashlib
import sys
from pathlib import Path

INSTALLERS = ("*.exe", "*.msi", "*.deb", "*.AppImage")
SUMS = "SHA256SUMS.txt"


def main() -> int:
    if len(sys.argv) != 2 or not Path(sys.argv[1]).is_dir():
        print(__doc__, file=sys.stderr)
        return 2
    bundle = Path(sys.argv[1])
    files = sorted({path for pattern in INSTALLERS for path in bundle.rglob(pattern) if path.is_file()})
    if not files:
        print(f"Nenhum instalador em {bundle}.", file=sys.stderr)
        return 1

    lines = []
    for path in files:
        digest = hashlib.sha256()
        with path.open("rb") as handle:
            for block in iter(lambda: handle.read(1 << 20), b""):
                digest.update(block)
        # Só o nome do arquivo: quem confere baixa os instaladores para a mesma pasta, sem os subdiretórios do bundle.
        lines.append(f"{digest.hexdigest()}  {path.name}")
    (bundle / SUMS).write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print("\n".join(lines))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
