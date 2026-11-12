using Mariliz.Application.Integrations;
using Mariliz.Application.Management;

namespace Mariliz.Application.Tests;

/// <summary>
/// Cycle time: dias corridos do início do trabalho à conclusão. O início é a primeira mudança de status para um status da
/// categoria "em andamento"; a categoria de cada nome vem dos itens que estão nele. As contas abaixo foram feitas à mão.
/// </summary>
public class CycleTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static WorkItem Item(
        string id, string status, string category, DateTimeOffset? closed = null, params (DateTimeOffset At, string From, string To)[] moves) =>
        new(DataKinds.Issues, id, "Item " + id, "Story", status, category, [], [], (closed ?? Now).AddDays(-30), closed ?? Now, closed, null, null,
            "https://example.test/" + id, moves.Select(move => new WorkItemTransition(move.At, move.From, move.To)).ToList());

    /// <summary>Um item entregue há <paramref name="closedDaysAgo"/> dias cujo trabalho começou <paramref name="cycleDays"/> dias antes de fechar.</summary>
    private static WorkItem Delivered(string id, int closedDaysAgo, double cycleDays, string started = "In Progress")
    {
        var closed = Now.AddDays(-closedDaysAgo);
        return Item(id, "Done", StatusCategories.Done, closed,
            (closed.AddDays(-cycleDays), "To Do", started), (closed, started, "Done"));
    }

    /// <summary>Itens abertos que ensinam ao cálculo a categoria de cada nome de status do fluxo.</summary>
    private static IEnumerable<WorkItem> Vocabulary() =>
    [
        Item("o1", "To Do", StatusCategories.Todo),
        Item("o2", "In Progress", StatusCategories.InProgress),
        Item("o3", "Code Review", StatusCategories.InProgress),
        Item("o4", "Done", StatusCategories.Done, Now.AddDays(-200))
    ];

    private static CycleTimeMetric Cycle(IEnumerable<WorkItem> items) =>
        FlowCalculator.Compute(DataKinds.Issues, items.ToList(), Now, weeks: 8).CycleTime;

    [Fact]
    public void Mediana_e_percentil_85_sobre_os_itens_com_inicio_conhecido_conferidos_a_mao()
    {
        // Ciclos de 1, 2, 3, 4, 5, 6 e 10 dias. Sete itens: a mediana é o 4º (4) e o P85 é o ⌈0,85·7⌉ = 6º (6).
        var items = Vocabulary().Concat(new[] { 1.0, 2, 3, 4, 5, 6, 10 }.Select((cycle, index) => Delivered("d" + index, 3 + index, cycle)));

        var metric = Cycle(items);

        Assert.Equal(7, metric.Sample);
        Assert.Equal(0, metric.Excluded);
        Assert.Equal(4.0, metric.MedianDays);
        Assert.Equal(6.0, metric.P85Days);
        Assert.Null(metric.Unavailable);
    }

    [Fact]
    public void O_inicio_e_a_primeira_mudanca_para_andamento_mesmo_com_varias_ou_com_reabertura()
    {
        var closed = Now.AddDays(-2);
        var items = Vocabulary().Concat(new[]
        {
            // Entrou em andamento 10 dias antes, passou por revisão, foi concluído e reaberto: vale a primeira entrada.
            Item("a", "Done", StatusCategories.Done, closed,
                (closed.AddDays(-10), "To Do", "In Progress"), (closed.AddDays(-4), "In Progress", "Code Review"),
                (closed.AddDays(-1), "Code Review", "Done"), (closed.AddDays(-0.5), "Done", "In Progress"), (closed, "In Progress", "Done")),
            Delivered("b", 3, 2), Delivered("c", 4, 2), Delivered("d", 5, 2), Delivered("e", 6, 2)
        });

        var metric = Cycle(items);

        // Ciclos: 10, 2, 2, 2, 2 → ordenados 2, 2, 2, 2, 10: mediana 2, P85 = ⌈0,85·5⌉ = 5º = 10.
        Assert.Equal((5, 2.0, 10.0), (metric.Sample, metric.MedianDays!.Value, metric.P85Days!.Value));
    }

    [Fact]
    public void Item_entregue_sem_passar_por_andamento_ou_por_status_desconhecido_fica_de_fora_e_e_contado()
    {
        var closed = Now.AddDays(-5);
        var items = Vocabulary()
            .Concat(Enumerable.Range(0, 5).Select(i => Delivered("ok" + i, 3 + i, 3)))
            // Foi direto de "To Do" para "Done": não há início.
            .Append(Item("direto", "Done", StatusCategories.Done, closed, (closed, "To Do", "Done")))
            // Começou em "Em Revisão", um nome que nenhum item ocupa hoje: não dá para saber a categoria.
            .Append(Delivered("desconhecido", 6, 4, started: "Em Revisão"));

        var metric = Cycle(items);

        Assert.Equal(5, metric.Sample);
        Assert.Equal(2, metric.Excluded);
        Assert.Equal(3.0, metric.MedianDays);
    }

    [Fact]
    public void O_nome_de_status_nao_diferencia_maiusculas()
    {
        var items = Vocabulary().Concat(Enumerable.Range(0, 5).Select(i => Delivered("d" + i, 3 + i, 2, started: "in progress")));

        Assert.Equal(5, Cycle(items).Sample);
    }

    [Fact]
    public void Nome_de_status_com_categorias_conflitantes_nao_e_classificado()
    {
        // "Blocked" está em andamento num item e a fazer em outro: o nome não diz nada. Os itens que começaram nele saem.
        var items = Vocabulary().Concat(new[]
        {
            Item("b1", "Blocked", StatusCategories.InProgress),
            Item("b2", "Blocked", StatusCategories.Todo)
        }).Concat(Enumerable.Range(0, 5).Select(i => Delivered("d" + i, 3 + i, 2, started: "Blocked")));

        var metric = Cycle(items);

        Assert.Equal((0, 5), (metric.Sample, metric.Excluded));
        Assert.Null(metric.MedianDays);
        Assert.Contains("ao menos 5", metric.Unavailable);
        Assert.Contains("5 sem início conhecido", metric.Unavailable);
    }

    [Fact]
    public void Fonte_sem_historico_de_status_diz_que_nao_informa_o_inicio_em_vez_de_inventar()
    {
        // Como o GitHub: nenhum item tem transição.
        var items = new[]
        {
            Item("1", "open", StatusCategories.InProgress),
            Item("2", "closed", StatusCategories.Done, Now.AddDays(-3)),
            Item("3", "closed", StatusCategories.Done, Now.AddDays(-4))
        };

        var metric = Cycle(items);

        Assert.Equal(FlowCalculator.NoStartInformation, metric.Unavailable);
        Assert.Equal((0, 0, (double?)null), (metric.Sample, metric.Excluded, metric.MedianDays));
    }

    [Fact]
    public void Amostra_pequena_nao_vira_tendencia()
    {
        var items = Vocabulary().Concat(Enumerable.Range(0, 3).Select(i => Delivered("d" + i, 3 + i, 2)));

        var metric = Cycle(items);

        Assert.Null(metric.MedianDays);
        Assert.Equal(3, metric.Sample);
        Assert.Contains("há 3", metric.Unavailable);
    }

    [Fact]
    public void So_entra_o_item_entregue_dentro_do_periodo()
    {
        var items = Vocabulary()
            .Concat(Enumerable.Range(0, 5).Select(i => Delivered("d" + i, 3 + i, 2)))
            .Append(Delivered("antigo", 200, 50));

        var metric = Cycle(items);

        Assert.Equal(5, metric.Sample);
        Assert.Equal(2.0, metric.P85Days);
    }

    [Fact]
    public void Sem_itens_o_indicador_diz_que_nao_ha_itens()
    {
        var flow = FlowCalculator.Compute(DataKinds.Issues, [], Now, 8);

        Assert.Equal(FlowCalculator.NoItems, flow.CycleTime.Unavailable);
    }

    [Fact]
    public void O_cycle_time_deixou_de_constar_como_nao_calculado()
    {
        Assert.DoesNotContain(ManagementService.NotCalculatedYet, item => item.Id == "cycleTime");
        Assert.Contains(ManagementService.NotCalculatedYet, item => item.Id == "blockers");
    }
}
