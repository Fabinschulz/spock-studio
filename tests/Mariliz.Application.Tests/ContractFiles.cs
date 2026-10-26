using System.Text.Json.Nodes;
using Json.Schema;

namespace Mariliz.Application.Tests;

internal static class ContractFiles
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Mariliz.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }

    public static string Path_(params string[] parts) => Path.Combine([Root, "packages", "contracts", .. parts]);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, JsonSchema> Cache = new();

    // A biblioteca registra o $id globalmente e recusa recarregar o mesmo schema.
    public static JsonSchema Schema(params string[] parts)
    {
        var path = Path_(["schemas", .. parts]);
        return Cache.GetOrAdd(path, p => JsonSchema.FromText(File.ReadAllText(p)));
    }

    public static bool IsValid(JsonSchema schema, JsonNode? node) => schema.Evaluate(System.Text.Json.JsonSerializer.SerializeToElement(node)).IsValid;

    public static IEnumerable<object[]> Fixtures(string folder) =>
        Directory.GetFiles(Path_("fixtures", folder), "*.json").Select(f => new object[] { System.IO.Path.GetFileName(f) });

    public static JsonNode? ReadFixture(string folder, string name) => JsonNode.Parse(File.ReadAllText(Path_("fixtures", folder, name)));
}
