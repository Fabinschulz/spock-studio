using System.Text;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;
using Xunit.Abstractions;

namespace Mariliz.Application.Tests;

// Cada teste só roda se a pessoa definiu as variáveis de ambiente dele (LiveFactAttribute); sem elas aparece como ignorado. As credenciais nunca passam pelo código-fonte, pela conversa ou pelo relatório: o relatório traz só contagens, estados e a prova de que nenhum segredo foi parar em disco.
public class LiveValidationTests(ITestOutputHelper output)
{
    [LiveFact(LiveWorld.GitHubToken, LiveWorld.GitHubRepository)]
    public async Task GitHub_real_sincroniza_sem_duplicar_e_sem_gravar_a_credencial()
    {
        using var world = new LiveWorld();
        var token = Env(LiveWorld.GitHubToken);
        var repository = Env(LiveWorld.GitHubRepository);

        var connection = await world.Hub.Connections.CreateAsync(
            IntegrationProviders.GitHub, "Validação real", new Dictionary<string, string>(), token, CancellationToken.None);
        Report("GitHub: conexão", connection.Status, $"escopos: {string.Join(", ", connection.Scopes)}");
        Assert.Equal(ConnectionStatus.Connected, connection.Status);
        Assert.False(string.IsNullOrWhiteSpace(connection.Account));

        var tested = await world.Hub.Connections.TestAsync(connection.Id, CancellationToken.None);
        Assert.Equal(ConnectionStatus.Connected, tested.Status);

        var link = await world.Hub.Projects.LinkAsync(
            world.ProjectId, connection.Id, repository, [DataKinds.Issues, DataKinds.PullRequests], CancellationToken.None);
        Assert.Equal(repository, link.Scope.Resource, ignoreCase: true);

        var first = await world.Hub.Sync.RunToEndAsync(link.ProjectId, link.Id, CancellationToken.None);
        ReportSync("GitHub: 1ª sincronização", first);
        AssertConsistent(first);
        var afterFirst = world.Hub.Projects.Data(link);

        var second = await world.Hub.Sync.RunToEndAsync(link.ProjectId, link.Id, CancellationToken.None);
        ReportSync("GitHub: 2ª sincronização", second);
        AssertConsistent(second);
        var afterSecond = world.Hub.Projects.Data(link);

        Assert.True(second.Incremental, "A segunda sincronização devia ser incremental.");
        Assert.True(second.Fetched <= first.Fetched || first.Status == SyncStatus.Partial);
        // Idempotência: o que a segunda trouxe de novo é exatamente o que cresceu; nada foi duplicado.
        Assert.Equal(afterFirst.Items.Count + second.Created, afterSecond.Items.Count);

        var foreign = afterSecond.Items.Values.Count(stored => !stored.Item.Url.Contains(repository, StringComparison.OrdinalIgnoreCase));
        Report("GitHub: itens", afterSecond.Items.Count, $"fora do repositório vinculado: {foreign}");
        Assert.Equal(0, foreign);

        world.AssertNoSecretOnDisk(token);
        world.Hub.Connections.Remove(connection.Id);
        world.AssertNoSecretOnDisk(token);
        Report("GitHub: credencial em disco", "nenhuma", "nem antes nem depois de remover a conexão");
    }

    [LiveFact(LiveWorld.JiraSite, LiveWorld.JiraEmail, LiveWorld.JiraToken, LiveWorld.JiraProject)]
    public async Task Jira_real_sincroniza_sem_duplicar_e_sem_gravar_a_credencial()
    {
        using var world = new LiveWorld();
        var token = Env(LiveWorld.JiraToken);
        var project = Env(LiveWorld.JiraProject);

        var connection = await world.Hub.Connections.CreateAsync(
            IntegrationProviders.Jira, "Validação real",
            new Dictionary<string, string> { ["site"] = Env(LiveWorld.JiraSite), ["email"] = Env(LiveWorld.JiraEmail) },
            token, CancellationToken.None);
        Report("Jira: conexão", connection.Status, "");
        Assert.Equal(ConnectionStatus.Connected, connection.Status);

        var link = await world.Hub.Projects.LinkAsync(
            world.ProjectId, connection.Id, project, [DataKinds.Issues], CancellationToken.None);

        var first = await world.Hub.Sync.RunToEndAsync(link.ProjectId, link.Id, CancellationToken.None);
        ReportSync("Jira: 1ª sincronização", first);
        AssertConsistent(first);
        var afterFirst = world.Hub.Projects.Data(link);

        var second = await world.Hub.Sync.RunToEndAsync(link.ProjectId, link.Id, CancellationToken.None);
        ReportSync("Jira: 2ª sincronização", second);
        AssertConsistent(second);
        var afterSecond = world.Hub.Projects.Data(link);

        Assert.True(second.Incremental, "A segunda sincronização devia ser incremental.");
        Assert.Equal(afterFirst.Items.Count + second.Created, afterSecond.Items.Count);

        var foreign = afterSecond.Items.Values.Count(stored =>
            !stored.Item.ExternalId.StartsWith(project + "-", StringComparison.OrdinalIgnoreCase));
        Report("Jira: itens", afterSecond.Items.Count, $"fora do projeto vinculado: {foreign}");
        Assert.Equal(0, foreign);

        world.AssertNoSecretOnDisk(token);
        world.Hub.Connections.Remove(connection.Id);
        world.AssertNoSecretOnDisk(token);
        Report("Jira: credencial em disco", "nenhuma", "nem antes nem depois de remover a conexão");
    }

    [LiveFact(LiveWorld.OpenAiKey)]
    public async Task Modelo_real_guarda_a_chave_no_cofre_e_o_assistente_responde_conferido()
    {
        using var world = new LiveWorld();
        var key = Env(LiveWorld.OpenAiKey);
        Assert.True(world.Hub.VaultAvailable, "Sem cofre neste sistema a chave ficaria em texto puro: rode no Windows.");

        var settings = new LocalModelSettings(world.DataDir, LocalIntegrationHub.SecretVault(world.DataDir));
        settings.Save(ModelProviders.OpenAi.Id, key);
        world.AssertNoSecretOnDisk(key);
        Assert.False(File.Exists(Path.Combine(world.DataDir, "model.json")));
        Report("Modelo: chave", "no cofre", "nenhum arquivo contém a chave");

        using var transport = new HttpsModelTransport();
        var gateway = new ModelGateway(settings, transport, timeout: TimeSpan.FromSeconds(30), chat: transport);
        var completion = await gateway.ChatAsync(
            new ChatRequest([new ChatMessage("user", "Responda apenas com a palavra: ok")], 16, false), CancellationToken.None);
        Report("Modelo: conversa", "respondeu", $"tokens: {completion.InputTokens + completion.OutputTokens}");
        Assert.False(string.IsNullOrWhiteSpace(completion.Text));
        Assert.DoesNotContain(key, completion.Text);

        // O assistente sobre um projeto real quando há GitHub definido; senão sobre um projeto sem dados, o que também
        // vale: o modelo precisa dizer o que não tem em vez de inventar, e a resposta passa pela conferência do motor.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LiveWorld.GitHubToken))
            && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LiveWorld.GitHubRepository)))
        {
            var connection = await world.Hub.Connections.CreateAsync(
                IntegrationProviders.GitHub, "Validação real", new Dictionary<string, string>(),
                Env(LiveWorld.GitHubToken), CancellationToken.None);
            var link = await world.Hub.Projects.LinkAsync(
                world.ProjectId, connection.Id, Env(LiveWorld.GitHubRepository), [DataKinds.Issues, DataKinds.PullRequests], CancellationToken.None);
            var sync = await world.Hub.Sync.RunToEndAsync(link.ProjectId, link.Id, CancellationToken.None);
            ReportSync("Modelo: dados para o assistente", sync);
        }

        var management = new ManagementService(world.Workspace, world.Hub, world.Clock);
        var assistant = new ManagementAssistant(management, world.Hub, world.Workspace, gateway, world.Clock);
        var answer = await assistant.AskAsync(world.ProjectId, "Como está o fluxo de entrega deste projeto?", CancellationToken.None);

        var statements = answer.Observed.Count + answer.Calculations.Count + answer.Inferences.Count + answer.Recommendations.Count;
        Report("Assistente: resposta", "conferida",
            $"afirmações aceitas: {statements}; descartadas pela conferência: {answer.Rejected.Count}; fatos citados: {answer.Evidence.Count}; "
            + $"modelo: {answer.Model}; tokens: {answer.InputTokens}+{answer.OutputTokens}");
        Assert.False(string.IsNullOrWhiteSpace(answer.Summary));
        Assert.All(answer.Observed.Concat(answer.Calculations).Concat(answer.Inferences).Concat(answer.Recommendations),
            statement => Assert.NotEmpty(statement.Evidence));
        Assert.DoesNotContain(key, answer.Summary);

        world.AssertNoSecretOnDisk(key);
        Report("Modelo: chave em disco", "nenhuma", "nem após as chamadas nem na auditoria");
    }

    private static string Env(string name) => Environment.GetEnvironmentVariable(name)!.Trim();

    private void Report(string what, object value, string detail) =>
        output.WriteLine($"[validação real] {what}: {value}{(detail.Length == 0 ? "" : " (" + detail + ")")}");

    private void ReportSync(string what, SyncExecution run) =>
        Report(what, run.Status,
            $"páginas: {run.Pages}; trazidos: {run.Fetched}; novos: {run.Created}; alterados: {run.Updated}; iguais: {run.Unchanged}; "
            + $"incremental: {run.Incremental}{(run.Error is null ? "" : "; erro: " + run.Error.Code)}");

    private static void AssertConsistent(SyncExecution run)
    {
        Assert.True(run.Status is SyncStatus.Succeeded or SyncStatus.Partial,
            $"A sincronização terminou como {run.Status} ({run.Error?.Code}).");
        Assert.Equal(run.Fetched, run.Created + run.Updated + run.Unchanged);
    }

    // Numa pasta temporária que some ao fim.
    private sealed class LiveWorld : IDisposable
    {
        public const string GitHubToken = "MARILIZ_LIVE_GITHUB_TOKEN";
        public const string GitHubRepository = "MARILIZ_LIVE_GITHUB_REPO";
        public const string JiraSite = "MARILIZ_LIVE_JIRA_SITE";
        public const string JiraEmail = "MARILIZ_LIVE_JIRA_EMAIL";
        public const string JiraToken = "MARILIZ_LIVE_JIRA_TOKEN";
        public const string JiraProject = "MARILIZ_LIVE_JIRA_PROJECT";
        public const string OpenAiKey = "MARILIZ_LIVE_OPENAI_KEY";

        private readonly TempProject _project = TempProject.Dotnet();
        private readonly HttpsConnectorTransport _transport = new();

        public LiveWorld()
        {
            Clock = new SystemClock();
            Workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
            ProjectId = Workspace.Authorize(_project.Root).Project!.ProjectId;
            Hub = LocalIntegrationHub.Create(DataDir, Workspace, Clock, _transport);
        }

        public IClock Clock { get; }
        public WorkspaceAuthorizer Workspace { get; }
        public IntegrationHub Hub { get; }
        public string ProjectId { get; }
        public string DataDir => _project.DataDir;

        // Nem em texto puro em nenhum arquivo do motor nem do projeto, auditoria inclusa.
        public void AssertNoSecretOnDisk(string secret)
        {
            foreach (var file in Directory.EnumerateFiles(DataDir, "*", SearchOption.AllDirectories)
                         .Concat(Directory.EnumerateFiles(_project.Root, "*", SearchOption.AllDirectories)))
                Assert.DoesNotContain(secret, Encoding.UTF8.GetString(File.ReadAllBytes(file)));
        }

        public void Dispose()
        {
            _transport.Dispose();
            _project.Dispose();
        }
    }
}

// Sem as variáveis pedidas aparece como ignorado, nunca como aprovado: um verde que não testou nada é pior que um aviso.
[AttributeUsage(AttributeTargets.Method)]
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute(params string[] variables)
    {
        var missing = variables.Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))).ToArray();
        if (missing.Length > 0) Skip = $"Validação real: defina {string.Join(", ", missing)} no seu terminal.";
    }
}
