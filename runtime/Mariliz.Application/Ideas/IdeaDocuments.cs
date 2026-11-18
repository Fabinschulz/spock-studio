using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Core.Policy;
using static Mariliz.Application.Ideas.IdeaChecks;

namespace Mariliz.Application.Ideas;

// Documentos para gente e para agentes lerem: cada item mantém o id, então histórias e componentes apontam para os requisitos.
internal static class IdeaDocuments
{
    public const string SpecificationName = "especificacao.md";
    public const string ArchitectureName = "arquitetura.md";
    public const string BacklogName = "backlog.md";

    // Na pasta que o projeto usa (.spock/arquitetura, ou .mariliz/arquitetura num projeto configurado antes da troca de nome).
    public static string SpecificationFile(HarnessNaming naming) => naming.ArchitectureFolder + "/" + SpecificationName;
    public static string ArchitectureFile(HarnessNaming naming) => naming.ArchitectureFolder + "/" + ArchitectureName;
    public static string BacklogFile(HarnessNaming naming) => naming.ArchitectureFolder + "/" + BacklogName;
    public static IReadOnlyList<string> Files(HarnessNaming naming) => [SpecificationFile(naming), ArchitectureFile(naming), BacklogFile(naming)];

    // Fim de linha fixo: o mesmo documento dá o mesmo hash, e o mesmo plano, em qualquer sistema.
    public static IReadOnlyList<(string Path, string Content)> Render(IdeaArtifacts artifacts, HarnessNaming naming) =>
    [
        (SpecificationFile(naming), Specification(artifacts.Idea, artifacts.Specification!).ReplaceLineEndings("\n")),
        (ArchitectureFile(naming), Architecture(artifacts.Architecture!).ReplaceLineEndings("\n")),
        (BacklogFile(naming), Backlog(artifacts.Backlog!).ReplaceLineEndings("\n"))
    ];

    private static string Specification(string idea, JsonObject spec)
    {
        var text = new StringBuilder();
        text.Append("# ").AppendLine(Text(spec, "title")).AppendLine();
        text.AppendLine("Especificação gerada a partir de uma ideia no Spock Studio e aprovada etapa por etapa.").AppendLine();
        text.AppendLine("## Ideia").AppendLine().AppendLine(idea.Trim()).AppendLine();
        text.AppendLine("## Problema").AppendLine().AppendLine(Text(spec, "problem")).AppendLine();
        Bullets(text, "Usuários", Strings(spec, "users"));
        Bullets(text, "Objetivos", Strings(spec, "goals"));
        Bullets(text, "Requisitos", Objects(spec, "requirements").Select(item => $"**{Text(item, "id")}** ({Text(item, "priority")}): {Text(item, "text")}"));
        Bullets(text, "Requisitos não funcionais", Objects(spec, "nonFunctional").Select(item => $"**{Text(item, "id")}**: {Text(item, "text")}"));
        Bullets(text, "Fora do escopo", Strings(spec, "outOfScope"));
        Bullets(text, "Perguntas em aberto", Strings(spec, "openQuestions"));
        return Lf(text);
    }

    private static string Architecture(JsonObject architecture)
    {
        var text = new StringBuilder();
        text.AppendLine("# Arquitetura").AppendLine();
        text.Append("**Estilo:** ").AppendLine(Text(architecture, "style")).AppendLine();
        text.AppendLine(Text(architecture, "summary")).AppendLine();
        Bullets(text, "Componentes", Objects(architecture, "components").Select(item =>
            $"**{Text(item, "id")} {Text(item, "name")}**: {Text(item, "responsibility")} Atende a {string.Join(", ", Strings(item, "requirements"))}."));
        Bullets(text, "Stack", Objects(architecture, "stack").Select(item => $"**{Text(item, "area")}**: {Text(item, "choice")}. {Text(item, "reason")}"));
        Bullets(text, "Dados", Strings(architecture, "data"));
        Bullets(text, "Integrações", Strings(architecture, "integrations"));
        Bullets(text, "Riscos", Objects(architecture, "risks").Select(item => $"{Text(item, "text")} Mitigação: {Text(item, "mitigation")}"));
        Bullets(text, "Decisões", Objects(architecture, "decisions").Select(item => $"**{Text(item, "title")}**: {Text(item, "choice")}. {Text(item, "reason")}"));
        return Lf(text);
    }

    private static string Backlog(JsonObject backlog)
    {
        var text = new StringBuilder();
        text.AppendLine("# Backlog").AppendLine();
        foreach (var epic in Objects(backlog, "epics"))
        {
            text.Append("## ").Append(Text(epic, "id")).Append(' ').AppendLine(Text(epic, "title")).AppendLine();
            foreach (var story in Objects(epic, "stories"))
            {
                text.Append("### ").Append(Text(story, "id")).Append(' ').AppendLine(Text(story, "title")).AppendLine();
                text.Append("Requisitos: ").Append(string.Join(", ", Strings(story, "requirements")))
                    .Append(". Estimativa: ").Append(Text(story, "estimate")).AppendLine(".").AppendLine();
                foreach (var criterion in Strings(story, "acceptance")) text.Append("- [ ] ").AppendLine(criterion);
                text.AppendLine();
            }
        }
        return Lf(text).TrimEnd() + "\n";
    }

    private static void Bullets(StringBuilder text, string title, IEnumerable<string> items)
    {
        var list = items.ToList();
        if (list.Count == 0) return;
        text.Append("## ").AppendLine(title).AppendLine();
        foreach (var item in list) text.Append("- ").AppendLine(item);
        text.AppendLine();
    }

    // AppendLine usa \r\n no Windows: o documento gravado e o hash dele seriam outros em cada sistema.
    private static string Lf(StringBuilder text) => text.ToString().Replace("\r\n", "\n");
}
