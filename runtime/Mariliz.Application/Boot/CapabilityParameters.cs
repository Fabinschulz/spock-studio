using Mariliz.Application.Management;

namespace Mariliz.Application.Boot;

public enum ParameterUnit
{
    Weeks,
    Count
}

/// <param name="Name">O nome do parâmetro no método do motor ("weeks", "limit").</param>
/// <param name="Nouns">Substantivos que, depois de um número, dizem que ele é este parâmetro (só para contagem).</param>
public sealed record CapabilityParameter(string Name, string Label, ParameterUnit Unit, int Min, int Max, IReadOnlyList<string> Nouns);

/// <param name="Requested">O que a pessoa escreveu; difere de <paramref name="Value"/> quando passou da faixa que o motor aceita.</param>
public sealed record ExtractedParameter(CapabilityParameter Parameter, int Value, int Requested)
{
    public bool Clamped => Value != Requested;

    public string Note => Clamped
        ? $"{Parameter.Label}: pedido {Requested}, o motor aceita de {Parameter.Min} a {Parameter.Max}; usei {Value}."
        : $"{Parameter.Label}: {Value}.";
}

/// <summary>
/// Os parâmetros que o Boot sabe tirar do texto, por capacidade, com a faixa que o motor aceita. É a lista que vale: o que o
/// registro do Boot guarda é conferido contra ela de novo ao executar, então um argumento que não está aqui nunca chega ao motor.
/// </summary>
public static class CapabilityParameters
{
    private static readonly string[] Events = ["evento", "eventos", "registro", "registros", "entrada", "entradas"];

    private static CapabilityParameter Weeks(string label = "Período em semanas") =>
        new("weeks", label, ParameterUnit.Weeks, ManagementService.MinWeeks, ManagementService.MaxWeeks, []);

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<CapabilityParameter>> ByCapability =
        new Dictionary<string, IReadOnlyList<CapabilityParameter>>(StringComparer.Ordinal)
        {
            ["management.tracking"] = [Weeks()],
            ["management.report"] = [Weeks()],
            ["platform.timeline"] = [new("limit", "Quantidade de eventos", ParameterUnit.Count, 1, 200, Events)],
            ["platform.audit"] = [new("limit", "Quantidade de registros", ParameterUnit.Count, 1, 200, Events)],
            ["platform.runs"] = [new("limit", "Quantidade de execuções", ParameterUnit.Count, 1, 100, ["execucao", "execucoes"])]
        };

    public static IReadOnlyList<CapabilityParameter> For(string capabilityId) =>
        ByCapability.TryGetValue(capabilityId, out var specs) ? specs : [];

    public static IReadOnlyList<ExtractedParameter> Extract(string capabilityId, string? text)
    {
        var tokens = IntentRecognizer.Tokens(text);
        var found = new List<ExtractedParameter>();
        foreach (var spec in For(capabilityId))
            if (Read(spec, tokens) is { } requested) found.Add(new ExtractedParameter(spec, Math.Clamp(requested, spec.Min, spec.Max), requested));
        return found;
    }

    // Um valor de um parâmetro que veio de um registro gravado: só o que a ficha declara, e sempre dentro da faixa.
    public static int? Bound(string capabilityId, string name, int value) =>
        For(capabilityId).FirstOrDefault(spec => spec.Name == name) is { } spec ? Math.Clamp(value, spec.Min, spec.Max) : null;

    private static int? Read(CapabilityParameter spec, IReadOnlyList<string> tokens) =>
        spec.Unit == ParameterUnit.Weeks ? ReadWeeks(tokens) : ReadCount(spec, tokens);

    private static int? ReadWeeks(IReadOnlyList<string> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (!int.TryParse(tokens[i], out var number) || number <= 0 || i + 1 >= tokens.Count) continue;
            var unit = tokens[i + 1];
            if (unit.StartsWith("semana", StringComparison.Ordinal)) return number;
            if (unit.StartsWith("dia", StringComparison.Ordinal)) return (number + 6) / 7;
            if (unit is "mes" or "meses") return number * 4;
            if (unit is "trimestre" or "trimestres") return number * 13;
            if (unit is "semestre" or "semestres") return number * 26;
        }
        // Sem número: as unidades que dizem um período fechado.
        if (tokens.Contains("trimestre")) return 13;
        if (tokens.Contains("semestre")) return 26;
        if (tokens.Contains("mes")) return 4;
        return null;
    }

    private static int? ReadCount(CapabilityParameter spec, IReadOnlyList<string> tokens)
    {
        for (var i = 0; i + 1 < tokens.Count; i++)
            if (int.TryParse(tokens[i], out var number) && number > 0 && spec.Nouns.Contains(tokens[i + 1], StringComparer.Ordinal)) return number;
        return null;
    }
}
