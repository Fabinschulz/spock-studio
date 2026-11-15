using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.Runtime.Credentials;
using Mariliz.Application.Cloud;

namespace Mariliz.Infrastructure.Cloud.Aws;

// Resolve pelo caminho oficial do SDK e nunca abre login: SSO vencido vira "credencial expirada" para a pessoa renovar
// no terminal, em vez de um navegador surgindo por conta de um agente.
internal static class AwsCredentials
{
    public const string SessionName = "spock-studio-read";

    public static AWSCredentials Resolve(CloudSettings settings) => settings.Mechanism switch
    {
        AwsCloudProvider.ProfileMechanism => FromProfile(settings.Value(AwsCloudProvider.ProfileField)!),
        AwsCloudProvider.DefaultMechanism => FromDefaultChain(),
        AwsCloudProvider.AssumeRoleMechanism => new AssumeRoleAWSCredentials(
            settings.Value(AwsCloudProvider.SourceProfileField) is { } source ? FromProfile(source) : FromDefaultChain(),
            settings.Value(AwsCloudProvider.RoleArnField)!,
            SessionName,
            new AssumeRoleAWSCredentialsOptions { DurationSeconds = 900 }),
        _ => throw CloudErrors.MechanismUnknown()
    };

    private static AWSCredentials FromProfile(string profile)
    {
        var chain = new CredentialProfileStoreChain();
        if (!chain.TryGetProfile(profile, out _))
            throw CloudErrors.NotAuthenticated($"O perfil '{profile}' não existe em ~/.aws/config nem em ~/.aws/credentials.");
        try
        {
            if (!chain.TryGetAWSCredentials(profile, out var credentials)) throw Unsupported(profile);
            return NoInteractiveLogin(credentials);
        }
        // O SDK recusa assim os tipos de perfil que não conhece, como o login_session do aws login.
        catch (InvalidOperationException)
        {
            throw Unsupported(profile);
        }
    }

    private static Application.Execution.EngineException Unsupported(string profile) =>
        CloudErrors.NotAuthenticated($"O perfil '{profile}' usa um tipo de credencial que o AWS SDK desta versão não sabe usar (por exemplo, aws login). Use um perfil de SSO ou de chaves, ou a cadeia padrão.");

    private static AWSCredentials FromDefaultChain()
    {
        try
        {
            return NoInteractiveLogin(DefaultAWSCredentialsIdentityResolver.GetCredentials());
        }
        catch (AmazonClientException)
        {
            throw CloudErrors.NotAuthenticated("Configure credenciais com aws configure, aws sso login ou variáveis de ambiente, e valide de novo.");
        }
    }

    private static AWSCredentials NoInteractiveLogin(AWSCredentials credentials)
    {
        if (credentials is SSOAWSCredentials sso) sso.Options.SupportsGettingNewToken = false;
        return credentials;
    }
}
