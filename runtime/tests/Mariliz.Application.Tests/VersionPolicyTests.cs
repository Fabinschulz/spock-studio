using System.Text.Json;
using Mariliz.Application.Compatibility;

namespace Mariliz.Application.Tests;

// MV-5: a política de versões de projeto novo (docs/compatibilidade-multiversao/POLITICA.md, seção 2). O catálogo é fixo; o
// embutido muda a cada atualização e só é conferido no formato.
public sealed class VersionPolicyTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    private static readonly ReleaseCatalog Catalog = ReleaseCatalog.Parse("""
        {
          "schemaVersion": 1,
          "generatedAt": "2026-11-19T00:00:00Z",
          "technologies": [
            { "id": "dotnet", "name": ".NET", "policy": "lifecycle", "cycle": "major.minor", "source": "https://dotnet", "checkedAt": "2026-11-19T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "12.0", "channel": "preview", "status": "preview", "eol": "2029-11-14", "language": "C# 16" },
                { "cycle": "11.0", "channel": "sts", "status": "active", "eol": "2028-11-14", "latest": "11.0.0", "language": "C# 15" },
                { "cycle": "10.0", "channel": "lts", "status": "active", "eol": "2028-11-14", "latest": "10.0.15", "language": "C# 14" },
                { "cycle": "9.0", "channel": "sts", "status": "active", "eol": "2027-05-12", "latest": "9.0.23", "language": "C# 13" },
                { "cycle": "8.0", "channel": "lts", "status": "maintenance", "eol": "2026-11-10", "latest": "8.0.34", "language": "C# 12" } ] },
            { "id": "node", "name": "Node.js", "policy": "lifecycle", "cycle": "major", "source": "https://node", "checkedAt": "2026-11-19T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "27", "channel": "preview", "status": "preview", "eol": "2030-04-30" },
                { "cycle": "26", "channel": "lts", "status": "active", "eol": "2029-04-30", "latest": "26.13.1" },
                { "cycle": "24", "channel": "lts", "status": "maintenance", "eol": "2028-04-30", "latest": "24.22.3" } ] },
            { "id": "python", "name": "Python", "policy": "lifecycle", "cycle": "major.minor", "source": "https://python", "checkedAt": "2026-11-19T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "3.16", "channel": "preview", "status": "preview", "eol": "2032-10" },
                { "cycle": "3.15", "channel": "stable", "status": "active", "releaseDate": "2026-10-09", "eol": "2031-10" },
                { "cycle": "3.14", "channel": "stable", "status": "active", "releaseDate": "2025-10-07", "eol": "2030-10" } ] },
            { "id": "java", "name": "Java", "policy": "vendor", "cycle": "major", "source": "https://java", "checkedAt": "2026-11-19T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "27", "channel": "stable", "status": "active" },
                { "cycle": "25", "channel": "lts", "status": "active" } ] },
            { "id": "rust", "name": "Rust", "policy": "rolling", "cycle": "major.minor", "source": "https://rust", "checkedAt": "2026-11-19T00:00:00Z", "stale": true,
              "lines": [ { "cycle": "1.100", "channel": "stable", "status": "active", "latest": "1.100.0" } ] },
            { "id": "vite", "name": "Vite", "policy": "latest-major", "cycle": "major", "source": "https://vite", "checkedAt": "2026-11-19T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "9", "channel": "stable", "status": "active", "latest": "9.0.0", "requires": { "node": ">=28.0.0" } },
                { "cycle": "8", "channel": "stable", "status": "active", "latest": "8.3.8", "requires": { "node": "^20.19.0 || >=22.12.0" } } ] }
          ]
        }
        """);

    private static VersionRecommendation One(string technology, string goal = VersionGoals.Production, params string[] others) =>
        VersionPolicy.Recommend([technology, .. others], goal, Catalog, Today).Technologies.Single(item => item.TechnologyId == technology);

    [Fact]
    public void Producao_escolhe_a_LTS_ativa_mais_nova_e_mostra_a_STS_que_empata_em_fim_de_suporte()
    {
        var dotnet = One("dotnet");

        Assert.Equal(("10.0", "lts", "10.0.15", "C# 14"), (dotnet.Choice!.Cycle, dotnet.Choice.Channel, dotnet.Choice.Version, dotnet.Choice.Language));
        Assert.Contains("Empata em fim de suporte com a 11.0 (STS)", dotnet.Choice.Reason);
        Assert.Equal("11.0", Assert.Single(dotnet.Alternatives).Cycle);
    }

    [Fact]
    public void Objetivo_mais_nova_escolhe_a_STS_e_deixa_a_LTS_como_alternativa_de_producao()
    {
        var dotnet = One("dotnet", VersionGoals.Latest);

        Assert.Equal("11.0", dotnet.Choice!.Cycle);
        var lts = Assert.Single(dotnet.Alternatives);
        Assert.Equal(("10.0", "lts"), (lts.Cycle, lts.Channel));
        Assert.Contains("a escolha para produção", lts.Reason);
    }

    [Theory]
    [InlineData(VersionGoals.Production)]
    [InlineData(VersionGoals.Latest)]
    public void Pre_lancamento_nunca_e_escolhido_nem_alternativa(string goal)
    {
        foreach (var technology in new[] { "dotnet", "node", "python" })
        {
            var recommendation = One(technology, goal);
            Assert.NotEqual("preview", recommendation.Choice!.Channel);
            Assert.DoesNotContain(recommendation.Alternatives, item => item.Channel == "preview");
        }
    }

    [Fact]
    public void Linha_ativa_com_menos_de_12_meses_de_suporte_nao_e_recomendada()
    {
        var dotnet = One("dotnet", VersionGoals.Latest);

        Assert.DoesNotContain(dotnet.Alternatives, item => item.Cycle == "9.0");
        Assert.Contains(dotnet.Notes, note => note.Contains(".NET 9.0 fica de fora") && note.Contains("2027-05-12"));
    }

    [Fact]
    public void Mesmo_pedido_e_mesmo_catalogo_dao_a_mesma_resposta()
    {
        string[] request = ["vite", "dotnet", "node", "python", "java", "rust"];
        VersionRecommendationReport Run(string[] technologies) => VersionPolicy.Recommend(technologies, VersionGoals.Production, Catalog, Today);

        Assert.Equal(JsonSerializer.Serialize(Run(request)), JsonSerializer.Serialize(Run(request)));
        // A ordem do pedido só muda a ordem da resposta: o Node é escolhido antes dos pacotes que dependem dele.
        Assert.Equal(Sorted(Run(request)), Sorted(Run([.. request.Reverse()])));
    }

    private static string Sorted(VersionRecommendationReport report) =>
        JsonSerializer.Serialize(report.Technologies.OrderBy(item => item.TechnologyId, StringComparer.Ordinal));

    [Fact]
    public void Pacote_do_npm_cai_para_a_linha_que_aceita_o_Node_escolhido()
    {
        var vite = One("vite", VersionGoals.Production, "node");

        Assert.Equal("8", vite.Choice!.Cycle);
        Assert.Contains(vite.Notes, note => note.Contains("Vite 9 exige Node >=28.0.0") && note.Contains("a linha 8 aceita"));
        Assert.Contains(vite.Alternatives, item => item.Cycle == "9");
    }

    [Fact]
    public void Sem_Node_no_pedido_o_requisito_vira_aviso()
    {
        var vite = One("vite");

        Assert.Equal("9", vite.Choice!.Cycle);
        Assert.Contains(vite.Notes, note => note == "Vite 9 exige Node >=28.0.0.");
    }

    [Fact]
    public void Linha_lancada_ha_pouco_e_escolhida_com_aviso()
    {
        var python = One("python");

        Assert.Equal(("3.15", null), (python.Choice!.Cycle, python.Choice.Version));
        Assert.Contains(python.Notes, note => note.Contains("Python 3.15 saiu em 2026-10-09"));
        Assert.Contains("suporte até 2031-10", python.Choice.Reason);
    }

    [Fact]
    public void Fornecedor_sem_data_e_fonte_que_falhou_ficam_nas_notas()
    {
        var java = One("java");
        var rust = One("rust");

        Assert.Equal(("25", "lts"), (java.Choice!.Cycle, java.Choice.Channel));
        Assert.Contains(java.Notes, note => note.Contains("depende do fornecedor"));
        Assert.Equal("1.100", rust.Choice!.Cycle);
        Assert.True(rust.Stale);
        Assert.Contains(rust.Notes, note => note.Contains("A última consulta à fonte falhou"));
    }

    [Fact]
    public void Tecnologia_fora_do_catalogo_nao_recebe_versao()
    {
        var report = VersionPolicy.Recommend(["dotnet", "Laravel"], VersionGoals.Production, Catalog, Today);

        var unknown = report.Technologies.Single(item => item.TechnologyId == "laravel");
        Assert.Null(unknown.Choice);
        Assert.Contains("Fora do catálogo", Assert.Single(unknown.Notes));
    }

    [Fact]
    public void Pedido_invalido_e_recusado()
    {
        Assert.Throws<ArgumentException>(() => VersionPolicy.Recommend(["dotnet"], "beta", Catalog, Today));
        Assert.Throws<ArgumentException>(() => VersionPolicy.Recommend([" "], VersionGoals.Production, Catalog, Today));
    }

    [Fact]
    public void Catalogo_embutido_recomenda_linha_estavel_para_cada_tecnologia_com_ciclo_de_vida()
    {
        var today = DateOnly.FromDateTime(ReleaseCatalog.Embedded.GeneratedAt.UtcDateTime);
        var ids = ReleaseCatalog.Embedded.Technologies.Select(item => item.Id).ToList();

        var report = VersionPolicy.Recommend(ids, VersionGoals.Production, ReleaseCatalog.Embedded, today);

        Assert.All(report.Technologies.Where(item => item.Choice is not null), item => Assert.NotEqual("preview", item.Choice!.Channel));
        Assert.All(new[] { "dotnet", "node", "python" },
            id => Assert.NotNull(report.Technologies.Single(item => item.TechnologyId == id).Choice));
    }
}
