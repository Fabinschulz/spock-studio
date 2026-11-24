using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mariliz.Application.Tests.Support;

// As fixtures em contracts/fixtures/golden são o que o motor responde de verdade; o Zod da interface é conferido contra
// elas. A comparação é de formato (chaves e tipos), não de valores. Para regenerar depois de uma mudança deliberada:
// MARILIZ_UPDATE_GOLDEN=1 dotnet test.
internal static class GoldenFixtures
{
    private static readonly bool Update = Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1";

    private static readonly JsonSerializerOptions Indented =
        new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string PathOf(string name) => ContractFiles.Path_("fixtures", "golden", name + ".json");

    // Remove o que muda a cada máquina ou execução (ids, nome da pasta temporária).
    public static JsonNode Normalize(JsonNode node, string projectId, string projectFolder)
    {
        var text = node.ToJsonString(Indented).Replace(projectId, "p-000000000000").Replace(projectFolder, "projeto");
        text = System.Text.RegularExpressions.Regex.Replace(text, "run-[0-9a-f]{12}", "run-000000000000");
        text = System.Text.RegularExpressions.Regex.Replace(text, "plan-[0-9a-f]{16}", "plan-0000000000000000");
        return JsonNode.Parse(text)!;
    }

    public static void Check(string name, JsonNode json)
    {
        var path = PathOf(name);
        if (Update || !File.Exists(path))
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json.ToJsonString(Indented).ReplaceLineEndings("\n") + "\n");
            return;
        }

        var committed = JsonNode.Parse(File.ReadAllText(path))!;
        var drift = Shape.Diff(Shape.Of(committed), Shape.Of(json));
        Assert.True(drift.Count == 0,
            $"{name}: o formato mudou ({string.Join("; ", drift.Take(8))}). Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }

    // Chaves e tipos, ignorando valores, para tolerar dados que variam.
    private static class Shape
    {
        public static SortedDictionary<string, SortedSet<string>> Of(JsonNode? node)
        {
            var map = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            Walk(node, "$", map);
            return map;
        }

        private static void Walk(JsonNode? node, string path, SortedDictionary<string, SortedSet<string>> map)
        {
            void Add(string type) { if (!map.TryGetValue(path, out var set)) map[path] = set = []; set.Add(type); }
            Add(node switch
            {
                null => "null",
                JsonObject => "object",
                JsonArray => "array",
                JsonValue v => v.GetValueKind() switch
                {
                    JsonValueKind.String => "string",
                    JsonValueKind.Number => "number",
                    JsonValueKind.True or JsonValueKind.False => "boolean",
                    _ => "other"
                },
                _ => "other"
            });

            if (node is JsonObject o)
                foreach (var (key, value) in o) Walk(value, path + "." + key, map);
            else if (node is JsonArray a)
                foreach (var item in a) Walk(item, path + "[]", map);
        }

        public static List<string> Diff(SortedDictionary<string, SortedSet<string>> expected, SortedDictionary<string, SortedSet<string>> actual)
        {
            var problems = new List<string>();
            foreach (var (path, types) in expected)
            {
                if (!actual.TryGetValue(path, out var now)) { if (!path.Contains("[]")) problems.Add($"falta {path}"); continue; }
                var missing = types.Except(now).Where(t => t != "null").ToList();
                if (missing.Count > 0 && !now.IsSupersetOf(types.Where(t => t != "null"))) problems.Add($"{path}: tipo {string.Join("/", now)} em vez de {string.Join("/", types)}");
            }
            foreach (var path in actual.Keys.Except(expected.Keys)) problems.Add($"campo novo {path}");
            return problems;
        }
    }
}
