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


class KeyTests(unittest.TestCase):
    def test_chave_que_nao_cabe_num_cabecalho_e_recusada_sem_ser_impressa(self) -> None:
        self.assertIsNone(campaign.key_format_problem("chave-falsa-de-teste-0000"))
        for bad in ("", "tem espaco", "quebra\nde linha", "com\ttab", "acentuação-ç", "fim\r"):
            with self.subTest(bad=bad):
                problem = campaign.key_format_problem(bad)
                self.assertIsNotNone(problem)
                if bad:
                    self.assertNotIn(bad, problem or "")

    def test_o_pre_voo_usa_o_mesmo_cabecalho_do_transporte(self) -> None:
        url, headers = campaign.preflight_request("anthropic", "K", "https://gw.exemplo/")
        self.assertEqual((url, headers["x-api-key"]), ("https://gw.exemplo/v1/models", "K"))
        url, headers = campaign.preflight_request("openai", "K", None)
        self.assertEqual((url, headers["Authorization"]), ("https://api.openai.com/v1/models", "Bearer K"))
        self.assertEqual(campaign.preflight_request("anthropic", "K", None)[0], "https://api.anthropic.com/v1/models")


class ProbeTests(unittest.TestCase):
    def test_a_sonda_faz_a_mesma_chamada_do_transporte(self) -> None:
        url, headers, body = campaign.completion_request("anthropic", "K", "https://gw.exemplo/", "m1")
        self.assertEqual((url, headers["x-api-key"], body["model"]), ("https://gw.exemplo/v1/messages", "K", "m1"))
        self.assertNotIn("response_format", body)
        url, headers, body = campaign.completion_request("openai", "K", None, "m2")
        self.assertEqual((url, headers["Authorization"], body["response_format"]), ("https://api.openai.com/v1/chat/completions", "Bearer K", {"type": "json_object"}))

    def test_descreve_so_a_forma_da_resposta_da_anthropic_com_bloco_de_pensamento_e_texto_com_cerca(self) -> None:
        document = {"model": "claude-x", "stop_reason": "end_turn", "usage": {"input_tokens": 9, "output_tokens": 30},
                    "content": [{"type": "thinking", "thinking": "raciocínio"}, {"type": "text", "text": "```json\n{\"a\":1}\n```"}]}

        shape = campaign.describe_reply("anthropic", document)

        self.assertEqual(shape["blocos"], ["thinking:10", "text:19"])
        self.assertTrue(shape["json"].startswith("não é JSON"))
        self.assertEqual((shape["modelo"], shape["parou"]), ("claude-x", "end_turn"))

    def test_descreve_a_resposta_da_openai_e_reconhece_json_valido(self) -> None:
        document = {"model": "gpt-x", "choices": [{"finish_reason": "stop", "message": {"content": "{\"a\":1,\"b\":[]}"}}], "usage": {"total_tokens": 5}}

        shape = campaign.describe_reply("openai", document)

        self.assertEqual(shape["json"], "JSON ok, chaves ['a', 'b']")
        self.assertEqual(shape["parou"], "stop")


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
