using System.Globalization;

namespace Mariliz.Application.Management;

/// <summary>Um risco como estava num registro: só identificador, título, gravidade e os itens citados (identificadores).</summary>
public sealed record RiskSnapshotEntry(string Id, string Title, string Severity, IReadOnlyList<string> Items);

/// <summary>
/// O que as regras de risco achavam do projeto num instante. Só entra um registro novo quando algo mudou; se nada mudou,
/// só <see cref="CheckedAt"/> avança. Sem título de item nem conteúdo do provedor: só identificadores.
/// </summary>
/// <param name="At">Quando este estado foi visto pela primeira vez.</param>
/// <param name="CheckedAt">A última vez em que ele foi confirmado.</param>
public sealed record RiskSnapshot(DateTimeOffset At, DateTimeOffset CheckedAt, IReadOnlyList<RiskSnapshotEntry> Risks);

/// <summary>Onde os registros de risco de um projeto ficam. Um arquivo por projeto: registros de projetos diferentes não se misturam.</summary>
public interface IRiskHistoryStore
{
    IReadOnlyList<RiskSnapshot> Load(string projectId);
    void Save(string projectId, IReadOnlyList<RiskSnapshot> snapshots);
}

public static class RiskChangeKinds
{
    /// <summary>Já estava ativo no primeiro registro: não se sabe desde quando.</summary>
    public const string Observed = "observed";

    public const string Appeared = "appeared";
    public const string Raised = "raised";
    public const string Lowered = "lowered";
    public const string ItemsChanged = "items_changed";
    public const string Resolved = "resolved";
}

/// <param name="Kind">Um de <see cref="RiskChangeKinds"/>.</param>
/// <param name="From">A gravidade antes, quando mudou.</param>
/// <param name="To">A gravidade depois, quando há risco.</param>
public sealed record RiskChange(
    DateTimeOffset At, string Kind, string? From, string? To, IReadOnlyList<string> ItemsAdded, IReadOnlyList<string> ItemsRemoved);

/// <summary>A trajetória de um risco: quando apareceu, como mudou e se já foi resolvido. Do mais antigo ao mais recente.</summary>
public sealed record RiskTrail(
    string Id,
    string Title,
    bool Active,
    string? CurrentSeverity,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<RiskChange> Changes);

/// <param name="Since">Quando o primeiro registro foi feito; antes disso não há histórico, só o que as regras dizem hoje.</param>
/// <param name="Snapshots">Quantos registros existem.</param>
/// <param name="LastCheckedAt">A última vez que o estado foi conferido e registrado.</param>
public sealed record ProjectRiskHistory(
    string ProjectId,
    DateTimeOffset GeneratedAt,
    DateTimeOffset? Since,
    DateTimeOffset? LastCheckedAt,
    int Snapshots,
    IReadOnlyList<RiskTrail> Trails,
    int Omitted,
    string Basis);

/// <summary>Monta os registros e as trajetórias. Funções puras: os mesmos registros dão sempre a mesma resposta.</summary>
public static class RiskHistoryBuilder
{
    public const int MaxSnapshots = 200;
    public const int MaxTrails = 30;

    public const string Basis =
        "Cada registro é feito quando uma sincronização termina com todas as fontes completas, e só se grava um novo quando algo "
        + "mudou. O que aconteceu antes do primeiro registro, ou entre sincronizações, não é conhecido.";

    private static readonly Dictionary<string, int> Ranks = new(StringComparer.Ordinal) { ["low"] = 0, ["medium"] = 1, ["high"] = 2 };

    /// <summary>
    /// Posição na escala de gravidade. O registro vem do disco: uma gravidade que o motor não conhece (arquivo antigo ou editado)
    /// fica abaixo da escala, em vez de derrubar o histórico inteiro.
    /// </summary>
    private static int Rank(string severity) => Ranks.GetValueOrDefault(severity, -1);

    /// <summary>O estado atual das regras como um registro, para comparar com o último e guardar.</summary>
    public static IReadOnlyList<RiskSnapshotEntry> Entries(IEnumerable<ProjectRisk> risks) =>
        risks.OrderBy(risk => risk.Id, StringComparer.Ordinal)
            .Select(risk => new RiskSnapshotEntry(risk.Id, risk.Title, risk.Severity.ToString().ToLowerInvariant(),
                risk.Items.Select(item => item.ExternalId).Order(StringComparer.Ordinal).ToList()))
            .ToList();

    /// <summary>Mesmos riscos, mesmas gravidades e mesmos itens.</summary>
    public static bool Same(IReadOnlyList<RiskSnapshotEntry> a, IReadOnlyList<RiskSnapshotEntry> b) =>
        a.Count == b.Count && a.Zip(b).All(pair =>
            pair.First.Id == pair.Second.Id && pair.First.Severity == pair.Second.Severity
            && pair.First.Items.SequenceEqual(pair.Second.Items, StringComparer.Ordinal));

    /// <summary>
    /// Acrescenta o estado atual: igual ao último, só confirma; diferente, vira um registro novo. Mantém os
    /// <see cref="MaxSnapshots"/> mais recentes.
    /// </summary>
    public static List<RiskSnapshot> Append(IReadOnlyList<RiskSnapshot> snapshots, IReadOnlyList<RiskSnapshotEntry> current, DateTimeOffset now)
    {
        var list = snapshots.ToList();
        if (list.Count > 0 && Same(list[^1].Risks, current))
            list[^1] = list[^1] with { CheckedAt = now };
        else
            list.Add(new RiskSnapshot(now, now, current));
        if (list.Count > MaxSnapshots) list.RemoveRange(0, list.Count - MaxSnapshots);
        return list;
    }

    public static ProjectRiskHistory Build(string projectId, DateTimeOffset now, IReadOnlyList<RiskSnapshot> snapshots)
    {
        var ordered = snapshots.OrderBy(snapshot => snapshot.At).ToList();
        var trails = ordered.SelectMany(snapshot => snapshot.Risks.Select(risk => risk.Id)).Distinct(StringComparer.Ordinal)
            .Select(id => Trail(id, ordered)).ToList();

        var sorted = trails
            .OrderByDescending(trail => trail.Active)
            .ThenByDescending(trail => trail.Active ? Rank(trail.CurrentSeverity!) : -1)
            .ThenByDescending(trail => trail.ResolvedAt)
            .ThenBy(trail => trail.Id, StringComparer.Ordinal)
            .ToList();

        return new ProjectRiskHistory(projectId, now, ordered.Count == 0 ? null : ordered[0].At,
            ordered.Count == 0 ? null : ordered[^1].CheckedAt, ordered.Count, sorted.Take(MaxTrails).ToList(),
            Math.Max(0, sorted.Count - MaxTrails), Basis);
    }

    private static RiskTrail Trail(string id, IReadOnlyList<RiskSnapshot> snapshots)
    {
        var changes = new List<RiskChange>();
        RiskSnapshotEntry? previous = null;
        RiskSnapshotEntry? latest = null;
        DateTimeOffset firstSeen = default;
        DateTimeOffset? resolvedAt = null;

        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];
            var current = snapshot.Risks.FirstOrDefault(risk => risk.Id == id);
            if (current is not null)
            {
                latest = current;
                resolvedAt = null;
                if (previous is null)
                {
                    if (firstSeen == default) firstSeen = snapshot.At;
                    changes.Add(new RiskChange(snapshot.At, index == 0 ? RiskChangeKinds.Observed : RiskChangeKinds.Appeared, null,
                        current.Severity, current.Items, []));
                }
                else if (current.Severity != previous.Severity)
                {
                    changes.Add(new RiskChange(snapshot.At, Rank(current.Severity) > Rank(previous.Severity) ? RiskChangeKinds.Raised : RiskChangeKinds.Lowered,
                        previous.Severity, current.Severity, current.Items.Except(previous.Items).ToList(), previous.Items.Except(current.Items).ToList()));
                }
                else if (!current.Items.SequenceEqual(previous.Items, StringComparer.Ordinal))
                {
                    changes.Add(new RiskChange(snapshot.At, RiskChangeKinds.ItemsChanged, null, current.Severity,
                        current.Items.Except(previous.Items).ToList(), previous.Items.Except(current.Items).ToList()));
                }
            }
            else if (previous is not null)
            {
                resolvedAt = snapshot.At;
                changes.Add(new RiskChange(snapshot.At, RiskChangeKinds.Resolved, previous.Severity, null, [], previous.Items));
            }

            previous = current;
        }

        var active = previous is not null;
        return new RiskTrail(id, latest!.Title, active, active ? previous!.Severity : null, firstSeen, active ? null : resolvedAt, changes);
    }

    /// <summary>Formato de data usado nos textos; o motor não usa o relógio local.</summary>
    public static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
