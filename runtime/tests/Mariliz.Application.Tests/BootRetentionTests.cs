using Mariliz.Application.Boot;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public sealed class BootRetentionTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private const string ProjectA = "p-00000000000a";
    private const string ProjectB = "p-00000000000b";
    private readonly TempProject _project = TempProject.Dotnet();
    private readonly FileBootStore _store;

    public BootRetentionTests() => _store = new FileBootStore(_project.DataDir);

    public void Dispose() => _project.Dispose();

    // O mais velho tem o menor número: a ordem de criação é a da posição.
    private static BootRecord Record(string? projectId, int position, string state = BootStates.Completed, string? recoveredBy = null) => new()
    {
        Id = BootIds.New(),
        ProjectId = projectId,
        State = state,
        RecoveredByBootId = recoveredBy,
        CreatedAt = Start.AddMinutes(position),
        UpdatedAt = Start.AddMinutes(position)
    };

    private List<BootRecord> Seed(string? projectId, int count, int from = 0)
    {
        var records = Enumerable.Range(from, count).Select(position => Record(projectId, position)).ToList();
        foreach (var record in records) _store.Save(record);
        return records;
    }

    [Fact]
    public void Expira_so_o_que_passa_do_limite_e_ja_terminou()
    {
        var records = Enumerable.Range(0, 205).Select(position => Record(ProjectA, position)).ToList();

        var expired = BootRetention.Expired(records);

        Assert.Equal(5, expired.Count);
        Assert.Equal(records.Take(5).Select(record => record.Id).Order(StringComparer.Ordinal), expired.Select(record => record.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Boot_vivo_ou_interrompido_sem_recuperacao_nunca_sai_mesmo_sendo_o_mais_antigo()
    {
        var protectedOnes = new[]
        {
            Record(ProjectA, 0, BootStates.Interrupted),
            Record(ProjectA, 1, BootStates.Executing),
            Record(ProjectA, 2, BootStates.Planned)
        };
        var recovered = Record(ProjectA, 3, BootStates.Interrupted, recoveredBy: BootIds.New());
        var recent = Enumerable.Range(10, BootRetention.Keep).Select(position => Record(ProjectA, position));

        var expired = BootRetention.Expired([.. protectedOnes, recovered, .. recent]);

        Assert.Equal([recovered.Id], expired.Select(record => record.Id));
    }

    [Fact]
    public void Ao_subir_cada_escopo_fica_com_os_200_mais_novos_e_o_Boot_sem_projeto_tambem()
    {
        Seed(ProjectA, 230);
        var global = Seed(null, 210);
        var other = Seed(ProjectB, 50);
        var stuck = Record(ProjectA, -5, BootStates.Interrupted);
        _store.Save(stuck);

        var removed = BootRetention.Apply(_store);

        Assert.Equal(30 + 10, removed);
        Assert.Equal(200 + 1, _store.Count(ProjectA));
        Assert.Equal(200, _store.Count(null));
        Assert.Equal(50, _store.Count(ProjectB));
        Assert.NotNull(_store.Find(ProjectA, stuck.Id));
        Assert.Null(_store.Find(null, global[0].Id));
        Assert.NotNull(_store.Find(null, global[^1].Id));
        Assert.Equal(other.Count, _store.List(ProjectB).Count);
    }

    [Fact]
    public void Ao_terminar_um_Boot_so_poda_quando_a_pasta_passa_do_limite_com_folga()
    {
        Seed(ProjectA, BootRetention.Keep + BootRetention.Slack);
        BootRetention.PruneIfNeeded(_store, ProjectA);
        Assert.Equal(BootRetention.Keep + BootRetention.Slack, _store.Count(ProjectA));

        _store.Save(Record(ProjectA, 9999));
        BootRetention.PruneIfNeeded(_store, ProjectA);

        Assert.Equal(BootRetention.Keep, _store.Count(ProjectA));
        Assert.Contains(_store.List(ProjectA), record => record.CreatedAt == Start.AddMinutes(9999));
    }

    [Fact]
    public void O_armazenamento_conta_e_apaga_sem_aceitar_id_malformado()
    {
        var record = Seed(ProjectA, 1)[0];

        Assert.False(_store.Delete(ProjectA, "../../model"));
        Assert.False(_store.Delete(ProjectA, BootIds.New()));
        Assert.Equal(1, _store.Count(ProjectA));
        Assert.Equal(0, _store.Count(ProjectB));
        Assert.True(_store.Delete(ProjectA, record.Id));
        Assert.Equal(0, _store.Count(ProjectA));
    }
}
