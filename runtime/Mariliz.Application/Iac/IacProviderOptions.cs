using Mariliz.Application.Cloud;

namespace Mariliz.Application.Iac;

/// <summary>
/// O que a tela de geração oferece em cada nuvem. As checagens do desenho leem daqui os motores de banco e a faixa de
/// armazenamento, e um teste confere que todo tamanho e padrão oferecido passa nelas: a tela não sugere o que o motor
/// recusa.
/// </summary>
/// <param name="ArchitectDraft">O motor propõe o desenho com a IA configurada (o prompt conhece só os componentes desta nuvem).</param>
/// <param name="CostEstimate">O motor estima o custo pelos preços públicos da nuvem, por uma conexão do projeto.</param>
public sealed record IacProviderOptions(
    string Id,
    string Label,
    string RegionExample,
    IacServiceOptions Service,
    IacDatabaseOptions Database,
    bool ArchitectDraft,
    bool CostEstimate);

/// <param name="Sizes">Os tamanhos oferecidos, o primeiro é o padrão; o motor aceita também as demais combinações válidas.</param>
/// <param name="Certificate">O HTTPS usa um certificado informado pela pessoa (na AWS); nas outras, o domínio gerenciado.</param>
/// <param name="PublicIp">As tarefas podem rodar com IP público em subnets públicas, sem NAT.</param>
public sealed record IacServiceOptions(IReadOnlyList<IacServiceSize> Sizes, bool Certificate, bool PublicIp);

public sealed record IacServiceSize(int Cpu, int Memory);

/// <param name="InstanceClass">O padrão do formulário, que também serve de exemplo.</param>
public sealed record IacDatabaseOptions(IReadOnlyList<string> Engines, string InstanceClass, IacStorageOptions Storage);

/// <param name="AllowedGb">Vazio: qualquer valor de MinGb a MaxGb. Senão, só estes.</param>
public sealed record IacStorageOptions(int MinGb, int MaxGb, IReadOnlyList<int> AllowedGb, int DefaultGb)
{
    public bool Accepts(int gb) => AllowedGb.Count > 0 ? AllowedGb.Contains(gb) : gb >= MinGb && gb <= MaxGb;
}

public sealed record IacGenerateOptions(IReadOnlyList<IacProviderOptions> Providers)
{
    public static IacGenerateOptions Current => new(IacProviders.All.Select(provider => provider.Options).ToList());
}

internal static class IacProviderOptionsCatalog
{
    private const string Postgres = "postgres";
    private const string MySql = "mysql";

    public static readonly IacProviderOptions Aws = new(CloudProviders.Aws, "AWS", "sa-east-1",
        new([new(256, 512), new(512, 1024), new(1024, 2048), new(2048, 4096)], Certificate: true, PublicIp: true),
        new([Postgres, MySql], "db.t4g.micro", new(20, 1000, [], 20)),
        ArchitectDraft: true, CostEstimate: true);

    public static readonly IacProviderOptions Azure = new(CloudProviders.Azure, "Azure", "brazilsouth",
        new([new(256, 512), new(512, 1024), new(1024, 2048), new(2048, 4096)], Certificate: false, PublicIp: false),
        new([Postgres], "B_Standard_B1ms", new(32, 1024, [32, 64, 128, 256, 512, 1024], 32)),
        ArchitectDraft: false, CostEstimate: false);

    public static readonly IacProviderOptions Gcp = new(CloudProviders.Gcp, "Google Cloud", "southamerica-east1",
        new([new(1024, 512), new(1024, 2048), new(2048, 4096), new(4096, 8192)], Certificate: false, PublicIp: false),
        new([Postgres, MySql], "db-f1-micro", new(10, 1000, [], 10)),
        ArchitectDraft: false, CostEstimate: false);
}
