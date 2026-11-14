import json
import tempfile
import unittest

import _paths  # noqa: F401

from agents import planning, prompts
from contracts.messages import PREVIEW_SCHEMA, ContractError, parse_preview
from knowledge import retrieval
from workflows.engineering.preview import PREVIEW_ACK, preview

PROJECT = "p-aaaaaaaaaaaa"
GOAL = "Corrigir o calculo de frete do carrinho"


def _payload(root: str, sources: list[dict], goal: str = GOAL) -> dict:
    return {
        "schema": PREVIEW_SCHEMA,
        "projectId": PROJECT,
        "goal": goal,
        "context": {"stacks": ["dotnet"], "knowledgeRoot": root, "sources": sources},
    }


def _source(path: str, letter: str, text: str) -> dict:
    return {"path": path, "hash": letter * 64, "text": text}


class PreviewTests(unittest.TestCase):
    def test_mostra_as_mensagens_exatas_que_o_planejamento_mandaria(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            request = parse_preview(_payload(root, [_source("src/frete.cs", "a", "calculo de frete do carrinho com desconto")]))
            result = preview(request)
            hits = retrieval.search(root, PROJECT, GOAL)

        self.assertEqual(result["schema"], PREVIEW_ACK)
        self.assertEqual([call["agentId"] for call in result["calls"]], ["planning", "architecture"])
        self.assertEqual(result["calls"][0]["messages"], planning.messages(GOAL, ["dotnet"], hits))
        self.assertEqual(result["calls"][0]["maxOutputTokens"], planning.MAX_OUTPUT_TOKENS)
        self.assertEqual(result["evidence"][0]["filePath"], "src/frete.cs")
        self.assertTrue(result["evidence"][0]["included"])
        self.assertEqual(result["index"], {"documents": 1, "pending": 0, "valid": True})

    def test_o_que_passa_do_bloco_fica_marcado_como_fora_e_o_bloco_nao_muda(self) -> None:
        long = "frete " * 400
        evidence = [{"chunkId": f"src/{n}.cs#1", "excerpt": long} for n in range(12)]

        entries = prompts.entries(evidence)

        self.assertTrue(all(entry["truncated"] for entry in entries))
        included = [entry for entry in entries if entry["included"]]
        self.assertTrue(0 < len(included) < len(entries))
        self.assertTrue(all(not entry["included"] for entry in entries[len(included):]))
        self.assertEqual(prompts.project_block(evidence).count("[src/"), len(included))
        self.assertLessEqual(sum(len(entry["text"]) for entry in included), prompts.MAX_BLOCK)

    def test_fonte_ainda_nao_indexada_aparece_como_pendente(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            result = preview(parse_preview(_payload(root, [{"path": "src/novo.cs", "hash": "b" * 64}])))

        self.assertEqual(result["index"]["pending"], 1)
        self.assertEqual(result["evidence"], [])
        self.assertIn("nenhum trecho do projeto foi recuperado", json.dumps(result["calls"][0]["messages"], ensure_ascii=False))

    def test_recusa_pedido_fora_do_contrato(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            good = _payload(root, [])
            for broken in (
                {**good, "schema": "x"},
                {**good, "projectId": "../p"},
                {**good, "goal": ""},
                {**good, "context": {**good["context"], "knowledgeRoot": ""}},
                {**good, "context": {**good["context"], "sources": [{"path": "a", "hash": "curto"}]}},
            ):
                with self.assertRaises(ContractError):
                    parse_preview(broken)


if __name__ == "__main__":
    unittest.main()
