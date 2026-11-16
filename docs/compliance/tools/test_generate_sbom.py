"""Testes do avaliador de política e da detecção de licenças do generate_sbom.py (sem rede e sem gerenciadores de pacote).

  python -m unittest discover -s docs/compliance/tools -p "test_*.py" -v
"""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import generate_sbom as g  # noqa: E402

POLICY = json.loads(g.POLICY_FILE.read_text(encoding="utf-8"))


def component(key: str, declared: str | None, *, distribution: str = "incorporado (teste)", environment: str = "execução",
              direct: bool = True) -> g.Component:
    prefix, _, rest = key.partition(":")
    name, _, version = rest.rpartition("@")
    return g.Component(key, name, version, prefix, "library", declared=declared, distribution=distribution,
                       environment=environment, direct=direct)


def evaluate(*components: g.Component, policy: dict = POLICY) -> dict[str, dict]:
    inv = g.Inventory()
    for c in components:
        inv.add(c)
    return {r["componente"]: r for r in g.evaluate_policy(inv, policy)}


class PolicyTests(unittest.TestCase):
    def test_permissiva_distribuida_passa(self):
        self.assertEqual("OK", evaluate(component("npm:pacote-mit@1.0.0", "MIT"))["pacote-mit"]["status"])

    def test_gpl_distribuida_e_proibida_e_bloqueia(self):
        result = evaluate(component("cargo:copyleft@0.1.0", "GPL-3.0-only"))["copyleft"]
        self.assertEqual("PROIBIDA", result["status"])
        self.assertIn(result["status"], g.BLOCKING)

    def test_or_com_ramo_permissivo_usa_o_permissivo(self):
        self.assertEqual("OK", evaluate(component("cargo:dual@1.0.0", "GPL-2.0-only OR MIT"))["dual"]["status"])

    def test_licenca_ausente_em_distribuido_e_desconhecida(self):
        self.assertEqual("DESCONHECIDA", evaluate(component("npm:sem-licenca@2.0.0", None))["sem-licenca"]["status"])

    def test_mpl_nova_pede_revisao_e_registrada_nao_bloqueia(self):
        results = evaluate(component("pypi:novo-mpl@1.0", "MPL-2.0"), component("pypi:certifi@2026.10.23", "MPL-2.0"))
        self.assertEqual("REVISÃO PENDENTE", results["novo-mpl"]["status"])
        self.assertEqual("REVISÃO REGISTRADA", results["certifi"]["status"])
        self.assertEqual("R-07", results["certifi"]["registro"])

    def test_desenvolvimento_aceita_gpl_mas_revisa_eula_de_taxa(self):
        dev = {"distribution": "não distribuído", "environment": "desenvolvimento"}
        results = evaluate(component("npm:ferramenta-gpl@1.0.0", "GPL-3.0-only", **dev),
                           component("nuget:Novo.Osmf@1.0.0", "MIT AND LicenseRef-OSMF-EULA", **dev))
        self.assertEqual("OK", results["ferramenta-gpl"]["status"])
        self.assertEqual("REVISÃO PENDENTE", results["Novo.Osmf"]["status"])

    def test_busl_sob_demanda_pede_revisao(self):
        terraform = component("dl:ferramenta@1.0.0", "BUSL-1.1", distribution="baixado sob demanda", environment="instalação sob demanda")
        self.assertEqual("REVISÃO PENDENTE", evaluate(terraform)["ferramenta"]["status"])

    def test_origem_mostra_o_caminho_ate_a_dependencia_direta(self):
        root = component("npm:app@1.0.0", "MIT")
        middle = component("npm:meio@1.0.0", "MIT", direct=False)
        leaf = component("npm:folha@1.0.0", "GPL-3.0-only", direct=False)
        root.depends_on.add(middle.key)
        middle.depends_on.add(leaf.key)
        self.assertEqual("via app > meio", evaluate(root, middle, leaf)["folha"]["origem"])


class DetectionTests(unittest.TestCase):
    def test_texto_mpl_nao_vira_gpl(self):
        text = "Mozilla Public License Version 2.0 ... Secondary License means GNU General Public License, Version 2.0"
        self.assertEqual({"MPL-2.0"}, g.detect_text(text))

    def test_mit_quebrado_em_linhas_e_reconhecido(self):
        self.assertEqual({"MIT"}, g.detect_text("Permission is hereby granted, free of charge, to any\nperson obtaining a copy"))

    def test_expressao_de_cargo_com_barra_vira_or(self):
        self.assertEqual("MIT OR Apache-2.0", g.normalize("MIT/Apache-2.0"))


if __name__ == "__main__":
    unittest.main()
