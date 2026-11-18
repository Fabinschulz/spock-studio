import contextlib
import copy
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import _paths  # noqa: F401
from support_model import FakeModel

from contracts.investigation import parse_groups
from evaluations import management as evals
from workflows.investigation.nodes import focus

DOCUMENT = evals.load_dataset()
CASES = {case["id"]: case for case in DOCUMENT["cases"]}


def _scripted(case: dict) -> FakeModel:
    return FakeModel(*case["reference"])


def _reference_acks() -> dict[str, dict]:
    return evals.run_dataset(DOCUMENT, _scripted)


def _score(acks: dict[str, dict]) -> dict:
    return evals.score_dataset(DOCUMENT, acks)


def _case_score(report: dict, case_id: str) -> dict:
    return next(item for item in report["cases"] if item["id"] == case_id)


class DatasetTests(unittest.TestCase):
    def test_o_dataset_se_sustenta(self) -> None:
        self.assertGreaterEqual(len(DOCUMENT["cases"]), 10)
        self.assertEqual(len({case["id"] for case in DOCUMENT["cases"]}), len(DOCUMENT["cases"]))
        for name, facts in DOCUMENT["scenarios"].items():
            ids = [fact["id"] for fact in facts]
            self.assertEqual(len(ids), len(set(ids)), name)
        for case in DOCUMENT["cases"]:
            ids = {fact["id"] for fact in DOCUMENT["scenarios"][case["scenario"]]}
            groups = {evals.plain(fact["group"]) for fact in DOCUMENT["scenarios"][case["scenario"]]}
            self.assertLessEqual(set(case["mustCite"]), ids, case["id"])
            self.assertLessEqual({evals.plain(group) for group in case["focus"]}, groups, case["id"])
            looked = {evals.plain(group) for group in focus(case["question"], [fact["group"] for fact in DOCUMENT["scenarios"][case["scenario"]]], parse_groups(evals.fact_groups()))}
            self.assertLessEqual({evals.plain(group) for group in case["focus"]}, looked, case["id"])

    def test_ha_perguntas_com_resposta_e_sem_resposta_nos_fatos(self) -> None:
        self.assertTrue(any(case["unanswerable"] for case in DOCUMENT["cases"]))
        self.assertTrue(any(not case["unanswerable"] for case in DOCUMENT["cases"]))

    def test_o_dataset_nao_traz_travessao(self) -> None:
        text = (Path(evals.__file__).parent / "datasets" / "management.json").read_text(encoding="utf-8")
        self.assertNotIn(chr(0x2014), text)


class NumbersTests(unittest.TestCase):
    def test_numeros_sao_comparados_como_o_conferente_do_motor(self) -> None:
        self.assertEqual(evals.numbers("média 3,38 por semana"), evals.numbers("media 3.38"))
        self.assertEqual(evals.numbers("05 e 5,0"), {"5"})
        self.assertEqual(evals.numbers("até 2026-12-15"), {"2026", "12", "15"})


class ReferenceTests(unittest.TestCase):
    def test_a_resposta_de_referencia_de_cada_caso_pontua_um_e_passa_os_minimos(self) -> None:
        report = _score(_reference_acks())

        self.assertTrue(report["passed"], evals.format_report(report))
        for metric in ("framing", "grounding", "coverage", "abstention"):
            self.assertEqual(report["means"][metric], 1.0, metric)
        self.assertFalse(any(item["failed"] for item in report["cases"]))
        self.assertEqual(sum(item["rejected"] for item in report["cases"]), 0)

    def test_sem_modelo_o_enquadramento_e_a_ancoragem_valem_e_a_abstencao_tambem(self) -> None:
        report = _score(evals.run_dataset(DOCUMENT))

        self.assertEqual(report["means"]["framing"], 1.0, evals.format_report(report))
        self.assertEqual(report["means"]["grounding"], 1.0)
        self.assertEqual(report["means"]["abstention"], 1.0)
        # A cobertura é o que o modelo acrescenta: sem ele, o mínimo de cobertura não é atingido.
        self.assertFalse(report["verdict"]["coverage"]["ok"])
        self.assertFalse(report["passed"])


class NegativeControlTests(unittest.TestCase):
    """Um pontuador que não pega o erro não serve: cada defeito plantado precisa derrubar a nota certa."""

    def setUp(self) -> None:
        self.acks = _reference_acks()

    def _mutate(self, case_id: str, change) -> dict:
        acks = copy.deepcopy(self.acks)
        change(acks[case_id])
        return _case_score(_score(acks), case_id)

    def test_numero_inventado_derruba_a_ancoragem(self) -> None:
        score = self._mutate("riscos-principais", lambda ack: ack["answer"]["observed"][0].update(text="Há 99 itens bloqueados."))

        self.assertLess(score["grounding"], 1.0)

    def test_afirmacao_sem_citacao_derruba_a_ancoragem(self) -> None:
        score = self._mutate("riscos-principais", lambda ack: ack["answer"]["observed"][0].update(evidence=[]))

        self.assertLess(score["grounding"], 1.0)

    def test_citar_fato_que_existe_mas_nao_foi_consultado_derruba_a_ancoragem(self) -> None:
        score = self._mutate("riscos-principais", lambda ack: ack["answer"]["observed"][0].update(evidence=["milestone.1"]))

        self.assertLess(score["grounding"], 1.0)

    def test_nao_citar_o_fato_essencial_derruba_a_cobertura(self) -> None:
        def drop(ack: dict) -> None:
            for section in ("observed", "calculations", "inferences", "recommendations"):
                for claim in ack["answer"][section]:
                    claim["evidence"] = [fact for fact in claim["evidence"] if fact != "risk.blocked"]
            ack["answer"]["summary"]["evidence"] = ["flow.issues.throughput"]

        score = self._mutate("queda-de-vazao", drop)

        self.assertEqual(score["coverage"], 0.5)

    def test_inventar_inferencia_onde_os_fatos_nao_tem_derruba_a_abstencao(self) -> None:
        score = self._mutate(
            "mais-gente-resolve",
            lambda ack: ack["answer"]["inferences"].append({"text": "Mais pessoas resolveriam.", "evidence": ["missing.capacity"]}),
        )

        self.assertEqual(score["abstention"], 0.0)

    def test_nao_dizer_que_esta_indisponivel_derruba_a_abstencao(self) -> None:
        score = self._mutate("cycle-time-nao-calculado", lambda ack: ack["answer"].update(unavailable=[]))

        self.assertEqual(score["abstention"], 0.0)

    def test_olhar_o_lugar_errado_derruba_o_enquadramento(self) -> None:
        score = self._mutate("riscos-principais", lambda ack: ack["path"][0].update(groups=["contexto", "fontes"]))

        self.assertEqual(score["framing"], 0.0)

    def test_investigacao_que_falhou_pontua_zero_no_que_depende_da_resposta(self) -> None:
        def fail(ack: dict) -> None:
            ack.update(status="failed", answer=None, failure="LIMIT_TOKENS")

        score = self._mutate("queda-de-vazao", fail)

        self.assertTrue(score["failed"])
        self.assertEqual((score["grounding"], score["coverage"]), (0.0, 0.0))

    def test_caso_sem_resultado_conta_como_falha_e_a_rodada_nao_passa(self) -> None:
        acks = copy.deepcopy(self.acks)
        del acks["gargalo-do-fluxo"]

        report = _score(acks)

        self.assertTrue(_case_score(report, "gargalo-do-fluxo")["failed"])
        self.assertFalse(report["passed"])


class ScoreFileTests(unittest.TestCase):
    def _run(self, acks: dict) -> tuple[int, str]:
        with tempfile.TemporaryDirectory() as root:
            path = Path(root) / "acks.json"
            path.write_text(json.dumps(acks, ensure_ascii=False), encoding="utf-8")
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                code = evals.main(["score", str(path)])
        return code, out.getvalue()

    def test_a_rodada_boa_sai_com_zero_e_a_ruim_com_um(self) -> None:
        good = _reference_acks()
        bad = copy.deepcopy(good)
        for ack in bad.values():
            ack["answer"]["summary"]["evidence"] = []

        code_good, report_good = self._run(good)
        code_bad, report_bad = self._run(bad)

        self.assertEqual(code_good, 0)
        self.assertIn("resultado: passou", report_good)
        self.assertEqual(code_bad, 1)
        self.assertIn("ABAIXO DO MÍNIMO", report_bad)

    def test_uso_errado_sai_com_dois(self) -> None:
        err = io.StringIO()
        with contextlib.redirect_stderr(err):
            self.assertEqual(evals.main([]), 2)
            self.assertEqual(evals.main(["score"]), 2)

    def test_o_relatorio_nao_traz_texto_de_resposta(self) -> None:
        _code, report = self._run(_reference_acks())

        self.assertNotIn("Desbloquear", report)
        self.assertNotIn("itens abertos", report)


class BaselineTests(unittest.TestCase):
    def setUp(self) -> None:
        self._root = tempfile.TemporaryDirectory()
        self.addCleanup(self._root.cleanup)
        self.folder = Path(self._root.name)
        patcher = mock.patch.object(evals, "_BASELINES", self.folder)
        patcher.start()
        self.addCleanup(patcher.stop)

    def _write(self, name: str, acks: dict) -> Path:
        path = self.folder / name
        path.write_text(json.dumps(acks, ensure_ascii=False), encoding="utf-8")
        return path

    def _main(self, *argv: str) -> tuple[int, str]:
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = evals.main(list(argv))
        return code, out.getvalue() + err.getvalue()

    def test_a_linha_de_base_guarda_so_numeros_e_ids_de_caso(self) -> None:
        results = self._write("management-eval-openai.json", _reference_acks())

        code, _output = self._main("baseline", str(results))

        self.assertEqual(code, 0)
        stored = json.loads((self.folder / "management-baseline-openai.json").read_text(encoding="utf-8"))
        self.assertEqual(stored["provider"], "openai")
        self.assertEqual(stored["dataset"], evals.dataset_digest())
        self.assertEqual({case["id"] for case in stored["cases"]}, set(CASES))
        text = json.dumps(stored, ensure_ascii=False)
        self.assertNotIn("Desbloquear", text)
        self.assertNotIn("itens abertos", text)
        for case in DOCUMENT["cases"]:
            self.assertNotIn(case["question"], text)

    def test_rodada_com_caso_que_falhou_nao_vira_linha_de_base(self) -> None:
        acks = _reference_acks()
        acks.pop(next(iter(acks)))
        results = self._write("management-eval-openai.json", acks)

        code, output = self._main("baseline", str(results))

        self.assertEqual(code, 1)
        self.assertIn("rode de novo", output)
        self.assertFalse((self.folder / "management-baseline-openai.json").exists())

    def test_a_pontuacao_compara_com_a_linha_de_base_do_provedor_do_arquivo(self) -> None:
        good = _reference_acks()
        self._main("baseline", str(self._write("management-eval-anthropic.json", good)))
        bad = copy.deepcopy(good)
        for ack in bad.values():
            ack["answer"]["summary"]["evidence"] = []

        code_same, report_same = self._main("score", str(self._write("management-eval-anthropic.json", good)))
        code_bad, report_bad = self._main("score", str(self._write("management-eval-anthropic.json", bad)))

        self.assertEqual(code_same, 0)
        self.assertIn("linha de base de anthropic", report_same)
        self.assertNotIn("PIOROU", report_same)
        self.assertEqual(code_bad, 1)
        self.assertIn("PIOROU", report_bad)

    def test_variacao_dentro_da_tolerancia_nao_e_regressao(self) -> None:
        report = evals.score_dataset(DOCUMENT, _reference_acks())
        baseline = evals.make_baseline(report, "openai", "2026-11-17", evals.dataset_digest())
        lower = copy.deepcopy(report)
        lower["means"]["grounding"] = round(report["means"]["grounding"] - evals.BASELINE_TOLERANCE / 2, 4)

        comparison = evals.compare_to_baseline(lower, baseline, evals.dataset_digest())

        self.assertTrue(comparison["applies"])
        self.assertFalse(comparison["regressed"])

    def test_linha_de_base_de_outro_dataset_nao_se_aplica(self) -> None:
        report = evals.score_dataset(DOCUMENT, _reference_acks())
        baseline = evals.make_baseline(report, "openai", "2026-11-17", "0" * 64)

        comparison = evals.compare_to_baseline(report, baseline, evals.dataset_digest())

        self.assertFalse(comparison["applies"])
        self.assertFalse(comparison["regressed"])
        self.assertIn("outro dataset", evals.format_comparison("openai", comparison))

    def test_sem_linha_de_base_a_pontuacao_diz_como_gravar(self) -> None:
        code, report = self._main("score", str(self._write("management-eval-openai.json", _reference_acks())))

        self.assertEqual(code, 0)
        self.assertIn("nenhuma ainda", report)

    def test_provedor_invalido_ou_ausente_sai_com_dois(self) -> None:
        results = self._write("acks.json", _reference_acks())

        self.assertEqual(self._main("baseline", str(results))[0], 2)
        self.assertEqual(self._main("baseline", str(results), "../fora")[0], 2)
        self.assertFalse(any(self.folder.glob("management-baseline-*")))


if __name__ == "__main__":
    unittest.main()
