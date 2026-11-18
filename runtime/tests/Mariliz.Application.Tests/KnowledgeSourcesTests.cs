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
        project.Write("spock.yaml", "schema: 1\nknowledge:\n  exclude:\n    - docs/interno/\n");

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
        project.Write("spock.yaml", "schema: 1\nknowledge:\n  include:\n    - docs/\n");

        var selected = KnowledgeSources.Select(new LocalProjectFiles(project.Root), new Dictionary<string, string>());

        Assert.Equal(60, selected.Count);
        Assert.All(selected, source => Assert.StartsWith("docs/", source.Path));
    }

    [Fact]
    public void Codigo_cjs_e_mjs_entra_antes_de_configuracao_quando_o_limite_corta()
    {
        using var project = new TempProject();
        project.Write("electron/main.cjs", "module.exports = { iniciar() { return 1; } };");
        project.Write("services/api/server.mjs", "export function ouvir() { return 2; }");
        project.Write("src/shared/x.cts", "export const x = 1;");
        for (var i = 0; i < KnowledgeSources.MaxFiles; i++) project.Write($"config/c-{i:0000}.json", $"{{\"n\": {i}, \"texto\": \"configuracao\"}}");

        var evaluated = KnowledgeSources.Evaluate(new LocalProjectFiles(project.Root)).ToDictionary(e => e.Path, e => e.Reason);

        foreach (var code in new[] { "electron/main.cjs", "services/api/server.mjs", "src/shared/x.cts" })
            Assert.Equal(KnowledgeSources.Exclusion.None, evaluated[code]);
        Assert.Equal(3, evaluated.Values.Count(reason => reason == KnowledgeSources.Exclusion.OverLimit));
    }

    [Fact]
    public void Codigo_fonte_entra_antes_dos_testes_do_mesmo_tipo_quando_o_limite_corta()
    {
        using var project = new TempProject();
        for (var i = 0; i < KnowledgeSources.MaxFiles; i++) project.Write($"scripts/caso-{i:0000}.test.cjs", $"test('caso {i}', () => {{}})");
        project.Write("src/shared/grade.cjs", "module.exports = { faixa() { return null; } };");
        project.Write("tests/unit/grade_spec.py", "def test_x():\n    assert True\n");

        var evaluated = KnowledgeSources.Evaluate(new LocalProjectFiles(project.Root)).ToDictionary(e => e.Path, e => e.Reason);

        Assert.Equal(KnowledgeSources.Exclusion.None, evaluated["src/shared/grade.cjs"]);
        Assert.Equal(KnowledgeSources.Exclusion.OverLimit, evaluated["tests/unit/grade_spec.py"]);
        Assert.Equal(2, evaluated.Values.Count(reason => reason == KnowledgeSources.Exclusion.OverLimit));
    }
}
