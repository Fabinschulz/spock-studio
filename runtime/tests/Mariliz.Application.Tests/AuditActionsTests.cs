using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Audit;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Tests;

public class AuditActionsTests
{
    private static IReadOnlyList<string> All() => typeof(AuditActions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToList();

    [Fact]
    public void Toda_acao_de_projeto_tem_descricao_propria_na_trilha()
    {
        var generic = All()
            .Where(action => !AuditActions.MachineWide.Contains(action))
            .Where(action =>
            {
                var record = new AuditRecord(1, DateTimeOffset.UnixEpoch, action, new JsonObject { ["projectId"] = "p-1" });
                return ProjectAuditService.Describe(record, [], []).Any(entry => entry.Title.StartsWith("Operação registrada", StringComparison.Ordinal));
            })
            .ToList();

        Assert.True(generic.Count == 0, "Sem descrição própria: " + string.Join(", ", generic));
    }

    [Fact]
    public void Os_nomes_das_acoes_nao_se_repetem()
    {
        var all = All();
        Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
        Assert.All(AuditActions.MachineWide, action => Assert.Contains(action, all));
    }

    // Um nome escrito à mão em quem grava escapa da cobertura acima.
    [Fact]
    public void Quem_grava_na_auditoria_usa_os_nomes_de_AuditActions()
    {
        var literal = new Regex("""\.Record\(\s*"[a-z]+\.""");
        var offenders = new[] { "Mariliz.Application", "Mariliz.Infrastructure", "Mariliz.Infrastructure.Cloud", "Mariliz.Runtime" }
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(ContractFiles.Root, "runtime", project), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => literal.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(ContractFiles.Root, path))
            .ToList();

        Assert.True(offenders.Count == 0, "Ação de auditoria com nome literal em: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Execucao_ligada_a_um_item_diz_qual_item_sem_o_titulo()
    {
        var record = new AuditRecord(7, DateTimeOffset.UnixEpoch, AuditActions.RunLinked, new JsonObject
        {
            ["projectId"] = "p-1", ["runId"] = "r-1", ["integrationId"] = "i-1", ["kind"] = DataKinds.PullRequests, ["externalId"] = "42"
        });

        var entry = Assert.Single(ProjectAuditService.Describe(record, [], []));

        Assert.Equal(AuditCategories.Change, entry.Category);
        Assert.Equal("Execução ligada a pull request 42", entry.Title);
        Assert.Contains("r-1", entry.Detail, StringComparison.Ordinal);
    }
}
