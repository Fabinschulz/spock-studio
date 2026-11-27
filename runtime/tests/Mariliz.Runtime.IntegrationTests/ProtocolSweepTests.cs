using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mariliz.Runtime.IntegrationTests;

// Varredura rota por rota com o motor real (processo, NDJSON): cada método anunciado no engine.hello recebe parâmetros ausentes, de tipo
// errado, hostis (caminho para fora do projeto, texto enorme, caracteres de controle) e o motor tem de responder sempre com um envelope
// válido e um erro de nome estável, nunca com INTERNAL_ERROR (uma exceção que escapou da validação), nunca com a pilha no texto, nunca
// travando, e continuar vivo no fim. Nada aqui usa modelo, rede, nuvem nem credencial. Um achado é um bug: a entrada inválida devia
// virar INVALID_PARAMS (ou outro erro de domínio), não uma falha interna.
public sealed partial class ProtocolSweepTests : IAsyncLifetime
{
    private static readonly TimeSpan PerCall = TimeSpan.FromSeconds(20);

    // Fora da varredura, com o motivo: o que baixa da internet, fala com nuvem ou encerra o motor não é entrada de teste.
    private static readonly (string Prefix, string Reason)[] Excluded =
    [
        ("engine.shutdown", "encerra o motor"),
        ("iac.runtime.install", "baixa o Terraform da internet"),
        ("cloud.", "fala com a nuvem com o perfil da pessoa"),
    ];

    private static readonly string[] Keys =
    [
        "projectId", "path", "text", "question", "goal", "id", "runId", "bootId", "connectionId", "linkId", "provider", "key", "name",
        "stage", "kind", "limit", "offset", "autonomy", "scope", "resource", "approvalId", "decision", "title", "content"
    ];

    private readonly List<EngineClient> _engines = [];
    private readonly List<string> _folders = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var engine in _engines) await engine.DisposeAsync();
        foreach (var folder in _folders)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
                    break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Thread.Sleep(200);
                }
            }
        }
    }

    private string NewFolder(string prefix)
    {
        var folder = Directory.CreateTempSubdirectory(prefix).FullName;
        _folders.Add(folder);
        return folder;
    }

    private static void Write(string root, string relative, string content)
    {
        var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content, new UTF8Encoding(false));
    }

    private string NewProject()
    {
        var root = NewFolder("spock-varredura-projeto-");
        Write(root, "backend/App.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        Write(root, "backend/src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        Write(root, "docs/leia-me.md", "# App\n\nUm serviço de pedidos.\n");
        return root;
    }

    private static JsonObject All(JsonNode? value)
    {
        var parameters = new JsonObject();
        foreach (var key in Keys) parameters[key] = value?.DeepClone();
        return parameters;
    }

    // Os pedidos hostis de cada método. O primeiro é "sem params"; o projeto é válido nos que dizem projectId.
    private static IEnumerable<(string Name, JsonObject? Params)> Variants(string projectId)
    {
        yield return ("sem params", null);
        yield return ("params vazio", new JsonObject());
        yield return ("só o projeto", new JsonObject { ["projectId"] = projectId });
        yield return ("todos inteiros", All(12345));
        yield return ("todos nulos", All(null));
        yield return ("todos listas", All(new JsonArray()));
        yield return ("todos objetos", All(new JsonObject()));
        yield return ("todos booleanos", All(true));
        yield return ("caminho para fora", All("../../../Windows/System32"));
        yield return ("caminho absoluto", All(Path.GetPathRoot(Path.GetTempPath())));
        yield return ("controle", All("\u0000\u001b[31m\r\nx" + (char)0x2028));
        // Dentro do limite do protocolo (4 MB): os handlers recebem texto grande de verdade. 24 chaves x 100 mil = 2,4 MB.
        yield return ("texto grande dentro do limite", All(new string('A', 100_000)));
        var withProject = All("x");
        withProject["projectId"] = projectId;
        yield return ("projeto válido e o resto lixo", withProject);
        var extra = new JsonObject { ["projectId"] = projectId, ["__proto__"] = new JsonObject { ["admin"] = true }, ["$ref"] = "#/x" };
        yield return ("campos estranhos", extra);
    }

    [GeneratedRegex(@"(^|\n)\s+at \w|--- End of|System\.\w+\.|Mariliz\.\w+\.|\.cs:line \d+")]
    private static partial Regex StackTrace();

    private EngineClient StartAuthorized(string data, string project, out Task<string> projectId)
    {
        var engine = EngineClient.Start(data);
        _engines.Add(engine);
        projectId = Task.Run(async () =>
        {
            await engine.HelloAsync();
            return await engine.AuthorizeAsync(project);
        });
        return engine;
    }

    [Fact]
    public async Task Mensagem_acima_do_limite_volta_com_o_id_do_pedido_pelo_processo_real_e_o_motor_segue_respondendo()
    {
        var engine = EngineClient.Start(NewFolder("spock-varredura-dados-"));
        _engines.Add(engine);
        await engine.HelloAsync();

        // 4,2 MB de texto: acima dos 4 MB do protocolo. Sem o id, o cliente esperaria até o prazo por uma resposta que nunca chega.
        var response = await engine.CallAsync("engine.hello", new JsonObject { ["texto"] = new string('A', 4_200_000) }, TimeSpan.FromSeconds(15));

        Assert.Equal("MESSAGE_TOO_LARGE", response["error"]?["code"]?.GetValue<string>());
        Assert.StartsWith("t", response["id"]?.GetValue<string>());
        // O motor continua atendendo depois da mensagem recusada.
        Assert.NotNull(await engine.HelloAsync());
        await engine.ShutdownAsync();
    }

    [Fact]
    public async Task Toda_rota_responde_com_erro_estruturado_a_parametros_hostis_e_o_motor_continua_vivo()
    {
        var data = NewFolder("spock-varredura-dados-");
        var project = NewProject();
        var engine = StartAuthorized(data, project, out var authorized);
        var projectId = await authorized;
        var hello = await engine.HelloAsync();
        var methods = hello["capabilities"]!.AsArray().Select(node => node!.GetValue<string>()).Order(StringComparer.Ordinal).ToList();
        var swept = methods.Where(method => Excluded.All(rule => !method.StartsWith(rule.Prefix, StringComparison.Ordinal))).ToList();

        var findings = new List<string>();
        var calls = 0;
        var slow = new List<string>();
        foreach (var method in swept)
        {
            foreach (var (variant, parameters) in Variants(projectId))
            {
                calls++;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                JsonObject response;
                try
                {
                    response = await engine.CallAsync(method, parameters?.DeepClone().AsObject(), PerCall);
                }
                catch (Exception ex) when (ex is TimeoutException or InvalidOperationException)
                {
                    findings.Add($"{method} [{variant}]: sem resposta ({ex.GetType().Name}); motor vivo: {!engine.HasExited}");
                    // Um motor que travou ou morreu não responde ao resto: sobe outro para continuar a varredura.
                    engine = StartAuthorized(data, project, out authorized);
                    projectId = await authorized;
                    continue;
                }
                if (watch.Elapsed > TimeSpan.FromSeconds(5)) slow.Add($"{method} [{variant}]: {watch.Elapsed.TotalSeconds:0.0}s");

                var error = response["error"] as JsonObject;
                var code = error?["code"]?.GetValue<string>();
                var message = error?["message"]?.GetValue<string>() ?? "";
                if (response["result"] is null && error is null) findings.Add($"{method} [{variant}]: resposta sem result nem error");
                if (error is not null && string.IsNullOrWhiteSpace(code)) findings.Add($"{method} [{variant}]: erro sem código");
                if (code == "INTERNAL_ERROR") findings.Add($"{method} [{variant}]: INTERNAL_ERROR: {message}");
                if (StackTrace().IsMatch(message)) findings.Add($"{method} [{variant}]: pilha no texto do erro: {message[..Math.Min(120, message.Length)]}");
            }
        }

        var alive = !engine.HasExited;
        var stderr = engine.Stderr;
        if (stderr.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase)) findings.Add("stderr: exceção não tratada no motor");

        var report = new StringBuilder()
            .AppendLine($"Varredura: {swept.Count} de {methods.Count} métodos, {calls} pedidos hostis; excluídos: {string.Join(", ", Excluded.Select(rule => $"{rule.Prefix} ({rule.Reason})"))}.")
            .AppendLine($"Pedidos acima de 5 s: {(slow.Count == 0 ? "nenhum" : string.Join("; ", slow.Take(10)))}.")
            .AppendLine($"Achados: {findings.Count}");
        foreach (var finding in findings.Distinct().Take(80)) report.AppendLine("  - " + finding);
        Console.WriteLine(report);

        Assert.True(alive, "O motor morreu durante a varredura.\n" + report);
        Assert.True(findings.Count == 0, report.ToString());
        await engine.ShutdownAsync();
    }
}
