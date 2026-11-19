using Mariliz.Application.Cloud;
using Mariliz.Infrastructure.Cloud.Aws;

namespace Mariliz.Application.Tests;

// Um perfil do AWS CLI de verdade pelo caminho da conexão: SSO, chaves ou aws login (login_session, que depende do
// AWSSDK.Signin). Só chama o STS GetCallerIdentity, que não exige permissão. Rode com MARILIZ_LIVE_AWS_PROFILE=<perfil>.
public sealed class AwsProfileLiveTests(ITestOutputHelper output)
{
    private const string Variable = "MARILIZ_LIVE_AWS_PROFILE";

    [LiveFact(Variable)]
    public async Task Perfil_do_aws_cli_identifica_a_conta_pela_conexao()
    {
        var provider = new AwsCloudProvider();
        var settings = provider.Normalize(new CloudSettings(AwsCloudProvider.ProfileMechanism, new Dictionary<string, string>
        {
            [AwsCloudProvider.ProfileField] = Environment.GetEnvironmentVariable(Variable)!,
            [CloudFields.Region] = "sa-east-1"
        }));

        var identity = await provider.IdentifyAsync(settings, CancellationToken.None);

        output.WriteLine($"Identidade: {identity.Principal.Split(':').Last()}");
        Assert.Matches("^[0-9]{12}$", identity.Account);
        Assert.StartsWith("arn:aws", identity.Principal);
    }
}
