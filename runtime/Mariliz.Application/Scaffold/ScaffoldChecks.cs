using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Scaffold;

public sealed record ScaffoldCheck(string Id, string Part, string Label, bool Passed, string? Detail);

// O que o agente criou, conferido contra o plano só pelos arquivos: SDK, alvo e referências do .NET, Node e versões exatas do
// frontend e o registro. Build e testes ficam com o agente, que roda os comandos de verificação do plano.
public static class ScaffoldChecks
{
    public static IReadOnlyList<ScaffoldCheck> Check(IProjectFiles files, ScaffoldPlan plan)
    {
        if (!plan.Supported) return [];
        var byId = plan.Technologies.ToDictionary(technology => technology.Id);
        var checks = new List<ScaffoldCheck>();
        if (plan.Backend) checks.AddRange(Backend(files, plan.ProjectName, byId["dotnet"]));
        if (plan.Frontend) checks.AddRange(Frontend(files, byId["node"], byId["react"], byId["vite"]));
        checks.Add(RecordCheck(files, plan));
        return checks;
    }

    private static IEnumerable<ScaffoldCheck> Backend(IProjectFiles files, string name, ScaffoldTechnology dotnet)
    {
        var framework = ScaffoldPlans.TargetFramework(dotnet.Cycle);
        var sdk = ScaffoldPlans.SdkVersion(dotnet.Cycle);
        var api = $"{ScaffoldPlans.BackendFolder}/src/{ScaffoldPlans.ApiProject(name)}/{ScaffoldPlans.ApiProject(name)}.csproj";
        var tests = $"{ScaffoldPlans.BackendFolder}/tests/{ScaffoldPlans.TestProject(name)}/{ScaffoldPlans.TestProject(name)}.csproj";

        var global = Json(files.ReadText($"{ScaffoldPlans.BackendFolder}/global.json"));
        var sdkVersion = global?["sdk"]?["version"]?.GetValue<string>();
        var rollForward = global?["sdk"]?["rollForward"]?.GetValue<string>();
        yield return Check("sdk", ScaffoldPlans.Backend, $"backend/global.json fixa o SDK {sdk} com rollForward latestFeature",
            sdkVersion == sdk && rollForward == "latestFeature", global is null ? "arquivo ausente" : $"{sdkVersion} {rollForward}");

        var apiProject = Project(files.ReadText(api));
        yield return Check("api", ScaffoldPlans.Backend, $"A API existe em {api}, com o SDK Web e alvo {framework}",
            apiProject is not null && apiProject.Sdk == "Microsoft.NET.Sdk.Web" && apiProject.Framework == framework,
            apiProject is null ? "projeto ausente" : $"{apiProject.Sdk} {apiProject.Framework}");

        var testProject = Project(files.ReadText(tests));
        var referencesApi = testProject?.References.Any(reference => reference.EndsWith(ScaffoldPlans.ApiProject(name) + ".csproj", StringComparison.OrdinalIgnoreCase)) == true;
        yield return Check("tests", ScaffoldPlans.Backend, $"O projeto de testes existe em {tests}, com alvo {framework} e referência à API",
            testProject is not null && testProject.Framework == framework && referencesApi,
            testProject is null ? "projeto ausente" : $"{testProject.Framework}{(referencesApi ? "" : ", sem referência à API")}");

        var solution = Solution(files);
        var listed = solution is not null
                     && solution.Contains(ScaffoldPlans.ApiProject(name) + ".csproj", StringComparison.OrdinalIgnoreCase)
                     && solution.Contains(ScaffoldPlans.TestProject(name) + ".csproj", StringComparison.OrdinalIgnoreCase);
        yield return Check("solution", ScaffoldPlans.Backend, $"A solução {name} em backend/ lista a API e os testes", listed,
            solution is null ? "solução ausente" : null);
    }

    private static IEnumerable<ScaffoldCheck> Frontend(IProjectFiles files, ScaffoldTechnology node, ScaffoldTechnology react, ScaffoldTechnology vite)
    {
        var nvmrc = files.ReadText($"{ScaffoldPlans.FrontendFolder}/.nvmrc")?.Trim();
        yield return Check("node", ScaffoldPlans.Frontend, $"frontend/.nvmrc fixa o Node {node.Version}", nvmrc == node.Version, nvmrc ?? "arquivo ausente");

        var package = Json(files.ReadText($"{ScaffoldPlans.FrontendFolder}/package.json"));
        var dependencies = package?["dependencies"] as JsonObject;
        var devDependencies = package?["devDependencies"] as JsonObject;
        var reactVersion = Version(dependencies, "react");
        var domVersion = Version(dependencies, "react-dom");
        yield return Check("react", ScaffoldPlans.Frontend, $"frontend/package.json fixa react e react-dom em {react.Version}, sem faixa",
            reactVersion == react.Version && domVersion == react.Version,
            package is null ? "package.json ausente" : $"react {reactVersion ?? "ausente"}, react-dom {domVersion ?? "ausente"}");

        var viteVersion = Version(devDependencies, "vite");
        yield return Check("vite", ScaffoldPlans.Frontend, $"frontend/package.json fixa o vite em {vite.Version}, sem faixa",
            viteVersion == vite.Version, package is null ? "package.json ausente" : $"vite {viteVersion ?? "ausente"}");

        var scripts = package?["scripts"] as JsonObject;
        var hasScripts = scripts?["build"] is not null && scripts["lint"] is not null;
        yield return Check("scripts", ScaffoldPlans.Frontend, "frontend/package.json tem os scripts build e lint do template", hasScripts, null);

        var locked = files.FileExists($"{ScaffoldPlans.FrontendFolder}/package-lock.json");
        yield return Check("lockfile", ScaffoldPlans.Frontend, "frontend/package-lock.json existe: as dependências foram instaladas", locked, null);
    }

    // Compara o conteúdo, não o texto: quebra de linha ou indentação do editor do agente não contam.
    private static ScaffoldCheck RecordCheck(IProjectFiles files, ScaffoldPlan plan)
    {
        var written = Json(files.ReadText(plan.RecordPath));
        var expected = Json(plan.Record);
        return Check("record", "registro", $"{plan.RecordPath} tem as versões, os motivos, as fontes e as datas do plano",
            written is not null && expected is not null && JsonNode.DeepEquals(written, expected),
            written is null ? "arquivo ausente ou inválido" : null);
    }

    private static string? Version(JsonObject? dependencies, string name) =>
        dependencies?[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string? Solution(IProjectFiles files)
    {
        if (!files.DirectoryExists(ScaffoldPlans.BackendFolder)) return null;
        var top = files.EnumerateFiles(ScaffoldPlans.BackendFolder, maxDepth: 1)
            .Where(path => !path[(ScaffoldPlans.BackendFolder.Length + 1)..].Contains('/')).ToList();
        foreach (var extension in new[] { ".slnx", ".sln" })
        {
            var match = top.FirstOrDefault(path => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return files.ReadText(match);
        }
        return null;
    }

    private sealed record ProjectInfo(string Sdk, string? Framework, IReadOnlyList<string> References);

    private static ProjectInfo? Project(string? text)
    {
        if (text is null) return null;
        try
        {
            var root = XDocument.Parse(text).Root;
            if (root is null) return null;
            var framework = root.Descendants().FirstOrDefault(element => element.Name.LocalName == "TargetFramework")?.Value.Trim();
            var references = root.Descendants().Where(element => element.Name.LocalName == "ProjectReference")
                .Select(element => (element.Attribute("Include")?.Value ?? "").Replace('\\', '/')).ToList();
            return new(root.Attribute("Sdk")?.Value ?? "", framework, references);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static JsonNode? Json(string? text)
    {
        if (text is null) return null;
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ScaffoldCheck Check(string id, string part, string label, bool passed, string? detail) =>
        new(id, part, label, passed, passed ? null : detail);
}
