import datetime
import re
import tempfile
import unittest
from pathlib import Path

import _paths  # noqa: F401

from evaluations import history
from evaluations import references as reference_eval


class ReferenceEvalTests(unittest.TestCase):
    def test_todo_padrao_esperado_existe_no_corpus(self) -> None:
        document, sources = reference_eval.load()
        known = {match for source in sources for match in re.findall(r"\| (REF-[A-Z]+\d+) \|", source["text"])}

        missing = {ref for case in document["cases"] for ref in case["expect"]} - known

        self.assertEqual(missing, set())

    def test_recall_e_contaminacao_atingem_a_meta(self) -> None:
        document, sources = reference_eval.load()

        report = reference_eval.run(document, sources)

        metrics, target = report["metrics"], document["target"]
        self.assertGreaterEqual(metrics["recallAtK"], target["recallAtK"], report["cases"])
        self.assertLessEqual(metrics["contaminationAtK"], target["contaminationAtK"], report["cases"])
        # Sem o filtro de stack, seções de outra tecnologia aparecem: é o que o filtro evita.
        self.assertGreater(metrics["contaminationAtKUnfiltered"], 0)

    def test_historico_guarda_uma_linha_por_medicao_com_a_data(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            path = Path(root) / "history.jsonl"
            history.record("references", {"recallAtK": 0.9}, path, datetime.date(2026, 11, 19))
            history.record("architecture", {"recall": 1.0}, path, datetime.date(2026, 11, 19))
            history.record("references", {"recallAtK": 1.0}, path, datetime.date(2026, 11, 20))

            entries = history.read("references", path)

        self.assertEqual([entry["measuredAt"] for entry in entries], ["2026-11-19", "2026-11-20"])
        self.assertEqual(entries[-1]["metrics"], {"recallAtK": 1.0})

    def test_historico_versionado_so_tem_linhas_validas(self) -> None:
        for entry in history.read("references") + history.read("architecture"):
            self.assertRegex(entry["measuredAt"], r"^\d{4}-\d{2}-\d{2}$")
            self.assertIsInstance(entry["metrics"], dict)


if __name__ == "__main__":
    unittest.main()
