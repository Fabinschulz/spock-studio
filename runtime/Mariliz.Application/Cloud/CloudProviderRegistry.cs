using Mariliz.Application.Execution;

namespace Mariliz.Application.Cloud;

// É a única lista: catálogo, validação de provedor e capacidade saem daqui. As regras comuns a todo provedor são
// conferidas ao registrar, para um descritor malformado falhar ao abrir o motor e não no meio de uma leitura.
public sealed class CloudProviderRegistry
{
    public const int MaxFieldValue = 200;
    public const int MaxLimit = 200;
    public const int DefaultLimit = 50;

    private readonly Dictionary<string, ICloudProvider> _byProvider;

    public CloudProviderRegistry(IEnumerable<ICloudProvider> providers)
    {
        _byProvider = providers.ToDictionary(p => p.Descriptor.Provider, StringComparer.Ordinal);
        foreach (var provider in _byProvider.Values) Check(provider.Descriptor);
    }

    public IReadOnlyList<CloudProviderDescriptor> Catalog() =>
        _byProvider.Values.Select(p => p.Descriptor).OrderBy(d => d.Name, StringComparer.Ordinal).ToList();

    public ICloudProvider Require(string provider) =>
        _byProvider.TryGetValue(provider, out var found) ? found : throw CloudErrors.ProviderUnknown();

    public CloudCapability RequireCapability(string provider, string capability) =>
        Require(provider).Descriptor.Capability(capability) ?? throw CloudErrors.CapabilityUnknown();

    // Regras comuns a qualquer mecanismo; o formato de cada campo (região, ARN, GUID) é do provedor.
    public CloudSettings Normalize(string provider, string mechanism, IReadOnlyDictionary<string, string> values)
    {
        var target = Require(provider);
        var described = target.Descriptor.Mechanism(mechanism) ?? throw CloudErrors.MechanismUnknown();
        var known = described.Fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        if (values.Keys.FirstOrDefault(key => !known.Contains(key)) is { } unknown)
            throw CloudErrors.SettingsInvalid($"'{unknown}' não é um campo de {described.Label}.");

        var clean = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in described.Fields)
        {
            var value = values.TryGetValue(field.Name, out var raw) ? raw.Trim() : "";
            if (value.Length > MaxFieldValue || value.Any(char.IsControl))
                throw CloudErrors.SettingsInvalid($"{field.Label}: até {MaxFieldValue} caracteres, numa linha.");
            if (field.Required && value.Length == 0) throw CloudErrors.SettingsInvalid($"{field.Label} é obrigatório.");
            if (value.Length > 0) clean[field.Name] = value;
        }
        return target.Normalize(new CloudSettings(mechanism, clean));
    }

    public static int Limit(int? asked) => asked switch
    {
        null => DefaultLimit,
        < 1 or > MaxLimit => throw CloudErrors.SettingsInvalid($"O limite vai de 1 a {MaxLimit}."),
        _ => asked.Value
    };

    private static void Check(CloudProviderDescriptor descriptor)
    {
        var problems = new List<string>();
        if (descriptor.Mechanisms.Count == 0) problems.Add("sem mecanismo de autenticação");
        if (descriptor.Capabilities.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != descriptor.Capabilities.Count)
            problems.Add("capacidade repetida");
        foreach (var capability in descriptor.Capabilities)
        {
            if (!capability.Id.StartsWith(descriptor.Provider + ".", StringComparison.Ordinal)) problems.Add($"{capability.Id} fora do prefixo");
            if (capability.Impact != CloudImpacts.Read) problems.Add($"{capability.Id} não é leitura");
            if (capability.Permissions.Count == 0) problems.Add($"{capability.Id} sem permissão declarada");
            if (capability.Columns.Count == 0) problems.Add($"{capability.Id} sem colunas");
        }
        if (problems.Count > 0)
            throw new EngineException("CLOUD_PROVIDER_INVALID", $"Descritor de {descriptor.Provider} inválido: {string.Join("; ", problems)}.");
    }
}
