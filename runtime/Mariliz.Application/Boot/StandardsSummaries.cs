using System.Text.Json.Nodes;
using static Mariliz.Application.Boot.SummaryJson;

namespace Mariliz.Application.Boot;

/// <summary>Resumos da análise de arquitetura e da política dos agentes.</summary>
internal static class StandardsSummaries
{
    public static CapabilitySummary Architecture(JsonObject result)
    {
        var architecture = Object(result, "architecture");
        if (Object(architecture, "report") is not { } report) return NoData("da análise de arquitetura");

        var dotnet = Array(report, "dotnetProjects");
        var frontends = Array(report, "frontends");
        var findings = Array(report, "findings");
        var warnings = findings.Count(finding => Text(finding, "severity") == "warning");
        var infos = findings.Count(finding => Text(finding, "severity") == "info");
        var evidence = new List<string>
        {
            $"{Plural(warnings, "achado que fere a direção das camadas", "achados que ferem a direção das camadas")} e {Plural(infos, "fato para ponderar", "fatos para ponderar")}."
        };
        var untested = dotnet.Count(project => Flag(project, "tested") == false && Text(project, "layer") != "tests");
        if (untested > 0) evidence.Add($"{Plural(untested, "projeto .NET sem teste", "projetos .NET sem teste")}.");

        if (Object(architecture, "assessment") is not { } assessment)
            evidence.Add("Sem avaliação de arquitetura gravada no projeto.");
        else if (Flag(assessment, "valid") != true)
        {
            var checks = Array(assessment, "checks");
            evidence.Add($"A avaliação gravada não passou na conferência: {checks.Count(check => Flag(check, "passed") == false)} de {checks.Count} checagens falharam.");
        }
        else
        {
            var divergences = Array(assessment, "divergences");
            int Of(string classification) => divergences.Count(item => Text(item, "classification") == classification);
            evidence.Add($"Avaliação conferida: {Plural(divergences.Count, "divergência", "divergências")} ({Of("risk")} de risco, {Of("cost")} de custo, {Of("style")} de estilo) "
                + $"e {Plural(Array(assessment, "conventions").Count, "convenção registrada", "convenções registradas")}.");
        }

        var headline = dotnet.Count == 0 && frontends.Count == 0
            ? "Nenhum projeto .NET nem módulo de frontend reconhecido."
            : $"{Plural(dotnet.Count, "projeto .NET", "projetos .NET")} e {Plural(frontends.Count, "módulo de frontend", "módulos de frontend")} analisados.";
        return new CapabilitySummary(headline, evidence);
    }

    public static CapabilitySummary AgentPolicy(JsonObject result)
    {
        if (!HasList(result, "rules")) return NoData("da política dos agentes");
        var rules = Array(result, "rules");
        if (rules.Count == 0) return new CapabilitySummary("Nenhuma regra de política para os agentes.", []);

        int Decision(string name) => rules.Count(rule => Text(rule, "decision") == name);
        int Scope(string name) => rules.Count(rule => Text(rule, "scope") == name);
        var evidence = new List<string>
        {
            $"Por decisão: {Decision("deny")} de bloqueio, {Decision("ask")} de confirmação, {Decision("allow")} de liberação.",
            $"Por alvo: {Scope("command")} sobre comandos, {Scope("write")} sobre gravação de arquivos."
        };
        var limits = Size(result, "limits");
        if (limits > 0) evidence.Add($"{Plural(limits, "limitação conhecida", "limitações conhecidas")} desta proteção, descritas na tela.");
        return new CapabilitySummary($"{Plural(rules.Count, "regra de política", "regras de política")} para os agentes.", evidence);
    }
}
