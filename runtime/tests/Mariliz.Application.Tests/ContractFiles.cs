using System.Text.Json.Nodes;
using Json.Schema;

namespace Mariliz.Application.Tests;

internal static class ContractFiles
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "contracts", "schemas", "protocol", "envelope.v1.schema.json")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }

    public static string Path_(params string[] parts) => Path.Combine([Root, "contracts", .. parts]);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, JsonSchema> Cache = new();

    // A biblioteca registra o $id globalmente e recusa recarregar o mesmo schema.
    public static JsonSchema Schema(params string[] parts)
    {
        var path = Path_(["schemas", .. parts]);
        lock (EvaluationLock) return Cache.GetOrAdd(path, p => JsonSchema.FromText(File.ReadAllText(p)));
    }

    // A avaliação do mesmo JsonSchema em paralelo dava resultado errado de forma intermitente
    // (fixture inválida aceita). Serializar é barato e torna o teste de contrato determinístico.
    private static readonly object EvaluationLock = new();

    public static JsonSchema SchemaFromPath(string absolutePath)
    {
        lock (EvaluationLock) return Cache.GetOrAdd(absolutePath, p => JsonSchema.FromText(File.ReadAllText(p)));
    }

    public static bool IsValid(JsonSchema schema, JsonNode? node)
    {
        var element = System.Text.Json.JsonSerializer.SerializeToElement(node);
        lock (EvaluationLock) return schema.Evaluate(element).IsValid;
    }

    public static IEnumerable<object[]> Fixtures(string folder) =>
        Directory.GetFiles(Path_("fixtures", folder), "*.json").Select(f => new object[] { System.IO.Path.GetFileName(f) });

    public static JsonNode? ReadFixture(string folder, string name) => JsonNode.Parse(File.ReadAllText(Path_("fixtures", folder, name)));
}
