using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class ModelGatewayTests
{
    private const string Key = "sk-test-chave-fase-j-9f3a";

    [Fact]
    public async Task Chave_fica_no_diretorio_de_dados_e_nao_volta_no_protocolo()
    {
        using var project = TempProject.Dotnet();
        var before = project.Fingerprint();
        var transport = new FakeTransport { Reply = "eco " + Key, Tokens = 9999 };
        var gateway = new ModelGateway(new FileModelSettings(project.DataDir), transport, TimeSpan.FromMilliseconds(50));
        var session = new EngineSession("test", models: gateway);

        var missing = session.Handle(Line("model.complete", new JsonObject { ["prompt"] = "descrever" })).Response;
        Assert.Equal("MODEL_NOT_CONFIGURED", missing["result"]!["limitation"]!.GetValue<string>());
        Assert.Equal(0, transport.Calls);

        var denied = session.Handle(Line("model.save", new JsonObject { ["provider"] = "outro", ["key"] = Key })).Response;
        Assert.Equal("MODEL_PROVIDER_DENIED", denied["error"]!["code"]!.GetValue<string>());
        Assert.DoesNotContain(Key, denied.ToJsonString());

        var saved = session.Handle(Line("model.save", new JsonObject { ["provider"] = "openai", ["key"] = Key })).Response;
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("protocol", "methods", "model.status.result.schema.json"), saved["result"]));
        Assert.Equal("openai", saved["result"]!["provider"]!.GetValue<string>());
        Assert.DoesNotContain(Key, saved.ToJsonString());
        Assert.True(File.Exists(Path.Combine(project.DataDir, "model.json")));
        Assert.False(File.Exists(Path.Combine(project.Root, "model.json")));
        Assert.Contains(Key, File.ReadAllText(Path.Combine(project.DataDir, "model.json")));

        var done = session.Handle(Line("model.complete", new JsonObject { ["prompt"] = "usar " + Key + " no texto" })).Response;
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("protocol", "methods", "model.complete.result.schema.json"), done["result"]));
        Assert.DoesNotContain(Key, done.ToJsonString());
        Assert.DoesNotContain(Key, transport.SeenPrompt);
        Assert.Equal(Key, transport.SeenKey);
        Assert.Equal(ModelGateway.MaxTokens, transport.SeenMaxTokens);
        Assert.Equal(ModelGateway.Endpoint, transport.SeenEndpoint);
        Assert.Contains("[omitido]", done["result"]!["text"]!.GetValue<string>());
        Assert.Equal(ModelGateway.MaxTokens, done["result"]!["tokens"]!.GetValue<int>());
        Assert.Equal("", done["result"]!["limitation"]!.GetValue<string>());

        var cleared = session.Handle(Line("model.clear", new JsonObject())).Response;
        Assert.False(cleared["result"]!["configured"]!.GetValue<bool>());
        Assert.False(File.Exists(Path.Combine(project.DataDir, "model.json")));
        Assert.Equal(before, project.Fingerprint());
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Prazo_estourado_nao_revela_a_chave()
    {
        using var project = TempProject.Dotnet();
        var transport = new FakeTransport { Delay = TimeSpan.FromSeconds(5) };
        var gateway = new ModelGateway(new FileModelSettings(project.DataDir), transport, TimeSpan.FromMilliseconds(30));
        gateway.Save("openai", Key);
        var session = new EngineSession("test", models: gateway);

        var response = session.Handle(Line("model.complete", new JsonObject { ["prompt"] = "descrever" })).Response;
        Assert.Equal("MODEL_TIMEOUT", response["error"]!["code"]!.GetValue<string>());
        Assert.DoesNotContain(Key, response.ToJsonString());
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Arquivo_corrompido_conta_como_modelo_ausente()
    {
        using var project = TempProject.Dotnet();
        File.WriteAllText(Path.Combine(project.DataDir, "model.json"), "{");
        var gateway = new ModelGateway(new FileModelSettings(project.DataDir), new FakeTransport());
        var status = gateway.Status();
        Assert.False(status.Configured);
        Assert.Equal("", status.Provider);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Transporte_recusa_outro_destino_e_poe_a_chave_so_no_cabecalho()
    {
        var blocked = new RecordingHandler();
        using var transport = new HttpsModelTransport(blocked);
        var denied = await Assert.ThrowsAsync<EngineException>(() =>
            transport.SendAsync(new Uri("https://evil.example/v1/chat/completions"), Key, "oi", 32, CancellationToken.None));
        Assert.Equal("MODEL_ENDPOINT_DENIED", denied.Code);
        Assert.DoesNotContain(Key, denied.Message);
        Assert.False(blocked.Called);

        var allowed = new RecordingHandler();
        using var live = new HttpsModelTransport(allowed);
        var result = await live.SendAsync(ModelGateway.Endpoint, Key, "oi", 32, CancellationToken.None);
        Assert.Equal("resposta", result.Text);
        Assert.Equal("Bearer " + Key, allowed.Authorization);
        Assert.DoesNotContain(Key, allowed.Body);
    }

    private static string Line(string method, JsonObject parameters) =>
        new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters }.ToJsonString();

    private sealed class FakeTransport : IModelTransport
    {
        public int Calls { get; private set; }
        public string SeenPrompt { get; private set; } = "";
        public string SeenKey { get; private set; } = "";
        public int SeenMaxTokens { get; private set; }
        public Uri? SeenEndpoint { get; private set; }
        public string Reply { get; init; } = "ok";
        public int Tokens { get; init; } = 3;
        public TimeSpan Delay { get; init; }

        public async Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct)
        {
            Calls++;
            SeenEndpoint = endpoint;
            SeenKey = key;
            SeenPrompt = prompt;
            SeenMaxTokens = maxTokens;
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
            return new ModelTransportResult(Reply, Tokens);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public bool Called { get; private set; }
        public string Authorization { get; private set; } = "";
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Called = true;
            Authorization = request.Headers.Authorization?.ToString() ?? "";
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var json = """{"choices":[{"message":{"content":"resposta"}}],"usage":{"total_tokens":4}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
