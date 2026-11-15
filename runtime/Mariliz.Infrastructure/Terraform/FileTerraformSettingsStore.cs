using System.Text.Json;
using Mariliz.Application.Iac;
using Mariliz.Application.Integrations;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure.Terraform;

public sealed class FileTerraformSettingsStore(string dataRoot) : ITerraformSettingsStore
{
    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "iac", "terraform.json");

    public TerraformSettings Read()
    {
        lock (_gate)
        {
            var settings = AtomicFile.ReadJson<TerraformSettings>(FilePath, IntegrationJson.Options, s => s is { Adopted: not null, Pins: not null });
            return settings is null ? TerraformSettings.Empty : settings with { Pins = new Dictionary<string, ProjectTerraformPin>(settings.Pins, StringComparer.Ordinal) };
        }
    }

    public void Write(TerraformSettings settings)
    {
        lock (_gate) AtomicFile.Write(FilePath, JsonSerializer.Serialize(settings, IntegrationJson.Options));
    }
}
