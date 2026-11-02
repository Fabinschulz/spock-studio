import json
import unittest
from pathlib import Path

import _paths  # noqa: F401

from workflows.engineering import graph

_BASELINE = Path(__file__).resolve().parents[1] / "evaluations" / "datasets" / "baseline.json"


class BaselineTests(unittest.TestCase):
    def test_o_grafo_continua_igual_a_baseline(self) -> None:
        document = json.loads(_BASELINE.read_text(encoding="utf-8"))
        self.assertEqual(document["schema"], "engineering.baseline.v1")
        for case in document["cases"]:
            payload = {
                "schema": "ai.workflow.start.v2",
                "workflowId": graph.WORKFLOW_ID,
                "workflowVersion": graph.WORKFLOW_VERSION,
                "runId": "run-cccccccccccc",
                "projectId": "p-cccccccccccc",
                "executionId": "ex-cccccccccccc",
                "input": {"goal": "descrever o módulo de cobrança", "impact": case["impact"]},
                "context": {"available": True, "stacks": ["dotnet"]},
                "limits": {"maxIterations": 8, "maxMilliseconds": 10000, "maxTokens": 0, "maxCostUsd": 0},
                "policy": {"allowedActions": [], "modelAllowed": False},
            }
            ack = list(graph.run_start(payload))[-1]
            codes = {item["code"] for item in ack["warnings"] + ack["errors"]}
            steps = [step["id"] for step in ack["output"]["plan"]["steps"]]
            agents = [item["agentId"] for item in ack["agents"]]
            self.assertEqual(ack["status"], "completed", case["id"])
            self.assertEqual(steps, case["steps"], case["id"])
            self.assertEqual(agents, case["agents"], case["id"])
            self.assertTrue(set(case["limitations"]).issubset(codes), case["id"])
            self.assertEqual(ack["output"]["evaluation"]["verdict"], "deterministic", case["id"])


if __name__ == "__main__":
    unittest.main()
