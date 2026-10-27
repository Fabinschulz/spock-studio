using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Mariliz.Engine.IntegrationTests;

public class EngineProcessTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Mariliz.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }

    private static Process StartEngine()
    {
        // O motor chega à saída dos testes pela ProjectReference; é o mesmo build que está sendo testado.
        var dll = Path.Combine(AppContext.BaseDirectory, "Mariliz.Engine.dll");
        var psi = new ProcessStartInfo("dotnet", $"\"{dll}\"")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false
        };
        return Process.Start(psi)!;
    }

    [Fact]
    public async Task Motor_responde_hello_e_encerra_no_shutdown_sem_poluir_o_stdout()
    {
        using var engine = StartEngine();
        var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;

        await engine.StandardInput.WriteLineAsync("{\"v\":1,\"kind\":\"request\",\"id\":\"h1\",\"method\":\"engine.hello\",\"params\":{\"protocol\":[1],\"hostVersion\":\"test\"}}");
        await engine.StandardInput.FlushAsync(timeout);
        var helloLine = await engine.StandardOutput.ReadLineAsync(timeout);

        await engine.StandardInput.WriteLineAsync("{\"v\":1,\"kind\":\"request\",\"id\":\"s1\",\"method\":\"engine.shutdown\"}");
        await engine.StandardInput.FlushAsync(timeout);
        var shutdownLine = await engine.StandardOutput.ReadLineAsync(timeout);
        var rest = await engine.StandardOutput.ReadToEndAsync(timeout);
        await engine.WaitForExitAsync(timeout);

        var hello = JsonNode.Parse(helloLine!)!;
        Assert.Equal("h1", hello["id"]!.GetValue<string>());
        var schemaPath = Path.Combine(Root(), "packages", "contracts", "schemas", "protocol", "envelope.v1.schema.json");
        var schema = JsonSchema.FromText(File.ReadAllText(schemaPath));
        Assert.True(schema.Evaluate(System.Text.Json.JsonSerializer.SerializeToElement(hello)).IsValid);
        Assert.Equal("s1", JsonNode.Parse(shutdownLine!)!["id"]!.GetValue<string>());
        Assert.Equal(string.Empty, rest);
        Assert.Equal(0, engine.ExitCode);
    }

    [Fact]
    public async Task Entrada_invalida_nao_derruba_o_motor()
    {
        using var engine = StartEngine();
        var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;

        await engine.StandardInput.WriteLineAsync("isto não é json");
        await engine.StandardInput.FlushAsync(timeout);
        var errorLine = await engine.StandardOutput.ReadLineAsync(timeout);

        await engine.StandardInput.WriteLineAsync("{\"v\":1,\"kind\":\"request\",\"id\":\"s\",\"method\":\"engine.shutdown\"}");
        await engine.StandardInput.FlushAsync(timeout);
        await engine.StandardOutput.ReadLineAsync(timeout);
        await engine.WaitForExitAsync(timeout);

        Assert.Equal("INVALID_MESSAGE", JsonNode.Parse(errorLine!)!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(0, engine.ExitCode);
    }
}
