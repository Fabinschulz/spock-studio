using System.Diagnostics;
using System.Text;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Runtime.IntegrationTests;

// C2: o que o lançador spock repassa ao motor. Os hooks da IDE chamam "context session" e "context prompt"; um argumento que o motor não
// entende não pode virar um sidecar parado esperando o protocolo.
public sealed class EngineCommandTests : IDisposable
{
    private readonly string _data = Directory.CreateTempSubdirectory("spock-dados-").FullName;
    private readonly string _project = Directory.CreateTempSubdirectory("spock-projeto-").FullName;

    public void Dispose()
    {
        Directory.Delete(_data, recursive: true);
        Directory.Delete(_project, recursive: true);
    }

    private Task<(int Code, string Out, string Err)> Run(params string[] arguments) => RunWith(null, arguments);

    // input é o JSON que a IDE manda na entrada do hook.
    private async Task<(int Code, string Out, string Err)> RunWith(string? input, params string[] arguments)
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "Mariliz.Runtime.dll");
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, UseShellExecute = false,
            // Os testes não leem nem gravam os dados de verdade da pessoa.
            Environment = { ["MARILIZ_DATA_DIR"] = _data },
            WorkingDirectory = Path.GetTempPath()
        };
        // Quem roda o teste dentro do Claude Code herdaria a pasta dele, que tem precedência sobre o cwd da entrada.
        psi.Environment.Remove("CLAUDE_PROJECT_DIR");
        psi.ArgumentList.Add(dll);
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        using var engine = Process.Start(psi)!;
        if (input is not null) await engine.StandardInput.WriteAsync(input);
        engine.StandardInput.Close();
        var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token;
        var stdout = engine.StandardOutput.ReadToEndAsync(timeout);
        var stderr = engine.StandardError.ReadToEndAsync(timeout);
        await engine.WaitForExitAsync(timeout);
        return (engine.ExitCode, await stdout, await stderr);
    }

    [Fact]
    public async Task Version_responde_e_argumento_desconhecido_nao_vira_sidecar()
    {
        var version = await Run("version");
        var unknown = await Run("ajuda");

        Assert.Equal(0, version.Code);
        Assert.Matches(@"^\d+\.\d+\.\d+", version.Out.Trim());
        Assert.Equal(2, unknown.Code);
        Assert.Equal(string.Empty, unknown.Out);
        Assert.Contains("spock mcp --project-dir", unknown.Err);
    }

    [Fact]
    public async Task Hook_de_sessao_entrega_o_contexto_da_pasta_e_registra_na_trilha()
    {
        Directory.CreateDirectory(Path.Combine(_project, "src"));
        File.WriteAllText(Path.Combine(_project, "spock.yaml"),
            "schema: 1\nproject:\n  id: spk-01M7GTEK00ABCDEFGHJKMNPQRS\n  name: loja\nimplementationModel: mariliz\naiEnvironment: claude-code\n");
        var projectId = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_data)).Authorize(_project).Project!.ProjectId;

        var (code, stdout, _) = await Run("context", "session", "--project-dir", Path.Combine(_project, "src"));

        Assert.Equal(0, code);
        Assert.StartsWith("# Contexto do projeto", stdout);
        Assert.Contains("spk-01M7GTEK00ABCDEFGHJKMNPQRS", stdout);
        var trail = string.Join("\n", Directory.EnumerateFiles(_data, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Contains("ide.bootstrap", trail);
        Assert.Contains(projectId, trail);
        Assert.Contains("ctx-", trail);
    }

    [Fact]
    public async Task Hook_por_prompt_sugere_uma_vez_por_sessao_e_nao_grava_o_prompt()
    {
        File.WriteAllText(Path.Combine(_project, "spock.yaml"),
            "schema: 1\nproject:\n  id: spk-01M7GTEK00ABCDEFGHJKMNPQRS\n  name: loja\nimplementationModel: mariliz\naiEnvironment: claude-code\n");
        new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_data)).Authorize(_project);
        var cwd = System.Text.Json.JsonSerializer.Serialize(_project);
        const string secret = "Como está o drift do Terraform da conta zebra-azul?";
        var prompt = $$"""{"session_id":"s-1","hook_event_name":"UserPromptSubmit","cwd":{{cwd}},"prompt":"{{secret}}"}""";

        var opened = await RunWith($$"""{"session_id":"s-1","hook_event_name":"SessionStart","cwd":{{cwd}}}""", "context", "session");
        var first = await RunWith(prompt, "context", "prompt");
        var again = await RunWith(prompt, "context", "prompt");
        var noSession = await RunWith($$"""{"cwd":{{cwd}},"prompt":"{{secret}}"}""", "context", "prompt");

        Assert.Equal(0, opened.Code);
        Assert.StartsWith("# Contexto do projeto", opened.Out);
        Assert.Equal(0, first.Code);
        Assert.StartsWith("Spock:\n- Para infraestrutura", first.Out.ReplaceLineEndings("\n"));
        Assert.Equal((0, ""), (again.Code, again.Out));
        Assert.Equal((0, ""), (noSession.Code, noSession.Out));
        var trail = string.Join("\n", Directory.EnumerateFiles(_data, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Contains("ide.prompt", trail);
        Assert.Contains("hint:infrastructure", trail);
        Assert.DoesNotContain("zebra-azul", trail);
    }

    // C3: o Cursor lê o contexto de um JSON (additional_context) e manda a pasta em workspace_roots, não em cwd.
    [Fact]
    public async Task Hook_de_sessao_do_cursor_responde_em_json_pela_pasta_do_workspace()
    {
        File.WriteAllText(Path.Combine(_project, "spock.yaml"),
            "schema: 1\nproject:\n  id: spk-01M7GTEK00ABCDEFGHJKMNPQRS\n  name: loja\nimplementationModel: mariliz\naiEnvironment: cursor\n");
        new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_data)).Authorize(_project);
        var roots = System.Text.Json.JsonSerializer.Serialize(new[] { _project });
        var input = $$"""{"session_id":"c-1","hook_event_name":"sessionStart","workspace_roots":{{roots}},"is_background_agent":false}""";

        var inside = await RunWith(input, "context", "session", "--ide", "cursor");
        var outside = await RunWith("""{"session_id":"c-2"}""", "context", "session", "--ide", "cursor", "--project-dir", Path.GetTempPath());
        var other = await RunWith(input, "context", "session", "--ide", "vscode");

        Assert.Equal(0, inside.Code);
        var context = System.Text.Json.Nodes.JsonNode.Parse(inside.Out)!["additional_context"]!.GetValue<string>();
        Assert.StartsWith("# Contexto do projeto", context);
        Assert.Contains("spk-01M7GTEK00ABCDEFGHJKMNPQRS", context);
        Assert.Contains("não está aberta no Spock Studio", System.Text.Json.Nodes.JsonNode.Parse(outside.Out)!["additional_context"]!.GetValue<string>());
        Assert.Equal(2, other.Code);
    }

    [Fact]
    public async Task Hook_por_prompt_fora_de_projeto_fica_em_silencio()
    {
        var input = $$"""{"session_id":"s-1","prompt":"terraform"}""";

        var outside = await RunWith(input, "context", "prompt", "--project-dir", _project);
        var budget = await RunWith(input, "context", "prompt", "--budget", "500");

        Assert.Equal((0, ""), (outside.Code, outside.Out));
        Assert.Equal(2, budget.Code);
    }

    [Fact]
    public async Task Hook_de_sessao_fora_de_projeto_avisa_sem_falhar_a_sessao()
    {
        var outside = await Run("context", "session", "--project-dir", _project);
        var wrong = await Run("context", "outra-coisa");

        Assert.Equal(0, outside.Code);
        Assert.Contains("não está aberta no Spock Studio", outside.Out);
        Assert.Equal(2, wrong.Code);
        Assert.Equal(string.Empty, wrong.Out);
    }
}
