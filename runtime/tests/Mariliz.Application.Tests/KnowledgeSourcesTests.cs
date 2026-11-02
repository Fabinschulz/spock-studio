using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Core.Policy;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class KnowledgeSourcesTests
{
    [Theory]
    [InlineData("*.log", "logs/app.log", true)]
    [InlineData("*.log", "app.logx", false)]
    [InlineData("/build", "build/out.txt", true)]
    [InlineData("/build", "src/build/out.txt", false)]
    [InlineData("build/", "src/build/out.txt", true)]
    [InlineData("build/", "build", false)]
    [InlineData("docs/**/rascunho*.md", "docs/a/b/rascunho-1.md", true)]
    [InlineData("docs/**/rascunho*.md", "outros/rascunho-1.md", false)]
    [InlineData("**/secrets/*", "app/secrets/token.txt", true)]
    [InlineData("arquivo?.txt", "arquivo1.txt", true)]
    [InlineData("[ab].txt", "c.txt", false)]
    [InlineData("!importante.log", "importante.log", false)]
    public void Padroes_do_gitignore_casam_como_o_git(string pattern, string path, bool expected)
    {
        Assert.Equal(expected, IgnorePatterns.Parse([pattern]).Matches(path));
    }

    [Fact]
    public void Fontes_respeitam_curingas_segredos_dependencias_e_a_declaracao()
    {
        using var project = TempProject.Dotnet();
        project.Write(".gitignore", "*.log\n**/rascunhos/\n");
        project.Write("app.log", "Linha de log longa o bastante para entrar no indice.");
        project.Write("docs/rascunhos/ideia.md", "Rascunho longo o bastante para entrar no indice.");
        project.Write("docs/guia.md", "Guia longo o bastante para entrar no indice.");
        project.Write("docs/interno/notas.md", "Notas internas longas o bastante para entrar no indice.");
        project.Write("id_rsa", "conteudo qualquer longo o bastante para indexar");
        project.Write("config/credentials.json", "{\"ok\": true, \"texto\": \"longo o bastante\"}");
        project.Write("web/app.min.js", "function a(){return 1}");
        project.Write("pnpm-lock.yaml", "lockfileVersion: 9");
        project.Write("mariliz.yaml", "schema: 1\nknowledge:\n  exclude:\n    - docs/interno/\n");

        var paths = KnowledgeSources.Select(new LocalProjectFiles(project.Root), new Dictionary<string, string>())
            .Select(source => source.Path).ToList();

        Assert.Contains("docs/guia.md", paths);
        foreach (var blocked in new[] { "app.log", "docs/rascunhos/ideia.md", "docs/interno/notas.md", "id_rsa", "config/credentials.json", "web/app.min.js", "pnpm-lock.yaml" })
            Assert.DoesNotContain(blocked, paths);
    }

    [Fact]
    public void Include_restringe_e_mais_de_quarenta_arquivos_entram()
    {
        using var project = TempProject.Dotnet();
        for (var i = 0; i < 60; i++) project.Write($"docs/nota-{i:00}.md", $"Nota {i} com texto longo o bastante para indexar.");
        project.Write("src/Outro.cs", "class Outro { }");
        project.Write("mariliz.yaml", "schema: 1\nknowledge:\n  include:\n    - docs/\n");

        var selected = KnowledgeSources.Select(new LocalProjectFiles(project.Root), new Dictionary<string, string>());

        Assert.Equal(60, selected.Count);
        Assert.All(selected, source => Assert.StartsWith("docs/", source.Path));
    }
}
