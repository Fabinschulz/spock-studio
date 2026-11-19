using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Workflows;

namespace Mariliz.Application.Tests;

// W2-3: o eval de referências roda no Python, sobre os mesmos documentos @reference/ que o motor manda ao índice. O arquivo
// é gerado daqui; se a pasta Organização ou o corte por seção mudarem, este teste falha até ele ser regenerado.
public class ReferenceCorpusTests
{
    [Fact]
    public void Corpus_do_eval_de_referencias_e_o_que_o_motor_indexa()
    {
        var corpus = new JsonObject
        {
            ["schema"] = "rag.reference-corpus.v1",
            ["documents"] = new JsonArray(OrganizationReferences.Documents
                .Select(document => (JsonNode)new JsonObject { ["path"] = document.Path, ["hash"] = document.Hash, ["text"] = document.Text })
                .ToArray())
        };
        var pretty = corpus.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");

        var path = Path.Combine(ContractFiles.Root, "intelligence", "evaluations", "datasets", "reference-corpus.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "reference-corpus: a pasta Organização mudou. Regenere com MARILIZ_UPDATE_GOLDEN=1 e rode o eval de referências.");
    }
}
