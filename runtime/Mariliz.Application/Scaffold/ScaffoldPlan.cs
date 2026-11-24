using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Compatibility;
using Mariliz.Application.Discovery;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Scaffold;

/// <param name="CheckedAt">Quando o gerador do catálogo leu a fonte oficial: a data do registro.</param>
public sealed record ScaffoldTechnology(
    string Id,
    string Name,
    string Cycle,
    string Version,
    string Channel,
    string? Eol,
    string? Language,
    string Reason,
    string? Source,
    DateTimeOffset? CheckedAt);

/// <param name="Directory">Pasta, a partir da raiz do projeto, onde o comando roda ("." é a raiz).</param>
/// <param name="Content">Para os passos que gravam um arquivo, o conteúdo exato; nulo quando o passo é um comando.</param>
public sealed record ScaffoldStep(string Id, string Part, string Title, string Directory, string? Command, string? File, string? Content);

/// <param name="Problems">O que impede criar o projeto; com algum, o plano não traz passos.</param>
/// <param name="Verification">Comandos que o agente roda depois dos passos: build e testes de cada parte.</param>
public sealed record ScaffoldPlan(
    bool Supported,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Notes,
    string ProjectName,
    bool Backend,
    bool Frontend,
    IReadOnlyList<ScaffoldTechnology> Technologies,
    IReadOnlyList<ScaffoldStep> Steps,
    IReadOnlyList<ScaffoldStep> Verification,
    string RecordPath,
    string? Record);

// MV-6: da arquitetura aprovada em Da ideia ao projeto ao projeto criado. O plano é determinístico: a stack vem do documento
// aprovado, as versões da política do MV-5 (catálogo embutido e data), os comandos são os oficiais de cada ecossistema com a
// versão fixada. Quem executa é o agente da sessão; o motor não roda processo nem acessa a rede.
public static partial class ScaffoldPlans
{
    public const string RecordFile = "spock-versions.json";
    public const string BackendFolder = "backend";
    public const string FrontendFolder = "frontend";
    public const string Backend = "backend";
    public const string Frontend = "frontend";

    // O ciclo LTS ou STS só fixa a banda de recursos: "10.0.100" com latestFeature aceita qualquer SDK 10.0 corrigido.
    private const string SdkFeatureBand = ".100";
    private const string TypeScriptNote =
        "TypeScript fica na versão que o template do Vite traz: o catálogo ainda não tem a faixa que o typescript-eslint do template aceita, e a versão mais nova pode não ser aceita por ele.";

    public static ScaffoldPlan Plan(IProjectFiles files, string folderName, ReleaseCatalog catalog, DateOnly today)
    {
        var name = ProjectName(folderName);
        var documents = ArchitectureDocuments.Read(files);
        if (!documents.Present.Contains(documents.ArchitectureFile))
            return Unsupported(name, ["Grave antes a especificação, a arquitetura e o backlog em Da ideia ao projeto: o plano parte da stack aprovada."]);

        var problems = new List<string>();
        var notes = new List<string>();
        var backend = false;
        var frontend = false;
        foreach (var item in documents.Stack)
        {
            switch (Part(item))
            {
                case Backend: backend = true; break;
                case Frontend: frontend = true; break;
                case null when item.StackId is not null || item.Language:
                    problems.Add($"{item.Choice} ({item.Area}) ainda não é criado pelo Spock: esta versão cria API .NET e frontend React com Vite.");
                    break;
                default:
                    notes.Add($"{item.Area}: {item.Choice} não é criado aqui; fica para o projeto seguir a arquitetura.");
                    break;
            }
        }
        if (!backend && !frontend)
            problems.Add("A stack aprovada não tem .NET nem React: esta versão cria API .NET e frontend React com Vite.");
        if (problems.Count > 0) return Unsupported(name, problems);

        var ids = new List<string>();
        if (backend) ids.Add("dotnet");
        if (frontend) ids.AddRange(["node", "react", "vite"]);
        var report = VersionPolicy.Recommend(ids, VersionGoals.Production, catalog, today);
        var technologies = new List<ScaffoldTechnology>();
        foreach (var recommendation in report.Technologies)
        {
            if (recommendation.Choice is not { Version: not null } choice)
            {
                problems.Add($"O catálogo não tem uma versão recomendável de {recommendation.Name}.");
                continue;
            }
            technologies.Add(new(recommendation.TechnologyId, recommendation.Name, choice.Cycle, choice.Version, choice.Channel, choice.Eol,
                choice.Language, choice.Reason, recommendation.Source, recommendation.CheckedAt));
            notes.AddRange(recommendation.Notes.Select(note => $"{recommendation.Name}: {note}"));
        }
        if (problems.Count > 0) return Unsupported(name, problems);
        if (frontend) notes.Add(TypeScriptNote);

        var byId = technologies.ToDictionary(technology => technology.Id);
        var steps = new List<ScaffoldStep>();
        var verification = new List<ScaffoldStep>();
        if (backend) AddBackend(steps, verification, name, byId["dotnet"]);
        if (frontend) AddFrontend(steps, verification, byId["node"], byId["react"], byId["vite"]);
        var record = Record(technologies, notes, catalog.GeneratedAt);
        steps.Add(new("R1", "registro", "Gravar o registro das versões", ".", null, RecordFile, record));
        return new(true, [], notes, name, backend, frontend, technologies, steps, verification, RecordFile, record);
    }

    public static string TargetFramework(string cycle) => "net" + cycle;

    public static string SdkVersion(string cycle) => cycle + SdkFeatureBand;

    public static string ApiProject(string name) => name + ".Api";

    public static string TestProject(string name) => name + ".Api.Tests";

    private static void AddBackend(List<ScaffoldStep> steps, List<ScaffoldStep> verification, string name, ScaffoldTechnology dotnet)
    {
        var framework = TargetFramework(dotnet.Cycle);
        var api = ApiProject(name);
        var tests = TestProject(name);
        steps.Add(new("B1", Backend, "Criar a solução", ".", $"dotnet new sln -n {name} -o {BackendFolder}", null, null));
        steps.Add(new("B2", Backend, "Fixar o SDK", BackendFolder, $"dotnet new globaljson --sdk-version {SdkVersion(dotnet.Cycle)} --roll-forward latestFeature", null, null));
        steps.Add(new("B3", Backend, "Criar a API", BackendFolder, $"dotnet new webapi -n {api} -o src/{api} -f {framework}", null, null));
        steps.Add(new("B4", Backend, "Criar o projeto de testes", BackendFolder, $"dotnet new xunit -n {tests} -o tests/{tests} -f {framework}", null, null));
        steps.Add(new("B5", Backend, "Ligar os testes à API", BackendFolder, $"dotnet add tests/{tests} reference src/{api}", null, null));
        steps.Add(new("B6", Backend, "Pôr os projetos na solução", BackendFolder, $"dotnet sln add src/{api} tests/{tests}", null, null));
        verification.Add(new("V1", Backend, "Compilar o backend", BackendFolder, "dotnet build", null, null));
        verification.Add(new("V2", Backend, "Rodar os testes do backend", BackendFolder, "dotnet test", null, null));
    }

    private static void AddFrontend(List<ScaffoldStep> steps, List<ScaffoldStep> verification, ScaffoldTechnology node, ScaffoldTechnology react, ScaffoldTechnology vite)
    {
        steps.Add(new("F1", Frontend, "Criar o frontend", ".", $"npm create vite@{vite.Cycle} {FrontendFolder} -- --template react-ts", null, null));
        steps.Add(new("F2", Frontend, "Fixar o Node", FrontendFolder, null, ".nvmrc", node.Version + "\n"));
        steps.Add(new("F3", Frontend, "Instalar as dependências", FrontendFolder, "npm install", null, null));
        steps.Add(new("F4", Frontend, "Fixar o React", FrontendFolder, $"npm install --save-exact react@{react.Version} react-dom@{react.Version}", null, null));
        steps.Add(new("F5", Frontend, "Fixar o Vite", FrontendFolder, $"npm install --save-exact --save-dev vite@{vite.Version}", null, null));
        verification.Add(new("V3", Frontend, "Compilar o frontend", FrontendFolder, "npm run build", null, null));
        verification.Add(new("V4", Frontend, "Rodar o lint do frontend", FrontendFolder, "npm run lint", null, null));
    }

    // O registro é do Spock, não do agente: o conteúdo sai pronto e a conferência compara com ele.
    private static string Record(IReadOnlyList<ScaffoldTechnology> technologies, IReadOnlyList<string> notes, DateTimeOffset catalogDate)
    {
        var json = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["generatedBy"] = "Spock Studio",
            ["catalogGeneratedAt"] = Date(catalogDate),
            ["technologies"] = new JsonArray(technologies.Select(technology => (JsonNode)new JsonObject
            {
                ["id"] = technology.Id,
                ["name"] = technology.Name,
                ["cycle"] = technology.Cycle,
                ["version"] = technology.Version,
                ["channel"] = technology.Channel,
                ["eol"] = technology.Eol,
                ["language"] = technology.Language,
                ["reason"] = technology.Reason,
                ["source"] = technology.Source,
                ["checkedAt"] = technology.CheckedAt is { } checkedAt ? Date(checkedAt) : null
            }).ToArray()),
            ["notes"] = new JsonArray(notes.Select(note => (JsonNode)JsonValue.Create(note)!).ToArray())
        };
        return json.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n") + "\n";
    }

    private static string Date(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // Backend .NET e frontend React criam; Node como linguagem do frontend (TypeScript) não é outra parte.
    private static string? Part(PlannedStack item) => item.StackId switch
    {
        "dotnet" => Backend,
        "react" => Frontend,
        "node" when !item.Area.Equals("backend", StringComparison.OrdinalIgnoreCase) && NodeIsLanguage().IsMatch(item.Choice) => Frontend,
        _ => null
    };

    private static ScaffoldPlan Unsupported(string name, IReadOnlyList<string> problems) =>
        new(false, problems, [], name, false, false, [], [], [], RecordFile, null);

    // "loja-nova" vira "LojaNova": o nome da solução e dos projetos .NET.
    public static string ProjectName(string folderName)
    {
        var plain = new string(folderName.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var name = string.Concat(Words().Matches(plain).Select(word => char.ToUpperInvariant(word.Value[0]) + word.Value[1..]));
        if (name.Length == 0) return "App";
        return char.IsDigit(name[0]) ? "App" + name : name;
    }

    [GeneratedRegex(@"[A-Za-z0-9]+")]
    private static partial Regex Words();

    [GeneratedRegex(@"^\s*(typescript|javascript)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NodeIsLanguage();
}
