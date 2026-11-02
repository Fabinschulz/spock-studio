"""Coloca a inteligência e as dependências instaladas (scripts/package-intelligence.py dev) no caminho dos testes."""

import os
import sys
from pathlib import Path

os.environ["LANGSMITH_TRACING"] = "false"
os.environ["LANGCHAIN_TRACING_V2"] = "false"

ROOT = Path(__file__).resolve().parents[1]
for path in (ROOT / "packages", ROOT):
    if str(path) not in sys.path:
        sys.path.insert(0, str(path))
