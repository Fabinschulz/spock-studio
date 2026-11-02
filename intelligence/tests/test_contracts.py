import json
import socket
import tempfile
import unittest
from pathlib import Path

import _paths  # noqa: F401

from workflows.engineering import graph

CONTRACTS = Path(__file__).resolve().parents[2] / "contracts"
SCHEMAS = CONTRACTS / "schemas" / "intelligence"
FIXTURES = CONTRACTS / "fixtures" / "intelligence"


def _payload(root: str, **input_fields) -> dict:
    return {
        "schema": "ai.workflow.start.v2",
        "workflowId": graph.WORKFLOW_ID,
        "workflowVersion": graph.WORKFLOW_VERSION,
        "runId": "run-dddddddddddd",
        "projectId": "p-dddddddddddd",
        "executionId": "ex-dddddddddddd",
        "input": {"goal": "descrever o módulo", **input_fields},
        "context": {"available": True, "stacks": ["dotnet", "typescript"], "knowledgeRoot": root, "executionRoot": root,
                    "sources": [{"path": "docs/modulo.md", "hash": "a" * 64, "text": "Descrever o módulo de faturamento recorrente."}]},
        "limits": {"maxIterations": 8, "maxMilliseconds": 10000, "maxTokens": 0, "maxCostUsd": 0},
        "policy": {"allowedActions": [], "modelAllowed": False},
    }


def _schema(name: str) -> dict:
    return json.loads((SCHEMAS / f"{name}.schema.json").read_text(encoding="utf-8"))


class ContractTests(unittest.TestCase):
    """A validação completa por JSON Schema roda nos testes do motor; aqui fica o que a stdlib confere."""

    def test_schemas_da_fronteira_se_identificam_pelo_nome_do_arquivo(self) -> None:
        for path in SCHEMAS.glob("*.schema.json"):
            schema = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual(schema["$id"], f"https://mariliz.dev/schemas/intelligence/{path.name}", path.name)

    def test_toda_fixture_aponta_para_um_schema_existente(self) -> None:
        names = {path.name[: -len(".schema.json")] for path in SCHEMAS.glob("*.schema.json")}
        for kind in ("valid", "invalid"):
            for path in (FIXTURES / kind).glob("*.json"):
                self.assertIn(path.stem.split("--", 1)[0], names, path.name)

    def test_fixtures_validas_declaram_o_proprio_schema(self) -> None:
        for path in (FIXTURES / "valid").glob("*.json"):
            body = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual(body["schema"], path.stem.split("--", 1)[0], path.name)

    def test_saida_do_grafo_usa_so_campos_dos_schemas(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            messages = list(graph.run_start(_payload(tmp, impact=True, requiresApproval=True)))
            paused = messages[-1]
            resumed = list(graph.run_resume({
                "schema": "ai.workflow.resume.v2", "runId": paused["runId"], "executionId": paused["executionId"],
                "projectId": paused["projectId"], "interruptId": paused["approval"]["interruptId"],
                "decision": {"approvalId": "apr-dddddddddddd", "approved": True, "actionIds": []},
                "context": {"executionRoot": tmp},
            }))
        ack_schema = _schema("ai.ack.v2")
        event_schema = _schema("ai.event.v1")
        for ack in (paused, resumed[-1]):
            self.assertTrue(set(ack_schema["required"]) <= set(ack), ack["status"])
            self.assertTrue(set(ack) <= set(ack_schema["properties"]), ack["status"])
            self.assertTrue(set(ack["output"]) <= set(ack_schema["properties"]["output"]["properties"]))
        self.assertEqual(paused["status"], "interrupted")
        self.assertEqual(resumed[-1]["status"], "completed")
        for event in messages[:-1] + resumed[:-1]:
            self.assertEqual(set(event) - {"agentId"}, set(event_schema["required"]))
            self.assertIn(event["type"], event_schema["properties"]["type"]["enum"])

        plan = resumed[-1]["output"]["plan"]
        impact = resumed[-1]["output"]["impact"]
        self.assertEqual(set(plan), set(_schema("engineering.plan.v1")["required"]))
        impact_schema = _schema("engineering.impact.v1")
        self.assertEqual(set(impact), set(impact_schema["required"]))
        self.assertTrue(set(impact["areas"]) <= set(impact_schema["properties"]["areas"]["items"]["enum"]))

    def test_execucao_nao_abre_conexao_fora_do_loopback(self) -> None:
        opened: list[object] = []
        original = socket.socket.connect

        def spy(sock, address):
            opened.append(address)
            return original(sock, address)

        socket.socket.connect = spy
        try:
            with tempfile.TemporaryDirectory() as tmp:
                list(graph.run_start(_payload(tmp, impact=True)))
        finally:
            socket.socket.connect = original
        self.assertEqual([address for address in opened if not str(address[0]).startswith("127.")], [])


if __name__ == "__main__":
    unittest.main()
