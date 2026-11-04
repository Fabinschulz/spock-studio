using System.Text.Json.Nodes;
using Mariliz.Application.Ports;
using Mariliz.Application.Workflows;

namespace Mariliz.Application.Tests;

/// <summary>Do runtime de IA só chega à interface o que confere com o contrato: caminho relativo seguro, trecho sem segredo, impacto conhecido.</summary>
public class WorkflowResultTests
{
    private static readonly string Hash = new('a', 64);

    [Fact]
    public void Evidencia_com_caminho_inseguro_fica_de_fora_e_trecho_com_segredo_perde_o_texto()
    {
        var result = WorkflowResult.From(Ack(new JsonObject
        {
            ["evidence"] = new JsonArray(
                Evidence("docs/ok.md", "trecho comum"),
                Evidence("../fora.md", "x"),
                Evidence("/etc/passwd", "x"),
                Evidence("docs\\win.md", "x"),
                Evidence("docs/segredo.md", "password = abcd1234efgh5678"))
        }));

        var evidence = result["evidence"]!.AsArray();
        Assert.Equal(["docs/ok.md", "docs/segredo.md"], evidence.Select(item => item!["filePath"]!.GetValue<string>()));
        Assert.Equal("trecho comum", evidence[0]!["excerpt"]!.GetValue<string>());
        Assert.DoesNotContain("abcd1234efgh5678", result.ToJsonString());
    }

    [Fact]
    public void Impacto_so_entra_inteiro_e_dentro_do_vocabulario()
    {
        var valid = WorkflowResult.From(Ack(new JsonObject { ["impact"] = Impact("servico") }));
        var unknown = WorkflowResult.From(Ack(new JsonObject { ["impact"] = Impact("banco") }));

        Assert.Equal("servico", valid["impact"]!["areas"]![0]!.GetValue<string>());
        Assert.Null(unknown["impact"]);
    }

    [Fact]
    public void Ids_de_etapa_aceitam_so_identificadores_curtos()
    {
        var steps = WorkflowResult.StepIds(new JsonObject
        {
            ["plan"] = new JsonObject
            {
                ["steps"] = new JsonArray(
                    new JsonObject { ["id"] = "describe" },
                    new JsonObject { ["id"] = "rm -rf /" },
                    new JsonObject { ["id"] = 42 })
            }
        });

        Assert.Equal(["describe"], steps.Select(item => item!.GetValue<string>()));
    }

    private static JsonObject Evidence(string path, string excerpt) => new()
    {
        ["documentId"] = path, ["chunkId"] = path + "#1", ["filePath"] = path, ["hash"] = Hash, ["score"] = 1.5, ["excerpt"] = excerpt
    };

    private static JsonObject Impact(string area) => new()
    {
        ["schema"] = "engineering.impact.v1", ["areas"] = new JsonArray(area), ["risks"] = new JsonArray(), ["evidence"] = new JsonArray()
    };

    private static WorkflowAck Ack(JsonObject output) =>
        new(WorkflowService.WorkflowId, "run-1", "p-1", "ex-1", "completed", output, [], [], null);
}
