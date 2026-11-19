using Mariliz.Application.Management;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Text;

namespace Mariliz.Application.Analysis;

// O relatório entra no índice do projeto como documentos @analysis/, um por assunto, para a busca achar a parte certa. Sem data
// no texto: o hash só muda quando o código muda, e o índice não refaz tudo a cada execução. O prefixo é só do motor
// (KnowledgeSources bloqueia um arquivo do projeto com esse nome).
public static class ArchitectureAnalysisDocuments
{
    public const string Prefix = "@analysis/";
    public const string Notice = "Análise determinística do código atual deste projeto: descreve o que existe, com evidência; não é regra nem recomendação.";

    public static IReadOnlyList<MemoryDocument> For(IProjectFiles files) =>
        Documents(ArchitectureAnalyzer.Analyze("", files, DateTimeOffset.UnixEpoch));

    public static IReadOnlyList<MemoryDocument> Documents(ArchitectureReport report) =>
        Sections(report)
            .Select(section => (section.Slug, Text: $"# Análise de arquitetura · {section.Title}\n{Notice}\n\n{section.Body.TrimEnd()}\n"))
            .Select(section => new MemoryDocument($"{Prefix}{section.Slug}.md", Hashing.Sha256(section.Text), section.Text))
            .ToList();

    public static string Render(ArchitectureReport report) => string.Join("\n", Documents(report).Select(document => document.Text));

    private static IEnumerable<(string Slug, string Title, string Body)> Sections(ArchitectureReport report)
    {
        var warnings = report.Findings.Where(finding => finding.Severity == ArchitectureSeverities.Warning).ToList();
        var info = report.Findings.Where(finding => finding.Severity != ArchitectureSeverities.Warning).ToList();
        if (report.DotnetProjects.Count == 0 && report.Frontends.Count == 0 && report.Findings.Count == 0) yield break;

        yield return ("visao-geral", "Visão geral", Overview(report, warnings));
        if (report.DotnetProjects.Count > 0) yield return ("projetos-dotnet", "Projetos .NET", Projects(report.DotnetProjects));
        if (report.Frontends.Count > 0) yield return ("frontend", "Frontend", Frontends(report.Frontends));
        if (info.Count > 0) yield return ("tamanho-e-testes", "Tamanho e testes", List(info));
    }

    private static string Overview(ArchitectureReport report, List<ArchitectureFinding> warnings)
    {
        var lines = new List<string>();
        if (report.DotnetProjects.Count > 0)
            lines.Add($"Projetos .NET: {report.DotnetProjects.Count} ({string.Join(", ", report.DotnetProjects.GroupBy(node => node.Layer)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Count()} {DotnetArchitecture.Label(group.Key)}"))}).");
        foreach (var module in report.Frontends)
            lines.Add($"Frontend em {module.Source}: pastas {Join(module.Folders)}; {module.Features.Count} feature(s).");
        lines.Add("");
        lines.Add(warnings.Count == 0
            ? "Nenhuma violação de direção de dependência encontrada."
            : $"Violações encontradas ({warnings.Count}):");
        if (warnings.Count > 0) lines.Add(List(warnings));
        return string.Join('\n', lines);
    }

    private static string Projects(IReadOnlyList<DotnetProjectNode> projects)
    {
        var names = projects.ToDictionary(node => node.Path, node => node.Name, StringComparer.Ordinal);
        var lines = new List<string>
        {
            "| Projeto | Camada | Depende de | Frameworks | Teste |",
            "| --- | --- | --- | --- | --- |"
        };
        lines.AddRange(projects.Select(node =>
            $"| {node.Name} | {DotnetArchitecture.Label(node.Layer)} | {Join(node.References.Select(path => names[path]).ToList())} | " +
            $"{Join(node.Frameworks)} | {(node.Layer == ArchitectureLayers.Tests ? "é teste" : node.Tested ? "sim" : "não")} |"));
        return string.Join('\n', lines);
    }

    private static string Frontends(IReadOnlyList<FrontendModule> modules) =>
        string.Join("\n\n", modules.Select(module =>
            $"Código em {module.Source}.\nPastas de primeiro nível: {Join(module.Folders)}.\nFeatures: {Join(module.Features)}.\n" +
            $"Regra de lint entre camadas: {(module.LayerLint ? "sim" : "não")}."));

    private static string List(IEnumerable<ArchitectureFinding> findings) =>
        string.Join('\n', findings.Select(finding => $"- {finding.Title} [{finding.Code}]{Evidence(finding)}"));

    private static string Evidence(ArchitectureFinding finding)
    {
        // O arquivo grande já está no título.
        var evidence = finding.Evidence.Where(item => item.Detail is not null || !finding.Title.Contains(item.Path, StringComparison.Ordinal)).ToList();
        return evidence.Count == 0 ? "" : $": {string.Join("; ", evidence.Select(item => item.Detail is null ? item.Path : $"{item.Path} ({item.Detail})"))}";
    }

    private static string Join(IReadOnlyCollection<string> items) => items.Count == 0 ? "nenhum" : string.Join(", ", items);
}
