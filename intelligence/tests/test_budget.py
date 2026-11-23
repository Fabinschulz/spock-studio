from __future__ import annotations

import unittest

import _paths  # noqa: F401
from support_files import TemporaryDirectory
from test_graph import EXECUTION, PROJECT, RUN, _resume, _run, _start

from infrastructure.events import Emitter
from memory.execution import saver
from workflows.engineering import graph
from workflows.engineering.budget import budgeted
from workflows.engineering.state import (
    MAX_ITERATIONS,
    MAX_MILLISECONDS_MODEL,
    MAX_TOKENS,
    Segment,
    segment_from_state,
)


def _segment(clock_values: list[float], **limits) -> Segment:
    values = iter(clock_values)
    return Segment(Emitter(EXECUTION), clock=lambda: next(values), **limits)


class SegmentFromStateTests(unittest.TestCase):
    def test_usa_os_limites_do_checkpoint(self) -> None:
        state = {"limits": {"iterations": 3, "milliseconds": 1500, "tokens": 900}}
        segment = segment_from_state(Emitter(EXECUTION), None, state)
        self.assertEqual((segment.max_iterations, segment.max_milliseconds, segment.max_tokens), (3, 1500, 900))

    def test_checkpoint_antigo_sem_limites_vale_o_teto_do_runtime(self) -> None:
        segment = segment_from_state(Emitter(EXECUTION), None, {})
        self.assertEqual(
            (segment.max_iterations, segment.max_milliseconds, segment.max_tokens),
            (MAX_ITERATIONS, MAX_MILLISECONDS_MODEL, MAX_TOKENS),
        )

    def test_checkpoint_do_disco_nao_amplia_o_teto_nem_aceita_lixo(self) -> None:
        state = {"limits": {"iterations": 10**9, "milliseconds": -1, "tokens": True}}
        segment = segment_from_state(Emitter(EXECUTION), None, state)
        self.assertEqual(
            (segment.max_iterations, segment.max_milliseconds, segment.max_tokens),
            (MAX_ITERATIONS, MAX_MILLISECONDS_MODEL, MAX_TOKENS),
        )


class BudgetedTests(unittest.TestCase):
    def test_no_depois_do_plano_nao_conta_etapa_mas_respeita_o_tempo(self) -> None:
        # started = 0; o relógio lido depois do nó marca 5 s, acima de 1 s.
        segment = _segment([0.0, 5.0], max_milliseconds=1000)
        state = budgeted(segment, lambda s: s, counts_iteration=False)({"iterations": 4, "limitations": []})
        self.assertEqual(state["iterations"], 4)
        self.assertEqual(state["failure"], "LIMIT_TIME")

    def test_no_depois_do_plano_respeita_os_tokens(self) -> None:
        segment = _segment([0.0, 0.0], max_tokens=100)
        state = budgeted(segment, lambda s: s, counts_iteration=False)({"tokens": 101, "limitations": []})
        self.assertEqual(state["failure"], "LIMIT_TOKENS")

    def test_no_de_analise_continua_contando_etapa(self) -> None:
        segment = _segment([0.0, 0.0], max_iterations=1)
        state = budgeted(segment, lambda s: s)({"iterations": 1, "limitations": []})
        self.assertEqual(state["iterations"], 2)
        self.assertEqual(state["failure"], "LIMIT_ITERATIONS")


class ResumeLimitsTests(unittest.TestCase):
    def test_a_pausa_guarda_os_limites_e_a_retomada_conclui_dentro_deles(self) -> None:
        with TemporaryDirectory() as tmp:
            _, paused = _run(graph.run_start(_start(tmp, requiresApproval=True)))
            config = {"configurable": {"thread_id": RUN}}
            with saver(tmp, PROJECT, RUN) as checkpoints:
                values = graph.build(Segment(Emitter(EXECUTION))).compile(checkpointer=checkpoints).get_state(config).values
            self.assertEqual(values["limits"], {"iterations": 8, "milliseconds": 10000, "tokens": 0})

            _, done = _run(graph.run_resume(_resume(tmp, paused["approval"]["interruptId"])))
            self.assertEqual(done["status"], "completed")


if __name__ == "__main__":
    unittest.main()
