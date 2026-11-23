import json
import unittest
from pathlib import Path

import _paths  # noqa: F401
from support_files import TemporaryDirectory
from test_graph import _run, _start

from contracts.messages import SEARCH_SCHEMA, ContractError, parse_search
from knowledge import index, retrieval
from workflows.engineering import graph

PROJECT = "p-aaaaaaaaaaaa"


def _doc(path: str, letter: str, text: str) -> dict:
    return {"path": path, "hash": letter * 64, "text": text}


ANGULAR = _doc("@reference/catalogo/frontend-angular.md", "a",
               "---\ntechnologies: angular\n---\n# Referência Angular\n\n## Estado da tela\n\nSignals guardam o estado da tela.\n")
REACT = _doc("@reference/catalogo/frontend-react.md", "b",
             "---\ntechnologies: react, nextjs\n---\n# Referência React\n\n## Estado da tela\n\nTanStack Query guarda o estado do servidor.\n")
GENERAL = _doc("@reference/catalogo/principios.md", "c", "# Princípios\n\n## Estado\n\nO estado da tela fica perto de quem o usa.\n")
PROJECT_FILE = _doc("docs/estado.md", "d", "# Estado\n\nO estado da tela deste projeto fica no hook useCart.\n")


def _paths_for(technologies: list[str] | None) -> list[str]:
    with TemporaryDirectory() as root:
        index.sync(root, PROJECT, [dict(ANGULAR), dict(REACT), dict(GENERAL), dict(PROJECT_FILE)])
        return [hit["filePath"] for hit in retrieval.search(root, PROJECT, "estado da tela", 10, technologies)]


class TechnologyFilterTests(unittest.TestCase):
    def test_projeto_react_nao_recebe_a_referencia_angular(self) -> None:
        paths = _paths_for(["react"])

        self.assertNotIn(ANGULAR["path"], paths)
        self.assertIn(REACT["path"], paths)

    def test_referencia_sem_tecnologia_e_arquivo_do_projeto_valem_para_todos(self) -> None:
        paths = _paths_for(["angular"])

        self.assertIn(GENERAL["path"], paths)
        self.assertIn(PROJECT_FILE["path"], paths)
        self.assertNotIn(REACT["path"], paths)

    def test_sem_stack_conhecida_nada_e_filtrado(self) -> None:
        self.assertEqual(len(_paths_for(None)), 4)
        self.assertEqual(len(_paths_for([])), 4)

    def test_o_frontmatter_vira_metadado_e_nao_texto_do_trecho(self) -> None:
        with TemporaryDirectory() as root:
            index.sync(root, PROJECT, [dict(ANGULAR)])
            hits = retrieval.search(root, PROJECT, "estado da tela", 5)
            bodies = index.chunk_bodies(root, PROJECT, [hit["chunkId"] for hit in hits])

        self.assertTrue(bodies)
        self.assertTrue(all("technologies:" not in body for body in bodies.values()))

    def test_o_fluxo_de_engenharia_filtra_pelas_stacks_do_projeto(self) -> None:
        with TemporaryDirectory() as root:
            payload = _start(root)
            payload["input"]["goal"] = "como guardar o estado da tela"
            payload["context"]["stacks"] = ["react"]
            payload["context"]["knowledgeRoot"] = root
            payload["context"]["sources"] = [dict(ANGULAR), dict(REACT)]

            _events, ack = _run(graph.run_start(payload))

        paths = [item["filePath"] for item in ack["output"]["evidence"]]
        self.assertIn(REACT["path"], paths)
        self.assertNotIn(ANGULAR["path"], paths)


class SearchContractTests(unittest.TestCase):
    def _payload(self, **extra) -> dict:
        return {"schema": SEARCH_SCHEMA, "projectId": PROJECT, "query": "estado", "knowledgeRoot": "C:/x", "limit": 5, **extra}

    def test_filtro_de_tecnologia_e_opcional(self) -> None:
        self.assertEqual(parse_search(self._payload()).technologies, ())
        self.assertEqual(parse_search(self._payload(filters={"technologies": ["react", "nextjs"]})).technologies, ("react", "nextjs"))

    def test_as_fixtures_do_contrato_valem_igual_aqui(self) -> None:
        folder = Path(__file__).resolve().parents[2] / "contracts" / "fixtures" / "intelligence"
        valid = sorted((folder / "valid").glob(f"{SEARCH_SCHEMA}--*.json"))
        invalid = sorted((folder / "invalid").glob(f"{SEARCH_SCHEMA}--*.json"))
        self.assertTrue(valid and invalid)
        for fixture in valid:
            parse_search(json.loads(fixture.read_text(encoding="utf-8")))
        for fixture in invalid:
            with self.subTest(fixture=fixture.name), self.assertRaises(ContractError):
                parse_search(json.loads(fixture.read_text(encoding="utf-8")))

    def test_filtro_fora_do_contrato_e_recusado(self) -> None:
        for broken in ({"technologies": "react"}, {"technologies": ["React!"]}, {"technologies": ["x"] * 21}, {"outro": []}, []):
            with self.subTest(broken=broken), self.assertRaises(ContractError):
                parse_search(self._payload(filters=broken))


if __name__ == "__main__":
    unittest.main()
