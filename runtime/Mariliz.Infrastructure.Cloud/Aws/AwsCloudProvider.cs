using System.Text.RegularExpressions;
using Amazon;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Mariliz.Application.Cloud;

namespace Mariliz.Infrastructure.Cloud.Aws;

// A credencial é sempre a que o AWS SDK acha sozinho (perfil, cache do SSO, variáveis, papel da máquina): o Spock guarda
// só o nome do perfil, a região e o Role ARN.
public sealed partial class AwsCloudProvider : ICloudProvider, ICloudPriceSource, ICloudProcessCredentials
{
    public const string IacSessionName = "spock-studio-iac";

    // Um apply passa fácil dos 15 minutos das leituras. Uma hora é o máximo padrão de um papel e o teto quando o perfil de
    // origem já é um papel assumido (encadeamento).
    public const int IacSessionSeconds = 3600;

    public const string ProfileMechanism = "profile";
    public const string DefaultMechanism = "default";
    public const string AssumeRoleMechanism = "assume-role";
    public const string ProfileField = "profile";
    public const string RoleArnField = "roleArn";
    public const string SourceProfileField = "sourceProfile";

    private static readonly CloudField Region = new(CloudFields.Region, "Região padrão", "Por exemplo sa-east-1. As leituras por região usam esta quando o pedido não diz outra.", Required: true);

    public CloudProviderDescriptor Descriptor { get; } = new(
        CloudProviders.Aws,
        "Amazon Web Services",
        "1.0.0",
        [
            new CloudMechanism(ProfileMechanism, "Perfil do AWS CLI",
                "Um perfil de ~/.aws/config: chaves, IAM Identity Center (SSO), aws login, processo externo ou papel assumido. Antes, rode aws sso login --profile <perfil> (SSO) ou aws login --profile <perfil>.",
                [new CloudField(ProfileField, "Perfil", "Nome do perfil em ~/.aws/config, por exemplo dev.", Required: true), Region]),
            new CloudMechanism(DefaultMechanism, "Cadeia padrão do SDK",
                "Variáveis de ambiente, AWS_PROFILE, perfil default, SSO, web identity ou o papel da máquina, na ordem oficial do AWS SDK.",
                [Region]),
            new CloudMechanism(AssumeRoleMechanism, "Assumir um papel (AssumeRole)",
                "Credenciais temporárias de um papel IAM (15 minutos nas leituras, uma hora num plano ou apply da IaC), a partir de um perfil ou da cadeia padrão.",
                [
                    new CloudField(RoleArnField, "Role ARN", "arn:aws:iam::<conta>:role/<nome>.", Required: true),
                    new CloudField(SourceProfileField, "Perfil de origem", "Opcional. Sem ele, vale a cadeia padrão.", Required: false),
                    Region
                ])
        ],
        AwsReaders.Capabilities,
        "Conta e ARN vêm do STS GetCallerIdentity, que não exige permissão.");

    public CloudSettings Normalize(CloudSettings settings)
    {
        var values = new Dictionary<string, string>(settings.Values, StringComparer.Ordinal);
        values[CloudFields.Region] = NormalizeRegion(settings.Region ?? "");
        foreach (var field in new[] { ProfileField, SourceProfileField })
        {
            if (values.TryGetValue(field, out var profile) && !ProfilePattern().IsMatch(profile))
                throw CloudErrors.SettingsInvalid("Nome de perfil com letras, números e . _ - @ +, até 64 caracteres.");
        }
        if (values.TryGetValue(RoleArnField, out var arn) && !RoleArnPattern().IsMatch(arn))
            throw CloudErrors.SettingsInvalid("O Role ARN precisa ter o formato arn:aws:iam::<conta de 12 dígitos>:role/<nome>.");
        return settings with { Values = values };
    }

    // Só regiões que o SDK conhece: uma inventada vira endpoint que não existe e um erro de rede confuso.
    public string NormalizeRegion(string region)
    {
        var clean = region.Trim().ToLowerInvariant();
        if (!RegionPattern().IsMatch(clean) || RegionEndpoint.EnumerableAllRegions.All(r => r.SystemName != clean))
            throw CloudErrors.RegionInvalid($"'{region.Trim()}' não é uma região da AWS conhecida por esta versão do SDK.");
        return clean;
    }

    public async Task<CloudIdentity> IdentifyAsync(CloudSettings settings, CancellationToken ct)
    {
        try
        {
            var credentials = AwsCredentials.Resolve(settings);
            using var sts = new AmazonSecurityTokenServiceClient(credentials, AwsClients.Config<AmazonSecurityTokenServiceConfig>(settings.Region!));
            var caller = await sts.GetCallerIdentityAsync(new GetCallerIdentityRequest(), ct);
            return new CloudIdentity(caller.Account ?? throw CloudErrors.InvalidResponse(), null, caller.Arn ?? "", null);
        }
        catch (Exception ex) when (AwsErrors.Map(ex, settings, ct) is { } mapped)
        {
            throw mapped;
        }
    }

    public async Task<CloudTable> ReadAsync(CloudSettings settings, CloudQuery query, CancellationToken ct)
    {
        try
        {
            var credentials = AwsCredentials.Resolve(settings);
            return await AwsReaders.ReadAsync(credentials, query.Region ?? settings.Region!, query, ct);
        }
        catch (Exception ex) when (AwsErrors.Map(ex, settings, ct) is { } mapped)
        {
            throw mapped;
        }
    }

    public async Task<IReadOnlyList<CloudPriceQuote>> PricesAsync(CloudSettings settings, IReadOnlyList<CloudPriceQuery> queries, CancellationToken ct)
    {
        try
        {
            return await AwsPrices.QuoteAsync(AwsCredentials.Resolve(settings), queries, ct);
        }
        catch (Exception ex) when (AwsErrors.Map(ex, settings, ct) is { } mapped)
        {
            throw mapped;
        }
    }

    // O OpenTofu/Terraform só conhece o perfil de origem: com ele, rodaria com a identidade de origem, não com a do papel.
    public async Task<IReadOnlyDictionary<string, string>> ProcessEnvironmentAsync(CloudSettings settings, CancellationToken ct)
    {
        if (settings.Mechanism != AssumeRoleMechanism) return new Dictionary<string, string>();
        try
        {
            var role = await AwsCredentials.AssumeRole(settings, IacSessionName, IacSessionSeconds).GetCredentialsAsync();
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["AWS_ACCESS_KEY_ID"] = role.AccessKey,
                ["AWS_SECRET_ACCESS_KEY"] = role.SecretKey,
                ["AWS_SESSION_TOKEN"] = role.Token
            };
        }
        catch (Exception ex) when (AwsErrors.Map(ex, settings, ct) is { } mapped)
        {
            throw mapped;
        }
    }

    [GeneratedRegex(@"^[a-z]{2}(-[a-z]+)+-\d{1,2}$")]
    private static partial Regex RegionPattern();

    [GeneratedRegex(@"^[A-Za-z0-9_.@+\-]{1,64}$")]
    private static partial Regex ProfilePattern();

    [GeneratedRegex(@"^arn:aws[a-z\-]*:iam::\d{12}:role/[A-Za-z0-9+=,.@_\-/]{1,512}$")]
    private static partial Regex RoleArnPattern();
}
