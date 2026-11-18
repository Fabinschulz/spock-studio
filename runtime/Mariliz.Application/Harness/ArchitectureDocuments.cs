using System.Text.RegularExpressions;
using Mariliz.Application.Discovery;
using Mariliz.Application.Ideas;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Harness;

/// <param name="StackId">A tecnologia do registro (Technologies) nomeada na linha; nulo quando não é nenhuma delas.</param>
/// <param name="Language">Reconhecida como linguagem ou framework, com ou sem tecnologia no registro.</param>
internal sealed record PlannedStack(string Area, string Choice, string? StackId, bool Language, string Line);

// Os documentos que a fase A grava. O motor lê o Markdown, não uma cópia em JSON: se a pessoa editar a stack no documento e
// planejar de novo, o plano acompanha.
internal sealed partial class ArchitectureDocuments
{
    // Linguagens que o Spock reconhece numa arquitetura, mas que não são tecnologia do registro (não há detector para elas).
    private static readonly Regex OtherLanguages = new(
        @"\bphp\b|\blaravel\b|\bruby\b|\brails\b|\bswift\b|\belixir\b|\bscala\b", RegexOptions.IgnoreCase);

    private ArchitectureDocuments(IReadOnlyList<string> present, IReadOnlyList<PlannedStack> stack, string architectureFile)
    {
        ArchitectureFile = architectureFile;
        Present = present;
        Stack = stack;
    }

    public string ArchitectureFile { get; }
    public IReadOnlyList<string> Present { get; }
    public IReadOnlyList<PlannedStack> Stack { get; }
    public bool Exists => Present.Count > 0;

    public static ArchitectureDocuments Read(IProjectFiles files)
    {
        var naming = files.Naming();
        var architectureFile = IdeaDocuments.ArchitectureFile(naming);
        var present = IdeaDocuments.Files(naming).Where(files.FileExists).ToList();
        var text = present.Contains(architectureFile) ? files.ReadText(architectureFile) : null;
        return new(present, text is null ? [] : ParseStack(text), architectureFile);
    }

    // Só o que a descoberta não achou: o que está no código sempre vale mais que o planejado.
    public List<StackFinding> PlannedOnly(IReadOnlyList<StackFinding> detected, Func<string, bool> family)
    {
        if (detected.Any(stack => family(stack.Id))) return [];
        return Stack.Where(item => item.StackId is not null && family(item.StackId))
            .Select(item => new StackFinding(item.StackId!, item.Choice, "", null, Confidence.Low,
                [new Evidence(ArchitectureFile, item.Line)], ["Planejada na arquitetura aprovada, ainda sem arquivos no projeto."]))
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

            var technology = Technologies.Named(choice);
            stack.Add(new(area, choice, technology?.Id, technology is not null || OtherLanguages.IsMatch(choice), line));
        }
        return stack;
    }

    [GeneratedRegex(@"^- \*\*(?<area>[^*]+)\*\*:\s*(?<rest>.+)$")]
    private static partial Regex Bullet();
}
