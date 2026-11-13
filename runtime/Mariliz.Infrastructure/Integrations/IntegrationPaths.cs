using Mariliz.Application.Execution;
using Mariliz.Core.Text;

namespace Mariliz.Infrastructure.Integrations;

// Os identificadores viram nome de pasta e de arquivo, então só passam os que têm a forma gerada pelo motor: um id forjado não consegue apontar para fora da pasta.
internal static class IntegrationPaths
{
    public static string Root(string dataRoot) => Path.Combine(dataRoot, "integrations");

    public static string Secret(string dataRoot, string connectionId) =>
        Path.Combine(Root(dataRoot), "secrets", Checked(connectionId, "conn-") + ".bin");

    public static string SyncedData(string dataRoot, string projectId, string integrationId) =>
        Path.Combine(Root(dataRoot), "data", Checked(projectId, "p-"), Checked(integrationId, "pint-") + ".json");

    private static string Checked(string id, string prefix) =>
        Ids.Is(id, prefix) ? id : throw new EngineException("INVALID_PARAMS", "Identificador fora do formato esperado.");
}
