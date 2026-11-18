import json
import tempfile
import unittest

import _paths  # noqa: F401
from support_model import FakeModel, plan, with_model
from test_graph import _codes, _run
from test_investigation import _conclusion, _hypotheses, _searching, _with_memory
from test_investigation import _go as _investigate

from agents import prompts
from agents.runner import BUDGET, ask, input_estimate
from contracts.messages import PREVIEW_SCHEMA, parse_preview
from knowledge import index
from knowledge.chunking import MAX_CHUNKS
from workflows.engineering import graph
from workflows.engineering.preview import preview

PROJECT = "p-aaaaaaaaaaaa"
TAIL = "A fatura do mês fecha no dia cinco e o boleto vence no dia quinze."
BODY = "O módulo de cobrança recorrente fatura assinaturas. " + "Detalhe operacional do faturamento. " * 12 + TAIL
SOURCE = {"path": "docs/cobranca.md", "hash": "c" * 64, "text": f"# Cobrança\n\n{BODY}\n"}


def _engineering(root: str, model: FakeModel) -> tuple[list[dict], dict]:
    payload = with_model(root)
    payload["context"]["sources"] = [dict(SOURCE)]
    return _run(graph.run_start(payload, model_factory=lambda _request: model))


class AgentContextTests(unittest.TestCase):
    def test_o_planejamento_recebe_o_trecho_inteiro_e_nao_so_o_resumo_da_busca(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            model = FakeModel(plan(("ler", "Ler o módulo de cobrança")))

            _events, ack = _engineering(root, model)

        self.assertGreater(len(BODY), 160)
        self.assertIn(TAIL, model.calls[0]["messages"][1]["content"])
        self.assertTrue(all(len(item["excerpt"]) <= 160 for item in ack["output"]["evidence"]))

    def test_o_corpo_do_trecho_nao_vai_para_o_ack_nem_para_os_eventos(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            events, ack = _engineering(root, FakeModel(plan(("ler", "Ler"))))

        self.assertNotIn(TAIL, json.dumps(ack, ensure_ascii=False))
        self.assertNotIn(TAIL, json.dumps(events, ensure_ascii=False))

    def test_a_previa_mostra_o_mesmo_trecho_inteiro_que_o_agente_recebe(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            request = parse_preview({
                "schema": PREVIEW_SCHEMA,
                "projectId": PROJECT,
                "goal": "Como funciona o faturamento da cobrança?",
                "context": {"stacks": [], "knowledgeRoot": root, "sources": [dict(SOURCE)]},
            })
            result = preview(request)

        self.assertIn(TAIL, result["calls"][0]["messages"][1]["content"])
        self.assertIn(TAIL, result["evidence"][0]["text"])
        self.assertFalse(result["evidence"][0]["truncated"])

    def test_sem_corpo_conhecido_vale_o_resumo_da_busca(self) -> None:
        evidence = [{"chunkId": "docs/a.md#intro", "excerpt": "resumo"}]

        self.assertEqual(prompts.with_bodies(evidence, {}), evidence)
        self.assertEqual(prompts.with_bodies(evidence, {"docs/a.md#intro": "corpo"})[0]["excerpt"], "corpo")


class InputReservationTests(unittest.TestCase):
    def test_chamada_cuja_entrada_estouraria_o_teto_nem_sai(self) -> None:
        model = FakeModel(plan(("ler", "Ler")))
        messages = [{"role": "user", "content": "x" * 900}]

        outcome = ask(model, agent_id="planning", purpose="plan", messages=messages, max_tokens=1024,
                      response_schema="engineering.plan.v1", parse=lambda document: document, can_spend=lambda needed: needed <= 1100)

        self.assertEqual((outcome.code, model.calls), (BUDGET, []))
        self.assertEqual(input_estimate(messages), 300)


class InvestigationMemoryTests(unittest.TestCase):
    def test_o_fato_da_memoria_traz_mais_que_o_resumo_da_busca(self) -> None:
        decision = ("@decision/d-1.md", f"# Adiar a migração\n\n{BODY}\n")
        with tempfile.TemporaryDirectory() as root:
            fake = FakeModel(_searching("cobrança faturamento"), _hypotheses([("H", ["memory.1"])]), _conclusion())

            _investigate(_with_memory(root, decision), fake)

        prompt = fake.calls[1]["messages"][1]["content"]
        fact = next(line for line in prompt.splitlines() if line.startswith("[memory.1]"))
        self.assertGreater(len(fact), 300)


class TruncatedFileTests(unittest.TestCase):
    def test_arquivo_cortado_no_limite_de_trechos_vira_limitacao(self) -> None:
        sections = "\n\n".join(f"## Seção {n}\n\nCobrança da parte {n}." for n in range(MAX_CHUNKS + 10))
        big = {"path": "docs/grande.md", "hash": "d" * 64, "text": f"# Grande\n\n{sections}\n"}
        with tempfile.TemporaryDirectory() as root:
            stats = index.sync(root, PROJECT, [big, dict(SOURCE)])
            payload = with_model(root)
            payload["context"]["sources"] = [big, dict(SOURCE)]
            _events, ack = _run(graph.run_start(payload, model_factory=lambda _request: FakeModel(plan(("ler", "Ler")))))

        self.assertEqual(stats["truncated"], 1)
        self.assertIn("INDEX_FILES_TRUNCATED", _codes(ack))


if __name__ == "__main__":
    unittest.main()
