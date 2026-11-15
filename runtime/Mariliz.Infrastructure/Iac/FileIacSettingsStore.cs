using System.Text.Json;
using Mariliz.Application.Iac;
using Mariliz.Application.Integrations;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure.Iac;

public sealed class FileIacSettingsStore(string dataRoot) : IIacSettingsStore
{
    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "iac", "terraform.json");

    public IacRuntimeSettings Read()
    {
        lock (_gate)
        {
            var settings = AtomicFile.ReadJson<IacRuntimeSettings>(FilePath, IntegrationJson.Options, s => s is { Adopted: not null, Pins: not null });
            return settings is null ? IacRuntimeSettings.Empty : settings with { Pins = new Dictionary<string, ProjectRuntimePin>(settings.Pins, StringComparer.Ordinal) };
        }
    }

    public void Write(IacRuntimeSettings settings)
    {
        lock (_gate) AtomicFile.Write(FilePath, JsonSerializer.Serialize(settings, IntegrationJson.Options));
    }
}
