import tempfile
import unittest

import _paths  # noqa: F401

from evaluations import recall as recall_eval


class RecallEvalTests(unittest.TestCase):
    def test_metricas_contam_a_posicao_do_primeiro_acerto(self) -> None:
        self.assertEqual(recall_eval.recall_at_k([False, True, False], 2), 1.0)
        self.assertEqual(recall_eval.recall_at_k([False, False, True], 2), 0.0)
        self.assertEqual(recall_eval.reciprocal_rank([False, False, True]), 1 / 3)
        self.assertEqual(recall_eval.reciprocal_rank([False, False]), 0.0)

    def test_recuperacao_nao_piora_em_relacao_a_baseline(self) -> None:
        document = recall_eval.load_dataset()
        baseline = document["baseline"]
        with tempfile.TemporaryDirectory() as root:
            report = recall_eval.run_dataset(root, document)
        self.assertGreaterEqual(report["recallAtK"], baseline["recallAtK"], report)
        self.assertGreaterEqual(report["mrr"], baseline["mrr"], report)


if __name__ == "__main__":
    unittest.main()
