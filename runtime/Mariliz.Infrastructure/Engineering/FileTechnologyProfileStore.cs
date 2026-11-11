using System.Text;
using System.Text.Json;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Core.Text;

namespace Mariliz.Infrastructure.Engineering;

/// <summary>
/// Um arquivo por projeto em <c>{dataRoot}/profiles/{projectId}.json</c>. O id vira nome de arquivo, então só passa o
/// que tem a forma gerada pelo motor. Arquivo ilegível conta como perfil ainda não criado.
/// </summary>
public sealed class FileTechnologyProfileStore(string dataRoot) : ITechnologyProfileStore
{
    private readonly object _gate = new();

    public TechnologyProfile? Read(string projectId)
    {
        var path = PathFor(projectId);
        lock (_gate)
        {
            if (!File.Exists(path)) return null;
            try
            {
                return JsonSerializer.Deserialize<TechnologyProfile>(File.ReadAllText(path), IntegrationJson.Options);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                return null;
            }
        }
    }

    public void Write(TechnologyProfile profile)
    {
        var path = PathFor(profile.ProjectId);
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(profile, IntegrationJson.Options), new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
        }
    }

    private string PathFor(string projectId) =>
        Ids.IsProjectId(projectId)
            ? Path.Combine(dataRoot, "profiles", projectId + ".json")
            : throw new EngineException("INVALID_PARAMS", "Identificador fora do formato esperado.");
}
