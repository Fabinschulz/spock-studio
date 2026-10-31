import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from workflows.engineering import graph

_BASELINE = Path(__file__).resolve().parents[1] / "evaluations" / "datasets" / "baseline.json"


class BaselineTests(unittest.TestCase):
    def test_o_grafo_continua_igual_a_baseline(self) -> None:
        document = json.loads(_BASELINE.read_text(encoding="utf-8"))
        self.assertEqual(document["schema"], "engineering.baseline.v1")
        for case in document["cases"]:
            payload = {
                "runId": "run-cccccccccccc",
                "projectId": "p-cccccccccccc",
                "executionId": "ex-1",
                "input": {"goal": "descrever o módulo de cobrança", "impact": case["impact"]},
                "context": {"stacks": ["dotnet"]},
            }
            ack = graph.run_start(payload)
            codes = {item["code"] for item in ack["limitations"]}
            steps = [step["id"] for step in ack["output"]["plan"]["steps"]]
            agents = [item["id"] for item in ack["output"]["agents"]]
            self.assertEqual(ack["status"], "completed", case["id"])
            self.assertEqual(steps, case["steps"], case["id"])
            self.assertEqual(agents, case["agents"], case["id"])
            self.assertTrue(set(case["limitations"]).issubset(codes), case["id"])
            self.assertEqual(ack["output"]["evaluation"]["verdict"], "deterministic", case["id"])
            graph.run_cancel("run-cccccccccccc", "p-cccccccccccc")
