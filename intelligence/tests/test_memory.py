import sqlite3
import tempfile
import unittest
from pathlib import Path

import _paths  # noqa: F401

from memory.execution import ExecutionError, checkpoint_file, drop, saver

RUN = "run-aaaaaaaaaaaa"


def _write(root: str, project: str, goal: str) -> None:
    with saver(root, project, RUN) as checkpoints:
        config = {"configurable": {"thread_id": RUN, "checkpoint_ns": ""}}
        checkpoint = {"v": 1, "id": "1", "ts": "2026-11-02T00:00:00+00:00", "channel_values": {"goal": goal},
                      "channel_versions": {}, "versions_seen": {}, "pending_sends": []}
        checkpoints.put(config, checkpoint, {}, {})


class ExecutionMemoryTests(unittest.TestCase):
    def test_checkpoint_fica_fora_do_indice_e_nao_cruza_projeto(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            first = "p-aaaaaaaaaaaa"
            second = "p-bbbbbbbbbbbb"
            _write(tmp, first, "alfa-mariliz-91")

            self.assertFalse((root / first / "index.sqlite").exists())
            checkpoint = root / "projects" / first / "execution" / "checkpoint.sqlite"
            self.assertTrue(checkpoint.is_file())
            self.assertIn(b"alfa-mariliz-91", checkpoint.read_bytes())
            conn = sqlite3.connect(checkpoint)
            try:
                tables = {row[0] for row in conn.execute("SELECT name FROM sqlite_master WHERE type = 'table'")}
            finally:
                conn.close()
            self.assertNotIn("chunks_fts", tables)
            self.assertIn("checkpoints", tables)
            self.assertFalse((root / "projects" / second / "execution" / "checkpoint.sqlite").exists())

            drop(tmp, first, RUN)
            self.assertFalse(checkpoint.exists())

    def test_recusa_id_e_caminho_que_escapam_da_raiz(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(ExecutionError):
                checkpoint_file(tmp, "../p-aaaaaaaaaaaa")
            with self.assertRaises(ExecutionError):
                with saver(tmp, "p-aaaaaaaaaaaa", "../run-aaaaaaaaaaaa"):
                    pass
            with self.assertRaises(ExecutionError):
                checkpoint_file(tmp, "p-1")
            with self.assertRaises(ExecutionError):
                checkpoint_file("", "p-aaaaaaaaaaaa")


if __name__ == "__main__":
    unittest.main()
