using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Core.Plan;

namespace Mariliz.Cli;

/// <summary>Texto para quem lê no terminal. O estado de cada item vem do motor; aqui só se formata.</summary>
internal static class CliRender
{
    private static string Str(JsonNode? n, string key) => n?[key]?.GetValue<string>() ?? "";
    private static IEnumerable<JsonNode?> Items(JsonNode? n, string key) => n?[key] is JsonArray a ? a : [];

    public static void Discovery(TextWriter w, JsonNode report)
    {
        w.WriteLine($"Projeto: {Str(report, "projectName")}");
        w.WriteLine("(Análise somente de leitura: nada foi alterado.)");

        foreach (var warning in Items(report, "warnings")) w.WriteLine($"! {Str(warning, "message")}");

        w.WriteLine();
        w.WriteLine("Stack detectada:");
        var stacks = Items(report, "stacks").ToList();
        if (stacks.Count == 0) w.WriteLine("  nenhuma stack conhecida detectada pelos arquivos de configuração");
        foreach (var s in stacks)
        {
            w.WriteLine($"  {Str(s, "name")} {Str(s, "version")} em '{Str(s, "root")}' (confiança {Str(s, "confidence")})");
            foreach (var e in Items(s, "evidence")) w.WriteLine($"    evidência: {Str(e, "path")}{(Str(e, "detail") is { Length: > 0 } d ? $" — {d}" : "")}");
        }

        foreach (var t in Items(report, "tooling"))
            w.WriteLine($"  {Str(t, "kind")}: {(Str(t, "command") is { Length: > 0 } command ? command : Str(t, "name"))}");

        w.WriteLine();
        w.WriteLine("Ambiente de IA:");
        var envs = Items(report, "aiEnvironments").ToList();
        if (envs.Count == 0) w.WriteLine("  nenhum detectado");
        foreach (var e in envs)
            w.WriteLine($"  {Str(e, "id")} ({(e?["supported"]?.GetValue<bool>() == true ? "suportado" : "sem suporte de configuração nesta fase")}, confiança {Str(e, "confidence")})");

        var specification = report["specification"];
        w.WriteLine();
        w.WriteLine($"Estrutura de especificação: {Str(specification, "state")}{(Str(specification, "version") is { Length: > 0 } v ? $" (versão {v})" : "")}");
        foreach (var p in Items(specification, "problems")) w.WriteLine($"  problema: {p?.GetValue<string>()}");

        var inventory = report["inventory"];
        w.WriteLine();
        w.WriteLine("Harness existente: " + string.Join(", ", new[] { "agents", "skills", "hooks", "rules", "mcpServers", "evals" }
            .Select(k => $"{Items(inventory, k).Count()} {k}")));
    }

    public static void Plan(TextWriter w, ConfigurationPlan plan, bool showDiff)
    {
        w.WriteLine($"Plano {plan.PlanId} — ambiente {(plan.AiEnvironment.Length == 0 ? "(não definido)" : plan.AiEnvironment)}, modelo de implementação {plan.ImplementationModel}");
        w.WriteLine(plan.ProposalOrigin == "harness-engineer"
            ? "Proposta do Harness Engineer, validada pelo motor."
            : "Plano pelas regras-base (o Harness Engineer ainda não gerou .mariliz/proposal.json).");

        w.WriteLine();
        w.WriteLine("Decisões:");
        foreach (var d in plan.Decisions)
            w.WriteLine($"  [{d.Decision}] {d.ComponentId} — {d.Justification} (origem: {d.Origin})");

        foreach (var l in plan.Limitations) w.WriteLine($"! {l.Message}");

        w.WriteLine();
        w.WriteLine("Arquivos:");
        foreach (var c in plan.Changes)
        {
            w.WriteLine($"  {c.Action.ToString().ToUpperInvariant(),-9} {c.Path}{(c.Destructive ? "  [destrutivo]" : "")}");
            if (c.Action == ChangeAction.Conflict && c.ConflictReason is not null) w.WriteLine($"            {c.ConflictReason}");
            if (showDiff && c.Diff is not null) foreach (var line in c.Diff.Split('\n')) w.WriteLine($"      {line}");
        }

        if (plan.Commands.Count > 0)
        {
            w.WriteLine();
            w.WriteLine("Comandos (só executam se você aprovar o id):");
            foreach (var cmd in plan.Commands)
            {
                w.WriteLine($"  {cmd.Id}: {cmd.Executable} {string.Join(' ', cmd.Arguments)}");
                w.WriteLine($"    efeitos: {string.Join(", ", cmd.Effects.Select(e => e.ToString()))}{(cmd.Destructive ? "  [destrutivo: sobrescreve edições locais]" : "")}");
            }
        }

        foreach (var o in plan.Observations) w.WriteLine($"  observação ({o.Kind}): {o.Text}");
        foreach (var r in plan.Risks) w.WriteLine($"  risco: {r}");
    }

    public static void Approvals(TextWriter w, ConfigurationPlan plan, ApplyApprovals approvals)
    {
        var byId = plan.Changes.ToDictionary(c => c.Id);
        w.WriteLine();
        w.WriteLine("Você está aprovando:");
        foreach (var id in approvals.ChangeIds) w.WriteLine($"  {byId[id].Action.ToString().ToLowerInvariant()} {byId[id].Path}");
        foreach (var id in approvals.CommandIds) w.WriteLine($"  executar o comando {id}");
        if (approvals.ChangeIds.Count + approvals.CommandIds.Count == 0) w.WriteLine("  nada");

        foreach (var (id, resolution) in approvals.Resolutions)
            w.WriteLine($"  conflito {byId[id].Path}: {(resolution == ConflictResolution.Keep ? "manter o seu arquivo" : "substituir (o seu fica em backup)")}");

        var skipped = plan.Changes.Where(c => c.Action == ChangeAction.Remove && !approvals.ChangeIds.Contains(c.Id)).ToList();
        foreach (var c in skipped) w.WriteLine($"  remoção NÃO aprovada (use --approve-removals): {c.Path}");
        foreach (var c in plan.Commands.Where(c => !approvals.CommandIds.Contains(c.Id)))
            w.WriteLine($"  comando NÃO aprovado (use --approve-command {c.Id}): {c.Executable} {string.Join(' ', c.Arguments)}");
        w.WriteLine();
    }

    public static void Result(TextWriter w, JsonObject result)
    {
        var outcome = Str(result, "outcome");
        var kind = Str(result, "kind");
        w.WriteLine();
        w.WriteLine($"Resultado ({kind}): {Describe(outcome)}");

        if (result["error"] is JsonObject error) w.WriteLine($"  erro {Str(error, "code")}: {Str(error, "message")}");
        if (result["rolledBack"]?.GetValue<bool>() == true) w.WriteLine("  as alterações já feitas foram desfeitas");

        if (result["files"] is JsonObject files)
            foreach (var (label, key) in new[] { ("criados", "created"), ("atualizados", "updated"), ("removidos", "removed"), ("mantidos (seus)", "kept"), ("não aplicados", "notApplied") })
            {
                var list = Items(files, key).Select(n => n?.GetValue<string>()).ToList();
                if (list.Count > 0) w.WriteLine($"  {label}: {string.Join(", ", list)}");
            }

        foreach (var p in Items(result, "pending")) w.WriteLine($"  pendência: {p?.GetValue<string>()}");

        if (result["reverted"] is JsonArray reverted) w.WriteLine($"  restaurados: {reverted.Count}{(result["verified"]?.GetValue<bool>() == true ? " (conferidos)" : " (não conferidos)")}");
        foreach (var c in Items(result, "conflicts")) w.WriteLine($"  mantido como está (editado depois): {c?.GetValue<string>()}");

        var results = Items(result["validation"], "results").ToList();
        if (results.Count > 0)
        {
            w.WriteLine();
            w.WriteLine("Evals:");
            foreach (var e in results)
            {
                w.WriteLine($"  {Str(e, "status"),-8} {Str(e, "id")} {Str(e, "name")} ({(e?["required"]?.GetValue<bool>() == true ? "obrigatório" : "opcional")})");
                if (Str(e, "reason") is { Length: > 0 } reason) w.WriteLine($"           {reason}");
            }
        }

        foreach (var l in Items(result, "limitations")) w.WriteLine($"! {Str(l, "message")}");
        foreach (var r in Items(result, "risks")) w.WriteLine($"  risco remanescente: {r?.GetValue<string>()}");
    }

    private static string Describe(string outcome) => outcome switch
    {
        RunStates.Completed => "concluído; todas as verificações obrigatórias passaram",
        RunStates.CompletedWithLimitations => "concluído com limitações; há itens não verificados ou que dependem de você",
        RunStates.Cancelled => "cancelado",
        RunStates.RequiresIntervention => "requer intervenção; alguns arquivos não puderam ser desfeitos sozinhos",
        RunStates.Failed => "falhou",
        _ => outcome
    };
}
