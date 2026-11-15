import tempfile
import unittest
from pathlib import Path
from unittest import mock

import _paths  # noqa: F401

from knowledge import chunking, retrieval
from knowledge import index as rag

PROJECT_A = "p-aaaaaaaaaaaa"
PROJECT_B = "p-bbbbbbbbbbbb"
TOKEN = "alfa-mariliz-91"


class RagTests(unittest.TestCase):
    def test_atualiza_so_o_arquivo_mudado_e_remove_orfaos(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            first = rag.sync(root, PROJECT_A, [{"path": "docs/a.md", "hash": "a" * 64, "text": "alfa mariliz exclusivo neste arquivo longo"}])
            self.assertEqual(first["updated"], 1)
            second = rag.sync(root, PROJECT_A, [{"path": "docs/a.md", "hash": "a" * 64}])
            self.assertEqual(second["updated"], 0)
            self.assertEqual(second["unchanged"], 1)
            changed = rag.sync(root, PROJECT_A, [{"path": "docs/b.md", "hash": "b" * 64, "text": "estoque e catalogo sem o termo exclusivo longo"}])
            self.assertEqual(changed["removed"], 1)
            self.assertEqual(changed["updated"], 1)
            self.assertEqual(rag.search(root, PROJECT_A, "alfa mariliz"), [])
            self.assertEqual(rag.search(root, PROJECT_A, "estoque catalogo")[0]["filePath"], "docs/b.md")

    def test_fonte_externa_entra_e_sai_do_indice_junto_com_a_autorizacao(self) -> None:
        external = "@external/github/pint-000000000001/issues/42.md"
        text = "# 42: Corrigir a cobranca\n\nFonte externa: dado de terceiros.\nSituacao: aberto\n"
        with tempfile.TemporaryDirectory() as root:
            rag.sync(root, PROJECT_A, [{"path": external, "hash": "c" * 64, "text": text}])
            found = rag.search(root, PROJECT_A, "cobranca")
            self.assertEqual(found[0]["filePath"], external)
            self.assertTrue(found[0]["chunkId"].startswith("@external/"))

            # A autorizacao foi retirada: o motor deixa de mandar a fonte e o indice a apaga.
            revoked = rag.sync(root, PROJECT_A, [{"path": "docs/a.md", "hash": "a" * 64, "text": "texto longo o bastante"}])
            self.assertEqual(revoked["removed"], 1)
            self.assertEqual(rag.search(root, PROJECT_A, "cobranca"), [])
            self.assertNotIn(external, [entry["path"] for entry in rag.manifest(root, PROJECT_A)])

    def test_decisao_registrada_e_achada_pela_pergunta_e_sai_do_indice_quando_o_motor_deixa_de_manda_la(self) -> None:
        decision = "@decision/dec-000000000001.md"
        text = "# Decisão registrada: Adiar o marco\n\nDecidido: adiar o marco da migracao em duas semanas.\nMotivo: bloqueios de infraestrutura.\n"
        with tempfile.TemporaryDirectory() as root:
            rag.sync(root, PROJECT_A, [{"path": decision, "hash": "d" * 64, "text": text}])
            found = rag.search(root, PROJECT_A, "adiar migracao infraestrutura")
            self.assertEqual(found[0]["filePath"], decision)
            self.assertTrue(found[0]["chunkId"].startswith("@decision/"))

            # Outro projeto nunca enxerga a decisao deste.
            self.assertEqual(rag.search(root, PROJECT_B, "adiar migracao infraestrutura"), [])

            gone = rag.sync(root, PROJECT_A, [{"path": "docs/a.md", "hash": "a" * 64, "text": "texto longo o bastante"}])
            self.assertEqual(gone["removed"], 1)
            self.assertEqual(rag.search(root, PROJECT_A, "adiar migracao"), [])

    def test_dois_projetos_nao_compartilham_trechos(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            rag.sync(root, PROJECT_A, [{"path": "docs/nota.md", "hash": "a" * 64, "text": f"O identificador {TOKEN} fica somente neste projeto."}])
            rag.sync(root, PROJECT_B, [{"path": "docs/nota.md", "hash": "b" * 64, "text": "Este projeto fala de estoque e de catalogo geral."}])
            leaked = rag.search(root, PROJECT_B, TOKEN)
            self.assertEqual(leaked, [])
            found = rag.search(root, PROJECT_A, TOKEN)
            self.assertEqual(found[0]["filePath"], "docs/nota.md")
            self.assertIn(TOKEN, found[0]["excerpt"])
            self.assertNotEqual(found[0]["hash"], "")

    def test_indice_fica_na_pasta_do_projeto_e_o_antigo_e_apagado(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            legacy = Path(root) / PROJECT_A / "index.sqlite"
            legacy.parent.mkdir(parents=True)
            legacy.write_bytes(b"antigo")
            rag.sync(root, PROJECT_A, [{"path": "docs/a.md", "hash": "a" * 64, "text": "texto longo o bastante"}])
            self.assertTrue((Path(root) / "projects" / PROJECT_A / "knowledge" / "index.sqlite").is_file())
            self.assertFalse(legacy.exists())
            self.assertEqual(rag.manifest(root, PROJECT_A), [{"path": "docs/a.md", "hash": "a" * 64}])

    def test_consultar_nao_cria_pasta_nem_apaga_o_indice_antigo(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            legacy = Path(root) / PROJECT_A / "index.sqlite"
            legacy.parent.mkdir(parents=True)
            legacy.write_bytes(b"antigo")

            self.assertEqual(rag.manifest(root, PROJECT_A), [])
            self.assertEqual(rag.lexical(root, PROJECT_A, "texto qualquer"), [])
            self.assertEqual(rag.symbols(root, PROJECT_A, ["Conta"]), [])
            self.assertIsNone(rag.chunk_body(root, PROJECT_A, "docs/a.md#x"))

            self.assertTrue(legacy.is_file())
            self.assertFalse((Path(root) / "projects").exists())

    def test_trecho_mantem_o_id_quando_outra_parte_do_arquivo_muda(self) -> None:
        before = "public sealed class Conta\n{\n    public void Debitar(decimal valor) { }\n\n    public void Creditar(decimal valor) { }\n}\n"
        after = before.replace("public void Debitar(decimal valor) { }", "public void Debitar(decimal valor)\n    {\n        Validar(valor);\n    }")
        with tempfile.TemporaryDirectory() as root:
            rag.sync(root, PROJECT_A, [{"path": "src/Conta.cs", "hash": "a" * 64, "text": before}])
            first = rag.symbols(root, PROJECT_A, ["Creditar"])
            changed = rag.sync(root, PROJECT_A, [{"path": "src/Conta.cs", "hash": "b" * 64, "text": after}])
            second = rag.symbols(root, PROJECT_A, ["Creditar"])
        self.assertEqual(first[0]["chunkId"], "src/Conta.cs#sym:Conta.Creditar")
        self.assertEqual(second[0]["chunkId"], first[0]["chunkId"])
        self.assertEqual(second[0]["documentVersion"], 2)
        self.assertTrue(changed["valid"])

    def test_metadados_e_procedencia_de_cada_trecho(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            rag.sync(root, PROJECT_A, [{"path": "src/Billing/Service.cs", "hash": "a" * 64,
                                        "text": "namespace Loja;\n\npublic class Service\n{\n    public void Cobrar() { }\n}\n"}])
            hit = rag.symbols(root, PROJECT_A, ["Service.Cobrar"])[0]
        self.assertEqual(hit["filePath"], "src/Billing/Service.cs")
        self.assertEqual(hit["language"], "csharp")
        self.assertEqual(hit["module"], "src/Billing")
        self.assertEqual(hit["kind"], "member")
        self.assertEqual(hit["hash"], "a" * 64)
        self.assertGreater(hit["startLine"], 1)

    def test_texto_ausente_fica_pendente_e_nao_derruba_o_resto(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            stats = rag.sync(root, PROJECT_A, [
                {"path": "docs/a.md", "hash": "a" * 64, "text": "texto longo o bastante"},
                {"path": "docs/b.md", "hash": "b" * 64},
            ])
        self.assertEqual(stats["pending"], 1)
        self.assertEqual(stats["documents"], 1)

    def test_sobrecarga_depois_de_metodo_longo_ganha_id_proprio_e_o_arquivo_entra_inteiro(self) -> None:
        # Validacao real C1: o metodo longo vira Compute e Compute~2, e a sobrecarga tambem pedia Compute~2.
        corpo = "".join(f"        total += Parcela({n}) * Taxa({n});\n" for n in range(120))
        text = (
            "public class Previsao\n{\n"
            f"    public decimal Compute(DateTime inicio)\n    {{\n        var total = 0m;\n{corpo}        return total;\n    }}\n\n"
            "    public decimal Compute(int dias)\n    {\n        return CalculoDaSobrecargaUnica(dias);\n    }\n}\n"
        )
        pieces = chunking.chunk("src/Previsao.cs", text)
        anchors = [piece.anchor for piece in pieces]
        self.assertEqual(len(anchors), len(set(anchors)), anchors)
        with tempfile.TemporaryDirectory() as root:
            stats = rag.sync(root, PROJECT_A, [{"path": "src/Previsao.cs", "hash": "a" * 64, "text": text}])
            self.assertTrue(stats["valid"])
            self.assertEqual(stats["chunks"], len(pieces))
            self.assertEqual(rag.search(root, PROJECT_A, "CalculoDaSobrecargaUnica")[0]["filePath"], "src/Previsao.cs")

    def test_documento_que_o_banco_recusa_fica_de_fora_sozinho(self) -> None:
        real = rag.chunk

        def repete(path: str, text: str) -> list:
            pieces = real(path, text)
            return pieces + pieces if path == "src/ruim.cs" else pieces

        sources = [
            {"path": "docs/a.md", "hash": "a" * 64, "text": "estoque e catalogo do armazem central"},
            {"path": "src/ruim.cs", "hash": "b" * 64, "text": "public class Ruim { public void Falha() { } }"},
            {"path": "docs/c.md", "hash": "c" * 64, "text": "frete e transportadora da regiao sul"},
        ]
        with tempfile.TemporaryDirectory() as root:
            with mock.patch.object(rag, "chunk", repete), mock.patch("sys.stderr") as stderr:
                stats = rag.sync(root, PROJECT_A, sources)
            self.assertFalse(stats["valid"])
            self.assertEqual(stats["documents"], 2)
            self.assertEqual(rag.search(root, PROJECT_A, "transportadora")[0]["filePath"], "docs/c.md")
            self.assertEqual(rag.search(root, PROJECT_A, "Falha"), [])
            logged = "".join(str(call.args[0]) for call in stderr.write.call_args_list if call.args)
            self.assertIn("IntegrityError", logged)
            self.assertNotIn("ruim", logged)

            # Sem o defeito, a sincronização seguinte traz o documento que tinha ficado de fora.
            again = rag.sync(root, PROJECT_A, sources)
            self.assertTrue(again["valid"])
            self.assertEqual(again["documents"], 3)

    def test_chunking_segue_a_estrutura_da_fonte(self) -> None:
        markdown = chunking.chunk("docs/a.md", "# Guia\n\nIntro.\n\n## Instalação\n\nPasso um.\n\n### Windows\n\nPasso dois.\n")
        self.assertEqual([piece.anchor for piece in markdown], ["h:guia", "h:instalacao", "h:windows"])
        self.assertTrue(markdown[2].body.startswith("Guia > Instalação > Windows"))
        python = chunking.chunk("x.py", "def a():\n    return 1\n\n\nclass B:\n    def c(self):\n        return 2\n")
        self.assertEqual([piece.anchor for piece in python], ["function:a", "class:B"])
        self.assertIn("B.c", python[1].symbols)
        spec = chunking.chunk("spec.md", "## Requisitos\n\n- REQ-001: O SISTEMA DEVE responder.\n")
        self.assertIn("REQ-001", spec[0].symbols)
        csharp = chunking.chunk("T.cs", 'public class Testes\n{\n    public void Escreve()\n    {\n        Gravar("class X { }");\n    }\n}\n')
        self.assertEqual([piece.anchor for piece in csharp], ["type:Testes", "sym:Testes.Escreve"])
        self.assertEqual(rag.query_terms("como o RRF funde as listas de"), ["como", "rrf", "funde", "listas"])
        long_text = "\n\n".join(f"Parágrafo {n} " + "palavra " * 60 for n in range(10))
        parts = chunking.chunk("notas.txt", long_text)
        self.assertTrue(all(len(piece.body) <= chunking.MAX_BODY for piece in parts))

    def test_recuperacao_hibrida_acha_identificador_curto_e_id_de_requisito(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            rag.sync(root, PROJECT_A, [
                {"path": "src/Pix.cs", "hash": "a" * 64, "text": "public sealed record Pix(string Key);\n"},
                {"path": "docs/spec.md", "hash": "b" * 64, "text": "## Requisitos\n\n- REQ-012: O SISTEMA DEVE suspender.\n"},
                {"path": "docs/outro.md", "hash": "c" * 64, "text": "Texto sobre pagamentos instantaneos e requisitos gerais."},
            ])
            pix = retrieval.search(root, PROJECT_A, "onde fica o Pix")
            req = retrieval.search(root, PROJECT_A, "o que diz o REQ-012")
        self.assertEqual(pix[0]["filePath"], "src/Pix.cs")
        self.assertIn(pix[0]["retriever"], {"symbol", "fused"})
        self.assertEqual(req[0]["filePath"], "docs/spec.md")
        self.assertEqual(retrieval.identifiers("o que BillingService.Charge faz com calcular_juros e useInvoices"),
                         ["BillingService.Charge", "calcular_juros", "useInvoices"])

    def test_uma_busca_nao_abre_uma_conexao_por_trecho_candidato(self) -> None:
        documents = [
            {"path": f"docs/nota{n}.md", "hash": f"{n:x}" * 64 if n < 16 else "f" * 64, "text": f"cobranca do cliente numero {n} fica registrada aqui"}
            for n in range(12)
        ]
        with tempfile.TemporaryDirectory() as root:
            rag.sync(root, PROJECT_A, documents)
            opened: list[int] = []
            original = rag._session

            def counting(path):
                opened.append(1)
                return original(path)

            with mock.patch.object(rag, "_session", counting):
                found = retrieval.search(root, PROJECT_A, "cobranca do cliente", limit=5)

        self.assertEqual(len(found), 5)
        # lexical + simbolos + os corpos de todos os candidatos de uma vez; nunca uma sessao por candidato
        self.assertLessEqual(len(opened), 3)

    def test_chunk_bodies_devolve_o_corpo_de_cada_id_pedido_e_ignora_o_que_nao_existe(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            rag.sync(root, PROJECT_A, [{"path": "docs/a.md", "hash": "a" * 64, "text": "alfa mariliz exclusivo neste arquivo longo"}])
            chunk_id = rag.search(root, PROJECT_A, "alfa mariliz")[0]["chunkId"]

            bodies = rag.chunk_bodies(root, PROJECT_A, [chunk_id, "docs/x.md#nao-existe"])

            self.assertEqual(list(bodies), [chunk_id])
            self.assertIn("alfa mariliz", bodies[chunk_id])
            self.assertEqual(rag.chunk_bodies(root, PROJECT_A, []), {})

    def test_recusa_caminho_de_segredo_e_projeto_invalido(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            stats = rag.sync(root, PROJECT_A, [
                {"path": "../.env", "hash": "c" * 64, "text": "API_KEY=abcd1234efgh5678 nao pode entrar"},
                {"path": ".env", "hash": "d" * 64, "text": "API_KEY=abcd1234efgh5678 nao pode entrar"},
                {"path": "docs/ok.md", "hash": "e" * 64, "text": "Texto seguro e longo o bastante para indexar."},
            ])
            self.assertEqual(stats["documents"], 1)
            with self.assertRaises(rag.KnowledgeError):
                rag.sync(root, "../" + PROJECT_A, [])
            with self.assertRaises(rag.KnowledgeError):
                rag.search("/tmp/nao-importa", "p-1", "texto")
