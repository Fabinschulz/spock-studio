using System.Reflection;
using System.Text.RegularExpressions;
using Mariliz.Application.Discovery;
using Mariliz.Application.Management;
using Mariliz.Core.Text;

namespace Mariliz.Application.Workflows;

// A pasta Organização (docs/organizacao) vai embutida no motor e entra no índice de cada projeto, uma seção "## " por
// documento: o índice corta arquivos grandes, e a busca acha a seção da tecnologia em vez do catálogo inteiro. O prefixo é
// só do motor (KnowledgeSources bloqueia um arquivo do projeto com esse nome).
public static partial class OrganizationReferences
{
    public const string Prefix = "@reference/";
    private const string ResourceFolder = "Organizacao/";
    private const string Notice = "Referência arquitetural da Organização: argumento de engenharia, não é regra nem convenção do projeto.";

    private static readonly Lazy<IReadOnlyList<MemoryDocument>> All = new(Load);

    public static IReadOnlyList<MemoryDocument> Documents => All.Value;

    private static IReadOnlyList<MemoryDocument> Load()
    {
        var assembly = typeof(OrganizationReferences).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourceFolder, StringComparison.Ordinal) && name.EndsWith(".md", StringComparison.Ordinal))
            .Where(name => !name.EndsWith("/README.md", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .SelectMany(name => Split(name[ResourceFolder.Length..^".md".Length], Read(assembly, name)))
            .ToList();
    }

    // Sem titulo e analisadoEm no frontmatter, o arquivo não entra: o agente precisa saber de quando é a referência.
    internal static IEnumerable<MemoryDocument> Split(string fileName, string markdown)
    {
        var frontmatter = FrontmatterReader.Read(markdown);
        if (frontmatter is null || !frontmatter.TryGetValue("titulo", out var title) || !frontmatter.TryGetValue("analisadoEm", out var analyzed))
            yield break;

        var file = Slug(fileName);
        foreach (var section in Section().Split(markdown.Replace("\r\n", "\n")).Skip(1))
        {
            var heading = section[..section.IndexOf('\n')].Trim();
            // A fonte e a data vão no título principal: o índice repete o caminho de títulos em cada trecho de subseção.
            var text = $"# Referência da Organização · {Heading().Replace(heading, "")} · {title}, análise de {analyzed}\n" +
                       $"{Notice}\n\n## {section.TrimEnd()}\n";
            yield return new MemoryDocument($"{Prefix}{file}/{Slug(heading)}.md", Hashing.Sha256(text), text);
        }
    }

    private static string Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // "3. Frontend React" vira "frontend-react": sem número, para o caminho não mudar quando uma seção for renumerada.
    private static string Slug(string text)
    {
        var plain = new string(text.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        return NotWord().Replace(Heading().Replace(plain, "").ToLowerInvariant(), "-").Trim('-');
    }

    [GeneratedRegex(@"(?m)^## ")]
    private static partial Regex Section();

    [GeneratedRegex(@"^\d+\.\s*")]
    private static partial Regex Heading();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NotWord();
}
