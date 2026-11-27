import importlib.util
import tempfile
import unittest
from pathlib import Path

_SPEC = importlib.util.spec_from_file_location("live_campaign", Path(__file__).with_name("live-campaign.py"))
campaign = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(campaign)


class SummaryTests(unittest.TestCase):
    def test_le_o_resumo_em_portugues_e_em_ingles(self) -> None:
        pt = "x\nResumo do teste: total: 6; falhou: 1; bem-sucedido: 4; ignorado: 1; duração: 2s\n"
        en = "Test summary: total: 3; failed: 0; succeeded: 3; skipped: 0; duration: 1s"

        self.assertEqual(campaign.parse_summary(pt), {"total": 6, "failed": 1, "passed": 4, "skipped": 1})
        self.assertEqual(campaign.parse_summary(en), {"total": 3, "failed": 0, "passed": 3, "skipped": 0})

    def test_vale_o_ultimo_resumo_e_sem_resumo_nao_inventa_numero(self) -> None:
        text = "Resumo do teste: total: 1; falhou: 1; bem-sucedido: 0; ignorado: 0\nResumo do teste: total: 2; falhou: 0; bem-sucedido: 2; ignorado: 0"

        self.assertEqual(campaign.parse_summary(text)["passed"], 2)
        self.assertIsNone(campaign.parse_summary("nada"))


class LeakTests(unittest.TestCase):
    def test_acha_o_arquivo_em_que_o_segredo_aparece_e_ignora_segredo_vazio(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            folder = Path(root)
            (folder / "limpo.log").write_text("tudo certo", encoding="utf-8")
            (folder / "sujo.log").write_text("a chave era SEGREDO-123 mesmo", encoding="utf-8")

            self.assertEqual([path.name for path in campaign.leaked(folder, ["SEGREDO-123"])], ["sujo.log"])
            self.assertEqual(campaign.leaked(folder, [""]), [])


class ReportTests(unittest.TestCase):
    def test_o_relatorio_traz_contagens_e_marca_vazamento(self) -> None:
        run = {"label": "evals-openai", "code": 0, "seconds": 12, "summary": {"total": 1, "failed": 0, "passed": 1, "skipped": 0}, "lines": ["[evals de gestão] x"]}
        with tempfile.TemporaryDirectory() as root:
            out = Path(root)

            text = campaign.write_report(out, ["evals"], [run], [out / "a.log"]).read_text(encoding="utf-8")

            self.assertIn("| evals-openai | ok | 1 | 0 | 0 | 12 |", text)
            self.assertIn("[evals de gestão] x", text)
            self.assertIn("ATENÇÃO", text)


if __name__ == "__main__":
    unittest.main()
