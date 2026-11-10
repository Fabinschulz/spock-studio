"""Fase 7b: o agente só propõe a intenção. Quem escolhe o alvo, aprova e executa é o motor, e o que não foi proposto não se aprova."""

import json
import tempfile
import unittest
from pathlib import Path

import _paths  # noqa: F401
from support_model import SOURCE
from test_graph import _codes, _resume, _run, _start

from contracts.engineering import decision_intent, forbidden, intents_ok
from workflows.engineering import graph


def _payload(root: str, *, allowed=("file.write",), approval=True, impact=True, sources=True) -> dict:
    payload = _start(root, impact=impact, requiresApproval=approval)
    payload["policy"] = {"allowedActions": list(allowed), "modelAllowed": False}
    if sources:
        payload["context"]["knowledgeRoot"] = root
        payload["context"]["sources"] = [dict(SOURCE)]
    return payload


def _paused(tmp: str, **options) -> tuple[list[dict], dict]:
    return _run(graph.run_start(_payload(tmp, **options)))


class ActionProposalTests(unittest.TestCase):
    def test_com_politica_pausa_e_impacto_o_agente_propoe_so_a_intencao(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            events, ack = _paused(tmp)

        self.assertEqual(ack["status"], "interrupted")
        (action,) = ack["nextActions"]
        # Sem alvo, resumo, tipo de escrita nem exigência de aprovação: isso é decisão do motor.
        self.assertEqual(set(action), {"id", "intent", "evidence"})
        self.assertEqual(action["intent"], "record-decision")
        self.assertNotIn(".mariliz", json.dumps(action))
        self.assertEqual(ack["approval"]["actionIds"], [action["id"]])
        self.assertTrue(set(action["evidence"]) <= {item["chunkId"] for item in ack["output"]["evidence"]})
        self.assertNotIn("content", json.dumps(action))
        self.assertFalse(forbidden(action))
        self.assertTrue(intents_ok([action], ack["output"]["evidence"]))

    def test_sem_a_politica_do_motor_nenhuma_acao_e_proposta(self) -> None:
        for allowed in ((), ("command.run",), ("config.change", "external.write")):
            with tempfile.TemporaryDirectory() as tmp:
                _, ack = _paused(tmp, allowed=allowed)
            self.assertEqual(ack["nextActions"], [], allowed)
            self.assertEqual(ack["approval"]["actionIds"], [], allowed)

    def test_sem_pausa_para_aprovacao_ou_sem_impacto_nao_ha_acao(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            _, no_pause = _paused(tmp, approval=False)
            _, no_impact = _paused(tmp, impact=False)

        self.assertEqual(no_pause["status"], "completed")
        self.assertEqual(no_pause["nextActions"], [])
        self.assertEqual(no_impact["nextActions"], [])

    def test_o_modelo_nao_consegue_trazer_acao_junto_com_o_impacto(self) -> None:
        from agents.model_client import ModelError  # noqa: F401
        from contracts.engineering import Rejected, impact_from_model

        sneaky = {"schema": "engineering.impact.v1", "areas": ["servico"], "risks": [], "evidence": ["a#1"],
                  "nextActions": [{"id": "act-x", "kind": "command.run", "commandId": "rm"}]}
        with self.assertRaises(Rejected):
            impact_from_model(sneaky, {"a#1"}, True)


class ApprovalBindingTests(unittest.TestCase):
    def _resume(self, tmp: str, ack: dict, action_ids: list[str], **over) -> dict:
        message = _resume(tmp, ack["approval"]["interruptId"])
        message["decision"]["actionIds"] = action_ids
        message.update(over)
        return list(graph.run_resume(message))[-1]

    def test_aprovar_a_acao_proposta_ou_nenhuma_retoma_a_execucao(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            _, ack = _paused(tmp)
            action_id = ack["nextActions"][0]["id"]
            done = self._resume(tmp, ack, [action_id])
            self.assertEqual(done["status"], "completed")

        with tempfile.TemporaryDirectory() as tmp:
            _, ack = _paused(tmp)
            self.assertEqual(self._resume(tmp, ack, [])["status"], "completed")

    def test_aprovar_acao_que_nao_foi_proposta_nao_retoma_e_a_pausa_continua(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            _, ack = _paused(tmp)
            refused = self._resume(tmp, ack, ["act-inventada"])
            self.assertEqual(refused["status"], "failed")
            self.assertIn("APPROVAL_MISMATCH", _codes(refused))

            # A pausa continua valendo: a aprovação certa ainda retoma.
            self.assertEqual(self._resume(tmp, ack, [ack["nextActions"][0]["id"]])["status"], "completed")

    def test_sem_aprovar_nada_a_execucao_nao_continua(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            _, ack = _paused(tmp)
            message = _resume(tmp, ack["approval"]["interruptId"])
            message["decision"]["approved"] = False

            declined = list(graph.run_resume(message))[-1]

        self.assertEqual(declined["status"], "interrupted")
        self.assertEqual(declined["approval"]["actionIds"], [ack["nextActions"][0]["id"]])

    def test_checkpoint_com_acao_adulterada_nao_retoma(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            _, ack = _paused(tmp)
            db = next(Path(tmp).rglob("checkpoint.sqlite"))
            self.assertTrue(db.exists())

        # A validação do estado restaurado recusa cada adulteração.
        good = decision_intent({"evidence": [ack["output"]["evidence"][0]["chunkId"]]})
        evidence = ack["output"]["evidence"]
        bad = [
            {**good, "command": "rm -rf /"},
            {**good, "intent": "command.run"},
            {**good, "intent": "file.write"},
            {**good, "id": "act-Maiuscula"},
            {**good, "target": ".mariliz/decisions/ex-ffffffffffff-impacto.md"},
            {**good, "kind": "file.write", "summary": "x", "requiresApproval": True},
            {**good, "evidence": ["docs/inventado.md#9"]},
            {**good, "content": "# conteúdo escrito pelo agente"},
        ]
        for action in bad:
            self.assertFalse(intents_ok([action], evidence), str(action))
        self.assertFalse(intents_ok([good, dict(good)], evidence))

    def test_acao_nenhuma_gravada_no_checkpoint_em_texto_livre(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            _paused(tmp)
            blob = next(Path(tmp).rglob("checkpoint.sqlite")).read_bytes()
        # O estado guarda só a intenção (id, tipo e evidências): sem comando e sem conteúdo de arquivo.
        self.assertIn(b"act-decisao-impacto", blob)
        self.assertNotIn(b"rm -rf", blob)
        self.assertNotIn(b'"content"', blob)


if __name__ == "__main__":
    unittest.main()
