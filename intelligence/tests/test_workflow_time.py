import unittest

import _paths  # noqa: F401
from support_model import FakeModel, impact, plan, with_model
from test_graph import _codes, _run

from workflows.engineering import graph
from workflows.engineering.state import MAX_MILLISECONDS_MODEL


class _Clock:
    def __init__(self) -> None:
        self.now = 0.0

    def __call__(self) -> float:
        return self.now


class _SlowModel:
    """Cada chamada leva ``latency`` segundos do relógio do teste, como um modelo de topo; o resto do fluxo não gasta tempo."""

    def __init__(self, fake: FakeModel, clock: _Clock, latency: float) -> None:
        self.fake, self.clock, self.latency = fake, clock, latency

    def complete(self, *args, **kwargs):
        self.clock.now += self.latency
        return self.fake.complete(*args, **kwargs)


def _go(latency: float, *replies: object, **fields) -> tuple[dict, FakeModel]:
    clock = _Clock()
    fake = FakeModel(*replies)
    slow = _SlowModel(fake, clock, latency)
    _, ack = _run(graph.run_start(with_model(**fields), clock=clock, model_factory=lambda _request: slow))
    return ack, fake


class SlowModelTests(unittest.TestCase):
    """Um modelo lento não pode jogar fora a execução: o que já veio fica, o resto segue determinístico e a limitação diz por quê."""

    def test_modelo_lento_nao_derruba_a_execucao_e_as_etapas_seguintes_ficam_sem_modelo(self) -> None:
        # A chamada do plano termina aos 100 s, depois dos 90 s: o plano do modelo vale; arquitetura e juiz nem começam.
        ack, fake = _go(100, plan(("a", "A")), impact(["servico"], [], []), impact=True)

        codes = _codes(ack)
        self.assertEqual(ack["status"], "completed")
        self.assertEqual([step["id"] for step in ack["output"]["plan"]["steps"]], ["a"])
        self.assertEqual(len(fake.calls), 1)
        self.assertTrue({"MODEL_TIME_EXCEEDED", "IMPACT_MODEL_FALLBACK", "JUDGE_NOT_RUN"} <= codes)
        self.assertNotIn("LIMIT_TIME", codes)
        self.assertNotIn("MODEL_BUDGET_EXCEEDED", codes)

    def test_dentro_do_tempo_todas_as_etapas_usam_o_modelo(self) -> None:
        # 10 s por chamada: plano, arquitetura (com a segunda tentativa) e juiz começam todos antes dos 90 s.
        ack, fake = _go(10, plan(("a", "A")), impact(["servico"], [], []), impact=True)

        codes = _codes(ack)
        self.assertEqual([call["purpose"] for call in fake.calls][:2], ["plan", "architecture"])
        self.assertNotIn("MODEL_TIME_EXCEEDED", codes)
        self.assertNotIn("LIMIT_TIME", codes)
        self.assertEqual(ack["status"], "completed")

    def test_o_teto_de_tempo_do_runtime_cobre_modelo_lento(self) -> None:
        self.assertGreaterEqual(MAX_MILLISECONDS_MODEL, 90000)


if __name__ == "__main__":
    unittest.main()
