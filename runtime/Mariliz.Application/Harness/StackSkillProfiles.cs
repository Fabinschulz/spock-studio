using System.Globalization;
using System.Text;
using Mariliz.Application.Workflows;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Harness;

/// <param name="Stack">O nome curto na pasta da skill (<c>spock-stack-react</c>) e no ativo do checklist.</param>
/// <param name="Technologies">As stacks que pedem a skill, as mesmas da regra de padrões do framework.</param>
public sealed record StackSkillProfile(string Component, string Stack, string Name, IReadOnlyList<string> Technologies, string AntiPatternTechnology);

// W3-2: uma skill curta por stack, com o "não copiar" do catálogo como anti-padrões e um checklist de conclusão. Curta de
// propósito: o padrão inteiro está a uma chamada da ferramenta MCP, e uma skill enorme come o contexto de toda sessão.
public static class StackSkillProfiles
{
    // Uma skill além disso já não é "curta": o EV-SKILL-002 reprova, e a geração daqui fica bem abaixo.
    public const int MaxCharacters = 8_000;

    public static readonly StackSkillProfile React = new(Catalog.SkillStackReact, "react", "React", ["react", "nextjs"], "react");
    public static readonly StackSkillProfile Angular = new(Catalog.SkillStackAngular, "angular", "Angular", ["angular"], "angular");
    public static readonly StackSkillProfile Dotnet = new(Catalog.SkillStackDotnet, "dotnet", ".NET", ["dotnet"], "dotnet");

    public static readonly IReadOnlyList<StackSkillProfile> All = [React, Angular, Dotnet];

    public static string Render(HarnessNaming naming, StackSkillProfile profile)
    {
        var antiPatterns = OrganizationReferences.AntiPatterns.Where(item => item.Technologies.Contains(profile.AntiPatternTechnology)).ToList();
        var sb = new StringBuilder();
        sb.Append("---\nname: ").Append(naming.StackSkillName(profile.Stack)).Append('\n');
        sb.Append("description: What not to copy and what to check before finishing ").Append(profile.Name)
          .Append(" work in this project. Use it when writing or reviewing ").Append(profile.Name).Append(" code.\n---\n\n");
        sb.Append("# ").Append(profile.Name).Append(": anti-patterns and finishing checklist\n\n");
        sb.Append("Managed by Spock Studio. Short on purpose. This project's own conventions, when present, are in `").Append(naming.RulePrefix)
          .Append("project-conventions.md` and come first; the Organization patterns are in `").Append(naming.PatternRule(profile.Stack))
          .Append("`; a full pattern is one `spock_architecture_reference` call away.\n");

        if (antiPatterns.Count > 0)
        {
            sb.Append("\n## Do not copy\n\n");
            sb.Append("Seen in reference projects (").Append(antiPatterns[0].Source).Append(", analysis of ")
              .Append(antiPatterns[0].AnalyzedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("):\n\n");
            foreach (var item in antiPatterns)
                sb.Append("- ").Append(item.Problem.TrimEnd('.')).Append(item.Remedy is null ? "." : $". Instead: {item.Remedy.TrimEnd('.')}.").Append('\n');
        }

        sb.Append("\n## Before finishing\n\n");
        sb.Append(AssetLoader.Read($"skills/stacks/{profile.Stack}.md").Replace("\r\n", "\n").TrimEnd()).Append('\n');
        return sb.ToString();
    }
}
