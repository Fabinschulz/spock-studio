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

    private const int MaxPatternTitle = 140;

    private static readonly Lazy<IReadOnlyList<MemoryDocument>> All = new(() => Files().SelectMany(file => Split(file.Name, file.Markdown)).ToList());

    private static readonly Lazy<IReadOnlyList<ReferencePattern>> AllPatterns =
        new(() => Files().SelectMany(file => PatternsOf(file.Markdown)).ToList());

    public static IReadOnlyList<MemoryDocument> Documents => All.Value;

    // Cada linha "| REF-… | nome |" das tabelas: o índice dos padrões que a intenção architecture do Context Engine entrega.
    public static IReadOnlyList<ReferencePattern> Patterns => AllPatterns.Value;

    private static IEnumerable<(string Name, string Markdown)> Files()
    {
        var assembly = typeof(OrganizationReferences).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourceFolder, StringComparison.Ordinal) && name.EndsWith(".md", StringComparison.Ordinal))
            .Where(name => !name.EndsWith("/README.md", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(name => (name[ResourceFolder.Length..^".md".Length], Read(assembly, name)));
    }

    internal static IEnumerable<ReferencePattern> PatternsOf(string markdown)
    {
        var frontmatter = FrontmatterReader.Read(markdown);
        if (frontmatter is null || !frontmatter.TryGetValue("titulo", out var title) || !frontmatter.TryGetValue("analisadoEm", out var analyzed)
            || !DateTimeOffset.TryParse(analyzed, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var analyzedAt))
            yield break;

        foreach (var section in Section().Split(markdown.Replace("\r\n", "\n")).Skip(1))
        {
            var heading = Heading().Replace(section[..section.IndexOf('\n')].Trim(), "");
            var technologies = TechnologiesOf(section);
            foreach (Match row in PatternRow().Matches(section))
            {
                var name = row.Groups["title"].Value.Replace("**", "").Trim();
                if (name.Length > MaxPatternTitle) name = name[..(MaxPatternTitle - 1)].TrimEnd() + "…";
                yield return new ReferencePattern(row.Groups["id"].Value, name, heading, technologies, title, analyzedAt);
            }
        }
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
            var technologies = TechnologiesOf(section);
            if (technologies.Count > 0) text = $"---\ntechnologies: {string.Join(", ", technologies)}\n---\n{text}";
            yield return new MemoryDocument($"{Prefix}{file}/{Slug(heading)}.md", Hashing.Sha256(text), text);
        }
    }

    // A linha "Escopo: tecnologias `react`, `nextjs` · tipo ..." da seção diz a que stacks ela vale; o índice filtra por isso.
    // Sem o trecho de tecnologia, ou com "qualquer tecnologia", a seção vale para todos os projetos.
    private static List<string> TechnologiesOf(string section)
    {
        var scope = Scope().Match(section);
        if (!scope.Success) return [];
        var segment = scope.Groups["scope"].Value.Split('·').FirstOrDefault(part => part.TrimStart().StartsWith("tecnologia", StringComparison.Ordinal));
        return segment is null ? [] : Quoted().Matches(segment).Select(match => match.Groups["id"].Value).Distinct().ToList();
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

    [GeneratedRegex(@"(?m)^\| (?<id>REF-[A-Z]+\d+) \| (?<title>[^|]+)\|")]
    private static partial Regex PatternRow();

    [GeneratedRegex(@"(?m)^Escopo:(?<scope>.*)$")]
    private static partial Regex Scope();

    [GeneratedRegex(@"`(?<id>[a-z0-9-]{1,40})`")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"^\d+\.\s*")]
    private static partial Regex Heading();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NotWord();
}
