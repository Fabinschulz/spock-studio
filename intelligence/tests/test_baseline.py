import json
import tempfile
import unittest
from pathlib import Path

import _paths  # noqa: F401

from agents.model_client import ModelError
from support_model import FakeModel, with_model
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


_BASELINE_MODEL = _BASELINE.with_name("baseline-model.json")


class ModelBaselineTests(unittest.TestCase):
    """O grafo com modelo (falso e roteirizado) continua igual à baseline: planos, agentes, limitações e chamadas."""

    def test_o_grafo_com_modelo_continua_igual_a_baseline(self) -> None:
        document = json.loads(_BASELINE_MODEL.read_text(encoding="utf-8"))
        self.assertEqual(document["schema"], "engineering.baseline.model.v1")
        for case in document["cases"]:
            with tempfile.TemporaryDirectory() as tmp:
                chunk = _chunk(tmp)
                replies = [ModelError(r) if isinstance(r, str) else _fill(r, chunk) for r in case["replies"]]
                model = FakeModel(*replies)
                payload = with_model(tmp, impact=case["impact"])
                ack = list(graph.run_start(payload, model_factory=lambda _request: model))[-1]

            codes = {item["code"] for item in ack["warnings"] + ack["errors"]}
            steps = [step["id"] for step in ack["output"]["plan"]["steps"]]
            self.assertEqual(ack["status"], "completed", case["id"])
            self.assertEqual(steps, case["steps"], case["id"])
            self.assertEqual([item["agentId"] for item in ack["agents"]], case["agents"], case["id"])
            self.assertTrue(set(case["limitations"]).issubset(codes), case["id"])
            self.assertFalse(set(case["absent"]) & codes, case["id"])
            self.assertEqual(ack["usage"]["modelCalls"], case["calls"], case["id"])
            self.assertEqual(ack["usage"]["costUsd"], 0, case["id"])
            self.assertEqual(ack["output"]["evaluation"]["verdict"], "deterministic", case["id"])


def _fill(value: object, chunk: str) -> object:
    """Troca o marcador '$CHUNK' pelo id real do trecho recuperado, em qualquer nível."""
    if isinstance(value, str):
        return chunk if value == "$CHUNK" else value
    if isinstance(value, list):
        return [_fill(item, chunk) for item in value]
    if isinstance(value, dict):
        return {key: _fill(item, chunk) for key, item in value.items()}
    return value


def _chunk(root: str) -> str:
    payload = with_model(root)
    payload["policy"] = {"allowedActions": [], "modelAllowed": False}
    del payload["broker"]
    return list(graph.run_start(payload))[-1]["output"]["evidence"][0]["chunkId"]


if __name__ == "__main__":
    unittest.main()
