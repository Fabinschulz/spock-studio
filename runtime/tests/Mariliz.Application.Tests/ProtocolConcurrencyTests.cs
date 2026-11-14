using System.Text.Json.Nodes;
using System.Threading.Channels;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class ProtocolConcurrencyTests
{
    private const string Key = "sk-test-chave-concorrencia-0001";

    private sealed class QueueReader : TextReader
    {
        private readonly Channel<string?> _lines = Channel.CreateUnbounded<string?>();
        public void Send(string id, string method, JsonObject? parameters = null) =>
            _lines.Writer.TryWrite(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = id, ["method"] = method, ["params"] = parameters ?? new JsonObject() }.ToJsonString());
        public void Eof() => _lines.Writer.TryWrite(null);
        public override async ValueTask<string?> ReadLineAsync(CancellationToken ct) => await _lines.Reader.ReadAsync(ct);
    }

    // Deixa o teste esperar por uma resposta sem sondar o relógio.
    private sealed class SignalWriter : StringWriter
    {
        private readonly Channel<string> _lines = Channel.CreateUnbounded<string>();
        public override Task WriteLineAsync(string? value)
        {
            _lines.Writer.TryWrite(value ?? "");
            return Task.CompletedTask;
        }

        public async Task<JsonObject> NextAsync()
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            return (JsonObject)JsonNode.Parse(await _lines.Reader.ReadAsync(limit.Token))!;
        }
    }

    // Só responde quando o teste manda (ou quando o pedido é cancelado).
    private sealed class HeldTransport : IModelTransport
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Started => _started.Task;
        public void Release() => _release.TrySetResult();

        public async Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct)
        {
            _started.TrySetResult();
            await _release.Task.WaitAsync(ct);
            return new ModelTransportResult("ok", 3);
        }
    }

    private sealed class Rig : IDisposable
    {
        private readonly TempProject _project = TempProject.Dotnet();
        public HeldTransport Transport { get; } = new();
        public QueueReader Input { get; } = new();
        public SignalWriter Output { get; } = new();
        public Task Running { get; }

        public Rig()
        {
            var settings = new FileModelSettings(_project.DataDir);
            var gateway = new ModelGateway(settings, Transport);
            settings.Save("openai", Key);
            var session = new EngineSession("test", models: gateway);
            Running = Task.Run(() => session.RunAsync(Input, Output, CancellationToken.None));
        }

        public void Dispose() => _project.Dispose();
    }

    private static JsonObject Hello() => new() { ["protocol"] = new JsonArray(1), ["hostVersion"] = "test" };

    [Fact]
    public async Task Enquanto_o_motor_espera_o_modelo_ele_continua_respondendo_aos_outros_pedidos()
    {
        using var rig = new Rig();
        rig.Input.Send("lento", ProtocolMethods.ModelTest);
        await rig.Transport.Started;

        rig.Input.Send("curto", ProtocolMethods.Hello, Hello());
        var first = await rig.Output.NextAsync();

        Assert.Equal("curto", first["id"]!.GetValue<string>());
        Assert.True(first["result"]!["capabilities"] is JsonArray);

        rig.Transport.Release();
        var second = await rig.Output.NextAsync();
        Assert.Equal("lento", second["id"]!.GetValue<string>());
        Assert.NotNull(second["result"]);

        rig.Input.Send("fim", ProtocolMethods.Shutdown);
        Assert.Equal("fim", (await rig.Output.NextAsync())["id"]!.GetValue<string>());
        await rig.Running.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task O_encerramento_vale_mesmo_com_um_pedido_longo_em_andamento_e_cancela_o_pedido()
    {
        using var rig = new Rig();
        rig.Input.Send("lento", ProtocolMethods.ModelTest);
        await rig.Transport.Started;

        rig.Input.Send("fim", ProtocolMethods.Shutdown);

        Assert.Equal("fim", (await rig.Output.NextAsync())["id"]!.GetValue<string>());
        await rig.Running.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Ao_fim_da_entrada_o_motor_espera_o_pedido_longo_e_o_responde()
    {
        using var rig = new Rig();
        rig.Input.Send("lento", ProtocolMethods.ModelTest);
        await rig.Transport.Started;

        rig.Input.Eof();
        rig.Transport.Release();

        Assert.Equal("lento", (await rig.Output.NextAsync())["id"]!.GetValue<string>());
        await rig.Running.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Pedidos_curtos_continuam_respondidos_na_ordem_em_que_chegaram()
    {
        using var rig = new Rig();
        for (var i = 0; i < 5; i++) rig.Input.Send("c" + i, ProtocolMethods.Hello, Hello());
        rig.Input.Send("fim", ProtocolMethods.Shutdown);

        var ids = new List<string>();
        for (var i = 0; i < 6; i++) ids.Add((await rig.Output.NextAsync())["id"]!.GetValue<string>());

        Assert.Equal(["c0", "c1", "c2", "c3", "c4", "fim"], ids);
        await rig.Running.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
