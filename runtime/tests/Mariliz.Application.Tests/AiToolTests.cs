using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.AiTools;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Tests;

public class AiToolTests
{
    private const string ClaudePath = @"C:\tools\claude.cmd";

    private sealed class FakeLocator(Dictionary<string, string> installed) : IExecutableLocator
    {
        public string? Find(string name) => installed.GetValueOrDefault(name);
    }

    private sealed class FakeRunner(Func<ProcessSpec, ProcessResult> reply) : IProcessRunner
    {
        public List<ProcessSpec> Calls { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct)
        {
            lock (Calls) Calls.Add(spec);
            return Task.FromResult(reply(spec));
        }
    }

    private static ProcessResult Output(string text) => new(0, text + "\n", "", false, false);

    private static AiToolService Service(Dictionary<string, string> installed, Func<ProcessSpec, ProcessResult> reply, out FakeRunner runner)
    {
        runner = new FakeRunner(reply);
        return new AiToolService(new FakeLocator(installed), runner);
    }

    private static AiToolStatus Tool(IReadOnlyList<AiToolStatus> tools, string id) => tools.Single(tool => tool.Id == id);

    [Fact]
    public async Task Diz_o_que_esta_instalado_com_a_versao_e_so_pergunta_a_quem_existe()
    {
        var service = Service(
            new() { ["claude"] = ClaudePath, ["codex"] = @"C:\tools\codex.cmd" },
            spec => Output(spec.Executable == ClaudePath ? "2.0.5 (Claude Code)" : "codex-cli 0.46.0"),
            out var runner);

        var tools = await service.ListAsync(CancellationToken.None);

        Assert.Equal(["claude-code", "cursor", "codex", "gemini"], tools.Select(tool => tool.Id));
        Assert.Equal("2.0.5", Tool(tools, "claude-code").Version);
        Assert.Equal("0.46.0", Tool(tools, "codex").Version);
        Assert.False(Tool(tools, "cursor").Installed);
        Assert.False(Tool(tools, "gemini").Installed);
        Assert.All(runner.Calls, call => Assert.Equal(["--version"], call.Arguments));
        Assert.Equal(2, runner.Calls.Count);
    }

    [Fact]
    public async Task Claude_Code_e_Codex_tem_o_Harness_configurado_pelo_Spock()
    {
        var service = Service([], _ => Output(""), out _);

        var tools = await service.ListAsync(CancellationToken.None);

        Assert.Equal(["claude-code", "codex"], tools.Where(tool => tool.HarnessSupport).Select(tool => tool.Id));
    }

    [Theory]
    [InlineData(1, "", "erro", false, false)]
    [InlineData(-1, "", "", true, false)]
    [InlineData(-1, "", "", false, true)]
    [InlineData(0, "sem numero de versao", "", false, false)]
    public async Task Instalada_sem_versao_legivel_continua_instalada_com_versao_desconhecida(
        int exitCode, string stdout, string stderr, bool timedOut, bool notFound)
    {
        var service = Service(new() { ["gemini"] = "/usr/bin/gemini" }, _ => new ProcessResult(exitCode, stdout, stderr, timedOut, notFound), out _);

        var gemini = Tool(await service.ListAsync(CancellationToken.None), "gemini");

        Assert.True(gemini.Installed);
        Assert.Null(gemini.Version);
    }

    [Fact]
    public async Task A_versao_pode_vir_pelo_stderr_e_em_formato_de_data()
    {
        var service = Service(new() { ["cursor-agent"] = "/usr/bin/cursor-agent" },
            _ => new ProcessResult(0, "", "2025.10.02-7ae6800", false, false), out _);

        Assert.Equal("2025.10.02", Tool(await service.ListAsync(CancellationToken.None), "cursor").Version);
    }

    [Fact]
    public void O_caminho_do_executavel_nunca_vai_para_a_resposta()
    {
        var session = new EngineSession("test", aiTools: Service(new() { ["claude"] = ClaudePath }, _ => Output("2.0.5"), out _));

        var json = Call(session).ToJsonString();

        Assert.DoesNotContain("claude.cmd", json);
        Assert.DoesNotContain(@"C:\\tools", json);
    }

    private static JsonObject Call(EngineSession session) =>
        session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = "aitool.list", ["params"] = new JsonObject() }.ToJsonString()).Response;

    [Fact]
    public void O_metodo_responde_pelo_protocolo_e_e_oferecido_no_hello()
    {
        var session = new EngineSession("test", aiTools: Service(new() { ["claude"] = ClaudePath }, _ => Output("2.0.5"), out _));

        var list = Call(session);
        var hello = session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "h", ["method"] = "engine.hello",
            ["params"] = new JsonObject { ["protocol"] = new JsonArray(1), ["hostVersion"] = "x" }
        }.ToJsonString()).Response;

        Assert.Equal(4, list["result"]!["tools"]!.AsArray().Count);
        Assert.Contains("aitool.list", hello["result"]!["capabilities"]!.AsArray().Select(node => node!.GetValue<string>()));
    }

    [Fact]
    public void Sem_o_servico_o_metodo_nao_existe()
    {
        var response = Call(new EngineSession("test"));

        Assert.Equal(ProtocolErrorCodes.MethodNotFound, response["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void Fixture_de_contrato_da_lista_de_ferramentas()
    {
        var service = Service(
            new() { ["claude"] = ClaudePath, ["codex"] = @"C:\tools\codex.cmd", ["gemini"] = "/usr/bin/gemini" },
            spec => spec.Executable switch
            {
                ClaudePath => Output("2.0.5 (Claude Code)"),
                "/usr/bin/gemini" => Output("0.9.0"),
                _ => new ProcessResult(1, "", "falhou", false, false)
            },
            out _);
        var result = Call(new EngineSession("test", aiTools: service))["result"]!;

        var pretty = result.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");

        var path = ContractFiles.Path_("fixtures", "aitools", "list.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "aitool.list: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
