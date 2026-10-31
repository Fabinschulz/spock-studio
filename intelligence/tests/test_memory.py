import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from memory.execution import ExecutionError, delete, load, save


class ExecutionMemoryTests(unittest.TestCase):
    def test_checkpoint_fica_fora_do_indice_e_nao_cruza_projeto(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            first = "p-aaaaaaaaaaaa"
            second = "p-bbbbbbbbbbbb"
            run = "run-aaaaaaaaaaaa"
            save(str(root), first, run, {"goal": "alfa-mariliz-91", "paused": True})

            self.assertFalse((root / first / "index.sqlite").exists())
            checkpoint = root / "projects" / first / "execution" / "checkpoint.sqlite"
            self.assertTrue(checkpoint.is_file())
            self.assertIn(b"alfa-mariliz-91", checkpoint.read_bytes())
            import sqlite3

            conn = sqlite3.connect(checkpoint)
            try:
                tables = [row[0] for row in conn.execute("SELECT name FROM sqlite_master WHERE type = 'table'")]
            finally:
                conn.close()
            self.assertEqual(tables, ["checkpoints"])
            self.assertEqual(load(str(root), first, run)["goal"], "alfa-mariliz-91")
            self.assertIsNone(load(str(root), second, run))
            self.assertFalse((root / "projects" / second / "execution" / "checkpoint.sqlite").exists())

            delete(str(root), first, run)
            self.assertFalse(checkpoint.exists())
            self.assertIsNone(load(str(root), first, run))

    def test_recusa_id_e_caminho_que_escapam_da_raiz(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(ExecutionError):
                save(tmp, "../p-aaaaaaaaaaaa", "run-aaaaaaaaaaaa", {})
            with self.assertRaises(ExecutionError):
                save(tmp, "p-aaaaaaaaaaaa", "../run-aaaaaaaaaaaa", {})
            with self.assertRaises(ExecutionError):
                load(tmp, "p-1", "run-aaaaaaaaaaaa")
