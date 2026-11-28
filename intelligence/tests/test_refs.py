import unittest

import _paths  # noqa: F401

from contracts.engineering import (
    CRITERIA_VERSION,
    IMPACT_SCHEMA,
    JUDGMENT_SCHEMA,
    PLAN_SCHEMA,
    Rejected,
    impact_from_model,
    judgment_from_model,
    plan_from_model,
    resolve_refs,
)

ONE = "docs/cobranca.md#cobranca"
KNOWN = {ONE, "docs/arquitetura.md#eventos", "docs/arquitetura.md#fatura"}


def _plan(*refs: str) -> dict:
    return {"schema": PLAN_SCHEMA, "steps": [{"id": "ler", "title": "Ler", "evidence": list(refs)}]}


class ResolveRefsTests(unittest.TestCase):
    def test_id_exato_entre_colchetes_ou_so_com_o_caminho_de_um_trecho_unico_se_resolve(self) -> None:
        self.assertEqual(resolve_refs([ONE], KNOWN), [ONE])
        self.assertEqual(resolve_refs([f"[{ONE}]", f" {ONE} "], KNOWN), [ONE, ONE])
        self.assertEqual(resolve_refs(["docs/cobranca.md"], KNOWN), [ONE])
        self.assertEqual(resolve_refs([], KNOWN), [])

    def test_o_que_nao_aponta_para_um_unico_trecho_fornecido_nao_se_resolve(self) -> None:
        for bad in (["docs/arquitetura.md"], ["docs/inventado.md#x"], ["docs/cobranca.md#outro"], [7], [""], ["[docs/x.md]"]):
            with self.subTest(bad=bad):
                self.assertIsNone(resolve_refs(bad, KNOWN))

    def test_uma_citacao_ruim_derruba_a_lista_inteira(self) -> None:
        self.assertIsNone(resolve_refs([ONE, "docs/inventado.md#x"], KNOWN))


class AgentsAcceptResolvedRefsTests(unittest.TestCase):
    def test_o_plano_aceita_citacao_com_colchetes_e_devolve_o_id_fornecido(self) -> None:
        plan = plan_from_model(_plan(f"[{ONE}]"), KNOWN)

        self.assertEqual(plan["steps"][0]["evidence"], [ONE])

    def test_o_plano_recusa_citacao_ambigua_ou_inventada(self) -> None:
        for ref in ("docs/arquitetura.md", "docs/inventado.md#x"):
            with self.subTest(ref=ref), self.assertRaises(Rejected):
                plan_from_model(_plan(ref), KNOWN)

    def test_o_impacto_aceita_o_caminho_de_um_trecho_unico(self) -> None:
        impact = impact_from_model(
            {"schema": IMPACT_SCHEMA, "areas": ["servico"], "risks": [], "evidence": ["docs/cobranca.md"]}, KNOWN, has_stacks=True)

        self.assertEqual(impact["evidence"], [ONE])

    def test_o_impacto_recusa_id_inventado(self) -> None:
        with self.assertRaises(Rejected):
            impact_from_model({"schema": IMPACT_SCHEMA, "areas": ["servico"], "risks": [], "evidence": ["docs/x.md#y"]}, KNOWN, has_stacks=True)

    def test_o_juiz_aceita_citacao_com_colchetes_e_recusa_o_que_nao_e_lista(self) -> None:
        base = {"schema": JUDGMENT_SCHEMA, "criteriaVersion": CRITERIA_VERSION,
                "scores": {"plan_quality": 4, "context_fit": 4, "request_fit": 4}, "rationale": "ok"}

        self.assertEqual(judgment_from_model({**base, "evidence": [f"[{ONE}]"]}, KNOWN, False)["evidence"], [ONE])
        with self.assertRaises(Rejected):
            judgment_from_model({**base, "evidence": ONE}, KNOWN, False)


if __name__ == "__main__":
    unittest.main()
