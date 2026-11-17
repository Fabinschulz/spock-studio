using System.Diagnostics;
using System.Text;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Runtime.IntegrationTests;

// C2: o que o lançador spock repassa ao motor. O hook de sessão da IDE chama "context session"; um argumento que o motor não
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

    private async Task<(int Code, string Out, string Err)> Run(params string[] arguments)
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "Mariliz.Runtime.dll");
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, UseShellExecute = false,
            // Os testes não leem nem gravam os dados de verdade da pessoa.
            Environment = { ["MARILIZ_DATA_DIR"] = _data },
            WorkingDirectory = Path.GetTempPath()
        };
        psi.ArgumentList.Add(dll);
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        using var engine = Process.Start(psi)!;
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
        File.WriteAllText(Path.Combine(_project, "mariliz.yaml"),
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
