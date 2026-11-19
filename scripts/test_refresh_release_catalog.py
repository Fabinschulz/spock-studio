import datetime as dt
import importlib.util
import unittest
from pathlib import Path

_spec = importlib.util.spec_from_file_location("catalog", Path(__file__).with_name("refresh-release-catalog.py"))
catalog = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(catalog)

TODAY = dt.date(2026, 11, 18)


def source(id_: str, lines: list[dict], prerelease: str | None = None) -> dict:
    return {"id": id_, "name": id_, "policy": "lifecycle", "cycle": "major", "url": f"https://example.invalid/{id_}",
            "load": lambda today: (lines, prerelease)}


def failing(id_: str) -> dict:
    def load(today):
        raise catalog.SourceError("fora do ar")
    return {**source(id_, []), "load": load}


def line(cycle: str, status: str = "active", channel: str = "stable", latest: str | None = None) -> dict:
    return {"cycle": cycle, "channel": channel, "status": status, "releaseDate": None, "eol": None, "latest": latest,
            "latestDate": None, "language": None}


class ParsersTest(unittest.TestCase):
    def test_dotnet_traz_canal_fase_e_versao_do_csharp(self):
        lines = catalog.parse_dotnet({"releases-index": [
            {"channel-version": "11.0", "latest-release": "11.0.0", "release-type": "sts", "support-phase": "active", "eol-date": "2028-11-14"},
            {"channel-version": "12.0", "latest-release": "12.0.0-preview.1", "release-type": "lts", "support-phase": "preview", "eol-date": None},
            {"channel-version": "8.0", "latest-release": "8.0.34", "release-type": "lts", "support-phase": "eol", "eol-date": "2026-11-10"},
        ]})
        self.assertEqual(("sts", "active", "C# 15"), (lines[0]["channel"], lines[0]["status"], lines[0]["language"]))
        self.assertEqual(("preview", "preview"), (lines[1]["channel"], lines[1]["status"]))
        self.assertEqual("eol", lines[2]["status"])

    def test_node_pelas_datas_do_cronograma(self):
        schedule = {
            "v26": {"start": "2026-04-22", "lts": "2026-10-28", "maintenance": "2027-10-20", "end": "2029-04-30"},
            "v27": {"start": "2027-04-22", "lts": "2027-10-20", "maintenance": "2028-10-20", "end": "2030-04-30"},
            "v24": {"start": "2025-05-06", "lts": "2025-10-28", "maintenance": "2026-10-20", "end": "2028-04-30"},
            "v18": {"start": "2022-04-19", "lts": "2022-10-25", "maintenance": "2023-10-18", "end": "2025-04-30"},
        }
        releases = [{"version": "v26.13.1"}, {"version": "v26.2.0"}, {"version": "v27.0.0-alpha.0"}, {"version": "v24.22.3"}]
        lines = {item["cycle"]: item for item in catalog.parse_node(schedule, releases, TODAY)}
        self.assertEqual(("lts", "active", "26.13.1"), (lines["26"]["channel"], lines["26"]["status"], lines["26"]["latest"]))
        self.assertEqual(("preview", "preview", None), (lines["27"]["channel"], lines["27"]["status"], lines["27"]["latest"]))
        self.assertEqual("maintenance", lines["24"]["status"])
        self.assertEqual("eol", lines["18"]["status"])

    def test_python_mapeia_as_fases_do_devguide(self):
        lines = {item["cycle"]: item["status"] for item in catalog.parse_python({
            "3.16": {"status": "feature"}, "3.15": {"status": "bugfix"}, "3.12": {"status": "security"}, "3.10": {"status": "end-of-life"},
            "2.7": {"status": "end-of-life"}})}
        self.assertEqual({"3.16": "preview", "3.15": "active", "3.12": "maintenance", "3.10": "eol"}, lines)

    def test_java_lts_antiga_depende_do_fornecedor(self):
        lines = {item["cycle"]: (item["channel"], item["status"]) for item in catalog.parse_java({
            "available_lts_releases": [17, 21, 25], "available_releases": [17, 21, 24, 25, 26, 27],
            "most_recent_lts": 25, "most_recent_feature_release": 27, "tip_version": 28})}
        self.assertEqual(("lts", "active"), lines["25"])
        self.assertEqual(("stable", "active"), lines["27"])
        self.assertEqual(("lts", "vendor"), lines["21"])
        self.assertEqual(("stable", "eol"), lines["26"])
        self.assertEqual(("preview", "preview"), lines["28"])

    def test_npm_ignora_pre_lancamento_e_guarda_o_mais_novo(self):
        lines, prerelease = catalog.parse_npm({
            "dist-tags": {"latest": "8.3.8", "beta": "8.4.0-beta.1", "old": "7.0.0-beta.1"},
            "versions": {"8.3.8": {}, "8.4.0-beta.1": {}, "7.3.8": {}, "7.3.10": {}, "0.9.0": {}}})
        by_cycle = {item["cycle"]: item for item in lines}
        self.assertEqual("8.4.0-beta.1", prerelease)
        self.assertEqual(("active", "8.3.8"), (by_cycle["8"]["status"], by_cycle["8"]["latest"]))
        self.assertEqual(("unknown", "7.3.10"), (by_cycle["7"]["status"], by_cycle["7"]["latest"]))
        self.assertNotIn("0", by_cycle)

    def test_npm_guarda_o_node_exigido_pela_versao_mais_nova_da_linha(self):
        lines, _ = catalog.parse_npm({
            "dist-tags": {"latest": "8.3.8"},
            "versions": {"8.3.8": {"engines": {"node": "^20.19.0 || >=22.12.0"}}, "8.0.0": {"engines": {"node": ">=18"}},
                         "7.3.8": {}}})
        by_cycle = {item["cycle"]: item for item in lines}
        self.assertEqual({"node": "^20.19.0 || >=22.12.0"}, by_cycle["8"]["requires"])
        self.assertIsNone(by_cycle["7"]["requires"])

    def test_php_pelas_datas_de_suporte_ativo_e_de_seguranca(self):
        lines = {item["cycle"]: (item["status"], item["eol"]) for item in catalog.parse_php({"8": {
            "8.5": {"state": "stable", "active_support_end": "2027-12-31T00:00:00+00:00", "security_support_end": "2029-12-31T00:00:00+00:00"},
            "8.4": {"state": "stable", "active_support_end": "2026-11-01T00:00:00+00:00", "security_support_end": "2028-12-31T00:00:00+00:00"},
            "8.3": {"state": "security", "active_support_end": "2025-12-31T00:00:00+00:00", "security_support_end": "2027-12-31T00:00:00+00:00"},
            "8.1": {"state": "eol", "security_support_end": "2025-12-31T00:00:00+00:00"}}}, TODAY)}
        self.assertEqual(("active", "2029-12-31"), lines["8.5"])
        self.assertEqual(("maintenance", "2028-12-31"), lines["8.4"])
        self.assertEqual(("maintenance", "2027-12-31"), lines["8.3"])
        self.assertEqual(("eol", "2025-12-31"), lines["8.1"])

    def test_ruby_le_o_branches_yml_e_normaliza_as_branches_antigas(self):
        lines = {item["cycle"]: (item["status"], item["eol"]) for item in catalog.parse_ruby(
            "# comentário\n- name: 4.1\n  status: preview\n  date:\n  eol_date:\n\n"
            "- name: 3.4\n  status: normal maintenance\n  date: 2024-12-25\n  eol_date:\n\n"
            "- name: 3.3\n  status: legacy maintenance\n  eol_date:\n  expected_eol_date: 2027-03-31\n\n"
            "- name: 2.0.0\n  status: eol\n  eol_date: 2016-02-24\n")}
        self.assertEqual({"4.1": ("preview", None), "3.4": ("active", None), "3.3": ("maintenance", "2027-03-31"),
                          "2.0": ("eol", "2016-02-24")}, lines)

    def test_rust_le_a_versao_estavel(self):
        lines = catalog.parse_rust('date = "2026-11-12"\n[pkg.rust]\nversion = "1.100.0 (0ef1bc386 2026-11-09)"\n')
        self.assertEqual(("1.100", "1.100.0"), (lines[0]["cycle"], lines[0]["latest"]))


class RefreshTest(unittest.TestCase):
    def test_fonte_fora_do_ar_mantem_a_entrada_anterior_marcada(self):
        first, _ = catalog.refresh(None, TODAY, "2026-11-01T00:00:00Z", [source("node", [line("26")])])
        second, report = catalog.refresh(first, TODAY, "2026-11-18T00:00:00Z", [failing("node")])
        entry = second["technologies"][0]
        self.assertTrue(entry["stale"])
        self.assertEqual("2026-11-01T00:00:00Z", entry["checkedAt"])
        self.assertIn("mantida", report[0])

    def test_sem_catalogo_anterior_a_falha_interrompe(self):
        with self.assertRaises(catalog.SourceError):
            catalog.refresh(None, TODAY, "2026-11-18T00:00:00Z", [failing("node")])

    def test_regressao_da_linha_mais_nova_e_tratada_como_inconsistencia(self):
        first, _ = catalog.refresh(None, TODAY, "2026-11-01T00:00:00Z", [source("dotnet", [line("11.0"), line("10.0")])])
        second, _ = catalog.refresh(first, TODAY, "2026-11-18T00:00:00Z", [source("dotnet", [line("10.0")])])
        self.assertTrue(second["technologies"][0]["stale"])
        self.assertEqual("11.0", second["technologies"][0]["lines"][0]["cycle"])

    def test_pre_lancamento_nunca_fica_ativo(self):
        errors = catalog.validate_lines("vite", [line("9", status="active", channel="preview")])
        self.assertTrue(any("pré-lançamento" in error for error in errors))

    def test_relatorio_lista_a_mudanca_de_linha(self):
        first, _ = catalog.refresh(None, TODAY, "2026-11-01T00:00:00Z", [source("dotnet", [line("10.0", latest="10.0.14")])])
        _, report = catalog.refresh(first, TODAY, "2026-11-18T00:00:00Z",
                                    [source("dotnet", [line("11.0", latest="11.0.0"), line("10.0", latest="10.0.15")])])
        self.assertTrue(any("11.0: nova linha" in item for item in report))
        self.assertTrue(any("10.0.14" in item and "10.0.15" in item for item in report))

    def test_catalogo_embutido_e_valido(self):
        import json
        self.assertEqual([], catalog.check(json.loads(catalog.CATALOG.read_text(encoding="utf-8"))))


if __name__ == "__main__":
    unittest.main()
