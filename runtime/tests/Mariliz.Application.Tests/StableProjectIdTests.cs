using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Mcp;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Core.Plan;
using Mariliz.Core.Text;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// F1: o id do projeto era o hash do caminho, então clone, worktree e outra máquina viravam outro projeto. Agora o
// mariliz.yaml carrega project.id (spk-), gravado por plano aprovado; o p-<hash> continua sendo a chave de cada cópia.
public sealed class StableProjectIdTests : IDisposable
{
    private const string Id = "spk-01JD2Y8Q4W0000000000000000";
    private const string Declaration = """
        # Declaração do Harness.
        schema: 1
        project:
          name: loja # nome curto
        implementationModel: mariliz
        aiEnvironment: claude-code
        components:
          exclude: []
        evals:
          required: []

        """;

    private readonly List<TempProject> _projects = [];

    public void Dispose()
    {
        foreach (var project in _projects) project.Dispose();
    }

    private TempProject NewProject()
    {
        var project = TempProject.Dotnet();
        _projects.Add(project);
        return project;
    }

    [Fact]
    public void O_id_e_um_ulid_com_prefixo_e_cada_um_e_unico()
    {
        var now = new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
        var ids = Enumerable.Range(0, 200).Select(_ => StableProjectId.New(now)).ToList();

        Assert.All(ids, id => Assert.True(StableProjectId.IsValid(id), id));
        Assert.Equal(ids.Count, ids.Distinct().Count());
        // Os 10 primeiros caracteres são o tempo: ids do mesmo instante dividem o começo.
        Assert.Single(ids.Select(id => id[..14]).Distinct());
        Assert.True(string.CompareOrdinal(StableProjectId.New(now.AddSeconds(1)), StableProjectId.New(now)) > 0);
        Assert.True(StableProjectId.IsValid(Id));
        foreach (var bad in new[] { "", "p-0123456789ab", "spk-", "spk-01jd2y8q4w0000000000000000", "spk-01JD2Y8Q4W000000000000000I", "spk-81JD2Y8Q4W0000000000000000", Id + "0" })
            Assert.False(StableProjectId.IsValid(bad), bad);
    }

    [Fact]
    public void Editor_acrescenta_o_id_no_bloco_project_e_preserva_o_resto()
    {
        var edited = DeclarationEditor.WithProjectId(Declaration, Id)!;

        Assert.Equal(Declaration.Replace("project:\n", $"project:\n  id: {Id}\n"), edited);
        Assert.Equal(Id, DeclaredConfig.StableIdIn(edited));
        // Sem o bloco project, ele entra antes da primeira chave, depois dos comentários do topo.
        var withoutProject = DeclarationEditor.WithProjectId(Declaration.Replace("project:\n  name: loja # nome curto\n", ""), Id)!;
        Assert.StartsWith($"# Declaração do Harness.\nproject:\n  id: {Id}\nschema: 1", withoutProject);
        // Um id que já está lá não é trocado, e formato que não reconhece não é editado.
        Assert.Null(DeclarationEditor.WithProjectId(edited, "spk-01JD2Y8Q4W1111111111111111"));
        Assert.Null(DeclarationEditor.WithProjectId(Declaration.Replace("project:\n  name: loja # nome curto", "project: { name: loja }"), Id));
    }

    [Fact]
    public void Declaracao_com_id_que_nao_e_do_spock_e_recusada()
    {
        var project = NewProject();
        project.Write("mariliz.yaml", Declaration.Replace("project:\n", "project:\n  id: meu-projeto\n"));

        var (config, error) = DeclaredConfig.TryRead(new TestFilesFactory().Open(project.Root));

        Assert.Null(config);
        Assert.Contains("project.id 'meu-projeto'", error);
    }

    [Fact]
    public async Task O_primeiro_plano_aprovado_grava_o_id_e_replanejar_nao_troca()
    {
        var project = NewProject();
        var journey = new Journey(project, Journey.FakeInstaller(project));

        var first = await journey.PlanAsync();
        var again = await journey.PlanAsync();
        var declaration = first.Changes.Single(change => change.Path == DeclaredConfig.FileName);
        Assert.Equal(ChangeAction.Create, declaration.Action);
        Assert.Equal(declaration.AfterHash, again.Changes.Single(change => change.Path == DeclaredConfig.FileName).AfterHash);

        await journey.ApplyAsync(again);

        var id = DeclaredConfig.StableIdIn(project.Read(DeclaredConfig.FileName));
        Assert.True(StableProjectId.IsValid(id));
        var after = await journey.PlanAsync();
        Assert.DoesNotContain(after.Changes, change => change.Path == DeclaredConfig.FileName && change.Action != ChangeAction.Preserve);
    }

    [Fact]
    public async Task Declaracao_antiga_sem_id_ganha_so_a_linha_do_id_com_aprovacao()
    {
        var project = NewProject();
        var journey = new Journey(project, Journey.FakeInstaller(project));
        await journey.ApplyAsync(await journey.PlanAsync());
        // Como um projeto configurado antes do F1: a declaração existe, sem id.
        var old = project.Read(DeclaredConfig.FileName)!.Replace("\r\n", "\n");
        var lines = old.Split('\n').Where(line => !line.TrimStart().StartsWith("id: spk-", StringComparison.Ordinal));
        project.Write(DeclaredConfig.FileName, string.Join("\n", lines));
        var withoutId = project.Read(DeclaredConfig.FileName)!;

        var plan = await journey.PlanAsync();
        var edit = plan.Changes.Single(change => change.Path == DeclaredConfig.FileName);
        Assert.Equal(ChangeAction.Update, edit.Action);
        Assert.Contains("project.id", edit.Reason);
        // Sem aprovar, nada muda.
        await journey.ApplyAsync(plan, new ApplyApprovals([], [], new Dictionary<string, ConflictResolution>()));
        Assert.Equal(withoutId, project.Read(DeclaredConfig.FileName));

        await journey.ApplyAsync(await journey.PlanAsync());

        var written = project.Read(DeclaredConfig.FileName)!.Replace("\r\n", "\n");
        var id = DeclaredConfig.StableIdIn(written)!;
        Assert.Equal(withoutId.Replace("\r\n", "\n").Replace("project:\n", $"project:\n  id: {id}\n"), written);
    }

    // Um projeto configurado antes do F1 não passa a falhar na validação: o id só entra no próximo plano que a pessoa gerar.
    [Fact]
    public async Task Validar_um_projeto_sem_id_da_o_mesmo_resultado_de_antes()
    {
        var project = NewProject();
        var journey = new Journey(project, Journey.FakeInstaller(project));
        await journey.ApplyAsync(await journey.PlanAsync());
        var withId = await journey.ValidateAsync();
        var lines = project.Read(DeclaredConfig.FileName)!.Replace("\r\n", "\n").Split('\n')
            .Where(line => !line.TrimStart().StartsWith("id: spk-", StringComparison.Ordinal));
        project.Write(DeclaredConfig.FileName, string.Join("\n", lines));

        var withoutId = await journey.ValidateAsync();

        Assert.Equal(withId["outcome"]!.GetValue<string>(), withoutId["outcome"]!.GetValue<string>());
        Assert.Null(DeclaredConfig.StableIdIn(project.Read(DeclaredConfig.FileName)));
    }

    [Fact]
    public void O_id_proposto_sobrevive_a_reiniciar_e_o_registro_antigo_continua_lendo()
    {
        var project = NewProject();
        File.WriteAllText(Path.Combine(project.DataDir, "workspaces.json"),
            new System.Text.Json.Nodes.JsonObject { ["projects"] = new System.Text.Json.Nodes.JsonObject { ["p-0123456789ab"] = project.Root } }.ToJsonString());
        WorkspaceAuthorizer Open() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(project.DataDir));

        var projectId = Open().Authorize(project.Root).Project!.ProjectId;
        var proposed = Open().ProposedStableId(projectId);
        Open().Authorize(project.Root);

        Assert.Equal(proposed, Open().ProposedStableId(projectId));
        Assert.Null(Open().StableIdOf(projectId));
        // A seção antiga continua no formato de antes: uma versão anterior do aplicativo segue lendo o arquivo.
        var registry = new FileWorkspaceRegistry(project.DataDir);
        Assert.Equal(project.Root, registry.Load()[projectId]);
        Assert.Equal(proposed, registry.LoadProposedStableIds()[projectId]);
        Assert.True(registry.Load().ContainsKey("p-0123456789ab"));
    }

    // Aceite do F1: o mesmo repositório em duas pastas resolve para o mesmo projeto, cada uma com o próprio estado.
    [Fact]
    public void O_mesmo_repositorio_em_duas_pastas_e_o_mesmo_projeto_e_cada_copia_tem_o_seu_estado()
    {
        var (original, clone) = (NewProject(), NewProject());
        foreach (var copy in new[] { original, clone }) copy.Write(DeclaredConfig.FileName, Declaration.Replace("project:\n", $"project:\n  id: {Id}\n"));
        var other = NewProject();
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(original.DataDir));
        var first = workspace.Authorize(original.Root).Project!.ProjectId;
        var second = workspace.Authorize(clone.Root).Project!.ProjectId;
        workspace.Authorize(other.Root);

        Assert.NotEqual(first, second);
        Assert.Equal(Id, workspace.StableIdOf(first));
        Assert.Equal(Id, workspace.StableIdOf(second));

        var scope = new HashSet<string>([Id], StringComparer.Ordinal);
        Assert.Equal(new[] { first, second }.Order(), new McpProjectResolver(workspace, original.Root, scope).Projects().Select(p => p.ProjectId).Order());
        Assert.Equal(first, new McpProjectResolver(workspace, Path.Combine(original.Root, "src"), scope).Resolve(null).ProjectId);
        Assert.Equal(second, new McpProjectResolver(workspace, clone.Root, scope).Resolve(null).ProjectId);
        // Fora das duas cópias, o id sozinho não escolhe: nunca a cópia errada por engano.
        Assert.Throws<McpToolException>(() => new McpProjectResolver(workspace, other.Root, scope).Resolve(null));
        Assert.Throws<McpToolException>(() => new McpProjectResolver(workspace, other.Root, scope).Resolve(Id));
        Assert.Equal(first, new McpProjectResolver(workspace, other.Root, scope).Resolve(first).ProjectId);

        var server = new McpServer("1.2.3", new ProjectContextTools(() => workspace, new TestFilesFactory(), new FakeClock(),
            new Infrastructure.Engineering.FileTechnologyProfileStore(original.DataDir), new Infrastructure.Integrations.FileDecisionStore(original.DataDir),
            clone.Root, scope).Tools());
        var listed = server.HandleAsync(new System.Text.Json.Nodes.JsonObject
        {
            ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call",
            ["params"] = new System.Text.Json.Nodes.JsonObject { ["name"] = "list_projects", ["arguments"] = new System.Text.Json.Nodes.JsonObject() }
        }.ToJsonString()).GetAwaiter().GetResult()!["result"]!["content"]![0]!["text"]!.GetValue<string>();
        Assert.Equal(2, listed.Split($"projeto {Id}").Length - 1);
        Assert.Contains($"id {second} · projeto {Id}", listed);
        Assert.Contains("(pasta atual)", listed.Split('\n').Single(line => line.Contains(second)));
    }

    [Fact]
    public void Project_dir_le_o_id_subindo_ate_o_mariliz_yaml_e_pasta_sem_id_recusa()
    {
        var (with, withoutId, without) = (NewProject(), NewProject(), NewProject());
        with.Write(DeclaredConfig.FileName, Declaration.Replace("project:\n", $"project:\n  id: {Id}\n"));
        with.Write("src/Loja/Program.cs", "class P {}");
        withoutId.Write(DeclaredConfig.FileName, Declaration);
        static string? Read(string directory) =>
            File.Exists(Path.Combine(directory, DeclaredConfig.FileName)) ? File.ReadAllText(Path.Combine(directory, DeclaredConfig.FileName)) : null;

        var resolved = McpOptions.Parse(["--project-dir", Path.Combine(with.Root, "src", "Loja")]).WithDirectoryResolved(Read);

        Assert.Equal([Id], resolved.Scope);
        Assert.Equal(Path.Combine(with.Root, "src", "Loja"), resolved.ProjectDirectory);
        Assert.Contains("não tem project.id", Assert.Throws<McpOptionsException>(() =>
            McpOptions.Parse(["--project-dir", withoutId.Root]).WithDirectoryResolved(Read)).Message);
        Assert.Contains("Nenhum mariliz.yaml", Assert.Throws<McpOptionsException>(() =>
            McpOptions.Parse(["--project-dir", without.Root]).WithDirectoryResolved(Read)).Message);
    }

    [Theory]
    [InlineData("--project", Id)]
    [InlineData("--project", "p-0123456789ab")]
    public void Project_aceita_o_id_da_copia_e_o_id_estavel(string argument, string value)
    {
        Assert.Equal([value], McpOptions.Parse([argument, value]).Scope);
    }

    [Theory]
    [InlineData("--project", "spk-123")]
    [InlineData("--project-dir")]
    [InlineData("--project-dir", "a", "--project-dir", "b")]
    public void Argumento_de_vinculo_malformado_derruba(params string[] args)
    {
        Assert.Throws<McpOptionsException>(() => McpOptions.Parse(args));
    }
}
