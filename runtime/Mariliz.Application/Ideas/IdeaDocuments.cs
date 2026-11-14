using System.Text;
using System.Text.Json.Nodes;
using static Mariliz.Application.Ideas.IdeaChecks;

namespace Mariliz.Application.Ideas;

// Documentos para gente e para agentes lerem: cada item mantém o id, então histórias e componentes apontam para os requisitos.
internal static class IdeaDocuments
{
    public const string Folder = ".mariliz/arquitetura";
    public const string SpecificationFile = Folder + "/especificacao.md";
    public const string ArchitectureFile = Folder + "/arquitetura.md";
    public const string BacklogFile = Folder + "/backlog.md";

    // Fim de linha fixo: o mesmo documento dá o mesmo hash, e o mesmo plano, em qualquer sistema.
    public static IReadOnlyList<(string Path, string Content)> Render(IdeaArtifacts artifacts) =>
    [
        (SpecificationFile, Specification(artifacts.Idea, artifacts.Specification!).ReplaceLineEndings("\n")),
        (ArchitectureFile, Architecture(artifacts.Architecture!).ReplaceLineEndings("\n")),
        (BacklogFile, Backlog(artifacts.Backlog!).ReplaceLineEndings("\n"))
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
        return text.ToString();
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
        return text.ToString();
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
        return text.ToString().TrimEnd() + "\n";
    }

    private static void Bullets(StringBuilder text, string title, IEnumerable<string> items)
    {
        var list = items.ToList();
        if (list.Count == 0) return;
        text.Append("## ").AppendLine(title).AppendLine();
        foreach (var item in list) text.Append("- ").AppendLine(item);
        text.AppendLine();
    }
}
