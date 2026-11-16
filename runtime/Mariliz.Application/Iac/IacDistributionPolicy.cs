namespace Mariliz.Application.Iac;

// O que cada ferramenta de IaC pode fazer nesta edição do Spock. A edição vem do build (propriedade SpockEdition do
// Mariliz.Runtime) e não de configuração do usuário: só quem gera o instalador decide. A edição comercial não instala o
// Terraform enquanto a BUSL 1.1 aguarda parecer (docs/compliance/LICENSE_DECISIONS.md, D-02).
public sealed record IacEnginePolicy(bool ManagedInstall, bool ExternalUse, string? Note);

public sealed class IacDistributionPolicy
{
    public const string Development = "development";
    public const string Commercial = "commercial";

    private const string TerraformCommercialNote =
        "A instalação do Terraform pelo Spock está desativada nesta edição enquanto a licença BUSL 1.1 está em análise. " +
        "Um Terraform que você já tenha instalado pode ser usado, sob a licença da HashiCorp.";

    private static readonly IacEnginePolicy Allowed = new(true, true, null);

    private readonly IReadOnlyDictionary<IacEngine, IacEnginePolicy> _engines;

    public IacDistributionPolicy(string edition, IReadOnlyDictionary<IacEngine, IacEnginePolicy> engines)
    {
        Edition = edition;
        _engines = engines;
    }

    public string Edition { get; }

    public static IacDistributionPolicy Unrestricted { get; } = new(Development, new Dictionary<IacEngine, IacEnginePolicy>());

    public static IacDistributionPolicy CommercialDefault { get; } = new(Commercial, new Dictionary<IacEngine, IacEnginePolicy>
    {
        [IacEngine.Terraform] = new(ManagedInstall: false, ExternalUse: true, TerraformCommercialNote)
    });

    // Edição desconhecida é erro de build, não motivo para liberar tudo nem para travar em silêncio.
    public static IacDistributionPolicy ForEdition(string? edition) => edition?.Trim().ToLowerInvariant() switch
    {
        null or "" or Development => Unrestricted,
        Commercial => CommercialDefault,
        _ => throw new ArgumentException($"Edição do Spock desconhecida: '{edition}'. Use '{Development}' ou '{Commercial}'.", nameof(edition))
    };

    public IacEnginePolicy For(IacEngine engine) => _engines.TryGetValue(engine, out var policy) ? policy : Allowed;
}
