import tempfile
import unittest

import _paths  # noqa: F401

from evaluations import recall as recall_eval


class RecallEvalTests(unittest.TestCase):
    def test_recall_at_k_conta_acertos_no_top_k(self) -> None:
        self.assertEqual(recall_eval.recall_at_k(["a#1", "b#1", "c#1"], ["b#1"], 2), 1.0)
        self.assertEqual(recall_eval.recall_at_k(["a#1", "b#1"], ["c#1"], 2), 0.0)

    def test_dataset_lexical_atende_a_baseline_antes_de_embeddings(self) -> None:
        document = recall_eval.load_dataset()
        min_recall = float(document["minMeanRecall"])
        with tempfile.TemporaryDirectory() as root:
            report = recall_eval.run_dataset(root, document)
        self.assertGreaterEqual(report["meanRecall"], min_recall, report)
        for item in report["cases"]:
            self.assertGreaterEqual(item["recall"], min_recall, item["id"])


if __name__ == "__main__":
    unittest.main()
