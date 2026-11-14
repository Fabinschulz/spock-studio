using System.Text.RegularExpressions;
using Mariliz.Application.Ideas;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Harness;

/// <param name="StackId">Uma stack com perfil de regras (dotnet, node, react, nextjs, angular, flutter); nulo quando não há.</param>
/// <param name="Language">Reconhecida como linguagem ou framework, com ou sem perfil de regras.</param>
internal sealed record PlannedStack(string Area, string Choice, string? StackId, bool Language, string Line);

// Os documentos que a fase A grava. O motor lê o Markdown, não uma cópia em JSON: se a pessoa editar a stack no documento e
// planejar de novo, o plano acompanha.
internal sealed partial class ArchitectureDocuments
{
    private static readonly string[] Files = [IdeaDocuments.SpecificationFile, IdeaDocuments.ArchitectureFile, IdeaDocuments.BacklogFile];

    // A ordem importa: a primeira que casar vence ("Next.js com React" é Next.js).
    private static readonly (string Id, Regex Pattern)[] Profiles =
    [
        ("nextjs", new(@"\bnext\.?js\b", RegexOptions.IgnoreCase)),
        ("angular", new(@"\bangular\b", RegexOptions.IgnoreCase)),
        ("react", new(@"\breact\b", RegexOptions.IgnoreCase)),
        ("dotnet", new(@"\.net\b|\basp\.net|\bc#|\bdotnet\b|\bblazor\b|\bentity framework\b", RegexOptions.IgnoreCase)),
        ("node", new(@"\bnode(\.?js)?\b|\bnestjs\b|\bexpress\b|\btypescript\b|\bjavascript\b|\bvue(\.js)?\b|\bnuxt\b|\bsvelte\b", RegexOptions.IgnoreCase)),
        ("flutter", new(@"\bflutter\b|\bdart\b", RegexOptions.IgnoreCase))
    ];

    private static readonly Regex WithoutProfile = new(
        @"\bpython\b|\bdjango\b|\bfastapi\b|\bflask\b|\bjava\b|\bspring\b|\bkotlin\b|\bgolang\b|\bgo\b|\brust\b|\bphp\b|\blaravel\b|\bruby\b|\brails\b|\bswift\b|\belixir\b|\bscala\b",
        RegexOptions.IgnoreCase);

    private ArchitectureDocuments(IReadOnlyList<string> present, IReadOnlyList<PlannedStack> stack)
    {
        Present = present;
        Stack = stack;
    }

    public IReadOnlyList<string> Present { get; }
    public IReadOnlyList<PlannedStack> Stack { get; }
    public bool Exists => Present.Count > 0;

    public static ArchitectureDocuments Read(IProjectFiles files)
    {
        var present = Files.Where(files.FileExists).ToList();
        var text = present.Contains(IdeaDocuments.ArchitectureFile) ? files.ReadText(IdeaDocuments.ArchitectureFile) : null;
        return new(present, text is null ? [] : ParseStack(text));
    }

    // Só o que a descoberta não achou: o que está no código sempre vale mais que o planejado.
    public List<StackFinding> PlannedOnly(IReadOnlyList<StackFinding> detected, Func<string, bool> family)
    {
        if (detected.Any(stack => family(stack.Id))) return [];
        return Stack.Where(item => item.StackId is not null && family(item.StackId))
            .Select(item => new StackFinding(item.StackId!, item.Choice, "", null, Confidence.Low,
                [new Evidence(IdeaDocuments.ArchitectureFile, item.Line)], ["Planejada na arquitetura aprovada, ainda sem arquivos no projeto."]))
            .ToList();
    }

    internal static List<PlannedStack> ParseStack(string markdown)
    {
        var stack = new List<PlannedStack>();
        var inSection = false;
        foreach (var raw in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSection = string.Equals(line[3..].Trim(), "Stack", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inSection || !line.StartsWith("- ", StringComparison.Ordinal)) continue;

            var bullet = Bullet().Match(line);
            var area = bullet.Success ? bullet.Groups["area"].Value.Trim() : "";
            var rest = bullet.Success ? bullet.Groups["rest"].Value : line[2..];
            // O documento escreve "escolha. motivo": o ponto seguido de espaço separa os dois sem cortar "ASP.NET" ou "Node.js".
            var end = rest.IndexOf(". ", StringComparison.Ordinal);
            var choice = (end < 0 ? rest : rest[..end]).Trim().TrimEnd('.');
            if (choice.Length == 0) continue;

            var profile = Profiles.FirstOrDefault(item => item.Pattern.IsMatch(choice)).Id;
            stack.Add(new(area, choice, profile, profile is not null || WithoutProfile.IsMatch(choice), line));
        }
        return stack;
    }

    [GeneratedRegex(@"^- \*\*(?<area>[^*]+)\*\*:\s*(?<rest>.+)$")]
    private static partial Regex Bullet();
}
