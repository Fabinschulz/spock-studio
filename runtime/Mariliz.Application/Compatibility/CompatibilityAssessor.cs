using System.Globalization;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Compatibility;

// A classificação orienta o que o agente pode usar; nunca impede o trabalho num projeto legado.
public static class SupportStatuses
{
    public const string Current = "current";
    public const string Supported = "supported";
    public const string Maintenance = "maintenance";
    public const string EndOfLife = "end-of-life";
    public const string Preview = "preview";
    public const string Partial = "partial";
    public const string Unknown = "unknown";

    public static string Label(string status) => status switch
    {
        Current => "Moderno",
        Supported => "Suportado",
        Maintenance => "Legado suportado",
        EndOfLife => "Legado sem suporte",
        Preview => "Pré-lançamento",
        Partial => "Parcialmente validado",
        _ => "Desconhecido"
    };
}

/// <param name="Confirmed">A versão veio de um arquivo que a fixa (instalada, TargetFramework, .nvmrc), não de uma faixa declarada.</param>
/// <param name="NewestStable">A linha estável mais nova do catálogo: referência para modernização, nunca alvo da tarefa.</param>
/// <param name="Guidance">A restrição dita ao agente para esta pasta.</param>
public sealed record ModuleCompatibility(
    string Root,
    string TechnologyId,
    string Name,
    string? Version,
    string Origin,
    string Status,
    string StatusLabel,
    string? Cycle,
    string? Channel,
    string? Eol,
    string? LatestInCycle,
    string? NewestStable,
    string? Language,
    bool Confirmed,
    string Reason,
    string Guidance,
    IReadOnlyList<string> Evidence);

/// <param name="CatalogStale">Alguma fonte do catálogo falhou na última consulta, ou o catálogo tem mais de 90 dias.</param>
public sealed record CompatibilityReport(
    DateTimeOffset CatalogGeneratedAt,
    bool CatalogStale,
    IReadOnlyList<ModuleCompatibility> Modules);

public static class CompatibilityAssessor
{
    public static readonly TimeSpan CatalogFreshFor = TimeSpan.FromDays(90);
    private const int EndOfLifeSoonDays = 180;

    // Frameworks fora do catálogo não entram: sem linha de suporte, repetiriam a lista do perfil sem dizer nada novo.
    public static CompatibilityReport Assess(IReadOnlyList<ProjectModule> modules, ReleaseCatalog catalog, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var assessed = modules
            .Where(module => module.Origin != ProjectModules.Framework || catalog.Find(module.TechnologyId) is not null)
            .Select(module => Assess(module, catalog, today))
            .ToList();
        var stale = now - catalog.GeneratedAt > CatalogFreshFor || catalog.Technologies.Any(technology => technology.Stale);
        return new CompatibilityReport(catalog.GeneratedAt, stale, assessed);
    }

    public static ModuleCompatibility Assess(ProjectModule module, ReleaseCatalog catalog, DateOnly today)
    {
        var technology = catalog.Find(module.TechnologyId);
        var cycle = technology is null ? null : VersionCycles.Of(technology, module.Version);
        var line = cycle is null ? null : technology!.Lines.FirstOrDefault(item => item.Cycle == cycle);
        var newest = technology is null ? null : NewestStable(technology);
        var (status, reason) = Classify(module, technology, cycle, line, newest, today);
        var confirmed = module.Confidence == Confidence.High;
        return new ModuleCompatibility(module.Root, module.TechnologyId, module.Name, module.Version, module.Origin, status, SupportStatuses.Label(status),
            cycle, line?.Channel, line?.Eol, line?.Latest, newest?.Latest ?? newest?.Cycle, line?.Language, confirmed, reason,
            Guidance(module, status, line, confirmed), module.Evidence);
    }

    private static (string Status, string Reason) Classify(ProjectModule module, CatalogTechnology? technology, string? cycle, ReleaseLine? line,
        ReleaseLine? newest, DateOnly today)
    {
        if (module.Version is null) return (SupportStatuses.Unknown, "Versão não declarada nos arquivos do projeto.");
        if (technology is null) return (SupportStatuses.Unknown, "Tecnologia fora do catálogo de versões.");
        if (cycle is null) return (SupportStatuses.Unknown, $"{module.Version} não corresponde a uma linha de {technology.Name} no catálogo.");
        if (line is null) return Unlisted(technology, cycle, newest);

        var eol = EndOfLife(line.Eol);
        if (line.Status == "preview") return (SupportStatuses.Preview, $"{technology.Name} {cycle} ainda não é estável ({line.Channel}).");
        if (line.Status == "eol" || eol < today)
            return (SupportStatuses.EndOfLife, eol is { } ended ? $"Fim do suporte oficial em {ended:yyyy-MM-dd}." : "Fora do suporte oficial.");
        if (line.Status == "vendor")
            return (SupportStatuses.Partial, $"Fim do suporte de {technology.Name} {cycle} depende do fornecedor; o catálogo não tem a data.");
        if (line.Status != "active" && line.Status != "maintenance")
            return (SupportStatuses.Partial, $"{technology.Name} não publica fim de suporte por linha em formato legível; a linha mantida é a {newest?.Cycle}.");

        var soon = eol is { } date && date.DayNumber - today.DayNumber <= EndOfLifeSoonDays ? $" O suporte termina em {date:yyyy-MM-dd}." : "";
        if (line.Status == "maintenance") return (SupportStatuses.Maintenance, $"Linha em manutenção: só correções críticas e de segurança.{soon}");
        var newestLts = NewestActive(technology, lts: true);
        return line.Cycle == newest?.Cycle || line.Cycle == newestLts?.Cycle
            ? (SupportStatuses.Current, $"Linha estável mais recente{(line.Channel == "lts" ? " com suporte longo (LTS)" : "")}.{soon}")
            : (SupportStatuses.Supported, $"Suportada; a linha mais nova é a {newest?.Cycle}.{soon}");
    }

    private static (string, string) Unlisted(CatalogTechnology technology, string cycle, ReleaseLine? newest)
    {
        var oldest = technology.Lines.Select(item => item.Cycle).OrderBy(item => item, Comparer<string>.Create(VersionCycles.Compare)).FirstOrDefault();
        if (newest is not null && VersionCycles.Compare(cycle, newest.Cycle) > 0)
            return (SupportStatuses.Unknown, $"{technology.Name} {cycle} é mais nova que o catálogo de {technology.CheckedAt:yyyy-MM-dd}.");
        if (technology.Policy == "supported-window" && oldest is not null && VersionCycles.Compare(cycle, oldest) < 0)
            return (SupportStatuses.EndOfLife, $"{technology.Name} suporta só as linhas {string.Join(" e ", technology.Lines.Where(item => item.Status == "active").Select(item => item.Cycle))}.");
        if (technology.Policy == "rolling")
            return (SupportStatuses.Supported, $"{technology.Name} corrige só a estável mais recente ({newest?.Latest}); versões anteriores seguem válidas, sem correção.");
        return (SupportStatuses.Unknown, $"{technology.Name} {cycle} não está no catálogo.");
    }

    private static string Guidance(ProjectModule module, string status, ReleaseLine? line, bool confirmed)
    {
        var target = module.Version is null ? module.Name : $"{module.Name} {module.Version}{(line?.Language is { } language ? $" ({language})" : "")}";
        if (module.Version is null)
            return $"Em {module.Root}: a versão de {module.Name} não foi encontrada; leia os manifestos antes de usar recurso que dependa de versão.";
        var text = $"Em {module.Root}: escreva código e configuração para {target}; não use API, sintaxe, pacote nem opção de build de versão mais nova, " +
                   "e mantenha o gerenciador de pacotes e os lockfiles do projeto.";
        if (module.Origin == ProjectModules.Runtime && module.Name.EndsWith("(projeto)", StringComparison.Ordinal))
            text = $"Em {module.Root}: o projeto compila para {module.Version}; use só APIs disponíveis nesse alvo.";
        if (!confirmed) text += " A versão vem de uma faixa declarada: confirme no lockfile antes de depender de um recurso novo.";
        if (status is SupportStatuses.EndOfLife or SupportStatuses.Maintenance)
            text += " Atualizar a versão é uma modernização à parte, com plano aprovado; não a misture com esta tarefa.";
        return text;
    }

    private static ReleaseLine? NewestStable(CatalogTechnology technology) => NewestActive(technology, lts: false);

    private static ReleaseLine? NewestActive(CatalogTechnology technology, bool lts) =>
        technology.Lines.Where(item => item.Status == "active" && item.Channel != "preview" && (!lts || item.Channel == "lts"))
            .OrderByDescending(item => item.Cycle, Comparer<string>.Create(VersionCycles.Compare))
            .FirstOrDefault();

    // O Python publica só o mês do fim de suporte: vale até o último dia dele.
    private static DateOnly? EndOfLife(string? eol)
    {
        if (eol is null) return null;
        if (DateOnly.TryParseExact(eol, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return day;
        return DateOnly.TryParseExact(eol, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)
            ? month.AddMonths(1).AddDays(-1)
            : null;
    }
}
