using Mariliz.Core.Plan;
using System.Text.Json.Nodes;

namespace Mariliz.Application.Execution;

/// <summary>
/// As etapas de uma aplicação, na ordem em que <see cref="ApplyExecution"/> as executa. Um só lugar para o id e o título:
/// o que <c>run.plan</c> anuncia é o que <c>step.started</c> depois publica.
/// </summary>
internal static class ApplySteps
{
    public static readonly (string Id, string Title) Precheck = ("precheck", "Verificar pré-condições");
    public static readonly (string Id, string Title) Backup = ("backup", "Guardar cópia do que será alterado");
    public static readonly (string Id, string Title) Write = ("write", "Aplicar as alterações aprovadas");
    public static readonly (string Id, string Title) Lock = ("lock", "Registrar os arquivos gerenciados");
    public static readonly (string Id, string Title) Validate = ("validate", "Validar a configuração aplicada");

    public static (string Id, string Title) Command(PlannedCommand command) => ("command:" + command.Id, command.Description);

    /// <summary>A lista ordenada de <c>run.plan</c>: uma etapa por comando aprovado, entre a cópia e a escrita.</summary>
    public static JsonObject Plan(IEnumerable<PlannedCommand> approvedCommands)
    {
        var steps = new List<(string Id, string Title)> { Precheck, Backup };
        steps.AddRange(approvedCommands.Select(Command));
        steps.AddRange([Write, Lock, Validate]);

        return new JsonObject
        {
            ["steps"] = new JsonArray(steps.Select(s => (JsonNode)new JsonObject { ["id"] = s.Id, ["title"] = s.Title }).ToArray())
        };
    }
}
