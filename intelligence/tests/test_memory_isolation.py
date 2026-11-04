"""Fase 5: cada memória grava só no seu lugar, o checkpoint some ao fim da execução e nenhum SQLite guarda segredo."""

import re
import tempfile
import unittest
from pathlib import Path

import _paths  # noqa: F401

from knowledge import index as rag
from test_graph import EXECUTION, PROJECT, RUN, _resume, _run, _start
from workflows.engineering import graph

# Mesmos formatos de credencial do SecretScanner do motor (Mariliz.Core/Policy/SecretScanner.cs).
_SECRET_PATTERNS = [
    re.compile(rb"\bAKIA[0-9A-Z]{16}\b"),
    re.compile(rb"\bgh[pousr]_[A-Za-z0-9]{30,}\b"),
    re.compile(rb"\bxox[abprs]-[A-Za-z0-9-]{10,}\b"),
    re.compile(rb"-----BEGIN (?:RSA |EC |OPENSSH |DSA |PGP )?PRIVATE KEY"),
    re.compile(rb"\beyJ[A-Za-z0-9_-]{8,}\.eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}"),
    re.compile(rb"\b[a-z][a-z0-9+.-]*://[^/\s:@]+:[^/\s@]{3,}@[^/\s]+", re.IGNORECASE),
]
_FAKE_KEY = b"AKIA" + b"ABCDEFGHIJKLMNOP"
OTHER = "p-bbbbbbbbbbbb"


def _secret_rules(path: Path) -> list[int]:
    data = path.read_bytes()
    return [index for index, pattern in enumerate(_SECRET_PATTERNS) if pattern.search(data)]


def _sqlite_files(root: str) -> list[Path]:
    return sorted(Path(root).rglob("*.sqlite"))


class MemoryIsolationTests(unittest.TestCase):
    def test_cada_memoria_grava_so_na_propria_pasta(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            _run(graph.run_start(_start(tmp, requiresApproval=True)))
            rag.sync(tmp, PROJECT, [{"path": "docs/a.md", "hash": "a" * 64, "text": "texto longo o bastante para indexar"}])

            base = Path(tmp) / "projects" / PROJECT
            found = {path.relative_to(tmp).as_posix() for path in _sqlite_files(tmp)}
            self.assertEqual(found, {f"projects/{PROJECT}/execution/checkpoint.sqlite",
                                     f"projects/{PROJECT}/knowledge/index.sqlite"})
            self.assertTrue((base / "execution").is_dir())
            self.assertFalse((Path(tmp) / "projects" / OTHER).exists())
            graph.run_cancel(RUN, PROJECT, tmp)

    def test_checkpoint_some_quando_a_execucao_termina(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            _, paused = _run(graph.run_start(_start(tmp, requiresApproval=True)))
            self.assertEqual(len(_sqlite_files(tmp)), 1)
            _, done = _run(graph.run_resume(_resume(tmp, paused["approval"]["interruptId"])))
            self.assertEqual(done["status"], "completed")
            self.assertEqual(_sqlite_files(tmp), [])

    def test_checkpoint_some_quando_a_execucao_e_cancelada(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            _run(graph.run_start(_start(tmp, requiresApproval=True)))
            self.assertEqual(len(_sqlite_files(tmp)), 1)
            graph.run_cancel(RUN, PROJECT, tmp)
            self.assertEqual(_sqlite_files(tmp), [])

    def test_saida_de_agente_nao_entra_no_indice_de_conhecimento(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            sources = [{"path": "docs/a.md", "hash": "a" * 64, "text": "texto longo o bastante para indexar"}]
            rag.sync(tmp, PROJECT, sources)
            before = rag.manifest(tmp, PROJECT)

            _, paused = _run(graph.run_start(_start(tmp, requiresApproval=True)))
            _run(graph.run_resume(_resume(tmp, paused["approval"]["interruptId"])))

            self.assertEqual(rag.manifest(tmp, PROJECT), before)
            self.assertEqual(rag.search(tmp, PROJECT, "decisão arquitetural plano módulo cobrança"), [])

    def test_nenhum_sqlite_guarda_segredo(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            _run(graph.run_start(_start(tmp, requiresApproval=True)))
            rag.sync(tmp, PROJECT, [{"path": "docs/a.md", "hash": "a" * 64, "text": "documentação comum sem credenciais"}])
            files = _sqlite_files(tmp)
            self.assertEqual(len(files), 2)
            for path in files:
                self.assertEqual(_secret_rules(path), [], path.name)
            graph.run_cancel(RUN, PROJECT, tmp)

    def test_varredura_enxerga_segredo_plantado(self) -> None:
        """Controle negativo: sem este teste, a varredura acima poderia passar por estar quebrada."""
        with tempfile.TemporaryDirectory() as tmp:
            rag.sync(tmp, PROJECT, [{"path": "docs/a.md", "hash": "a" * 64, "text": "x " * 20 + _FAKE_KEY.decode()}])
            (path,) = _sqlite_files(tmp)
            self.assertEqual(_secret_rules(path), [0])


if __name__ == "__main__":
    unittest.main()
