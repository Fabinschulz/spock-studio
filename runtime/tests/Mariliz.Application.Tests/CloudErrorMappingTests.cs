using System.Net;
using Amazon.Runtime;
using Azure;
using Azure.Identity;
using Google;
using Mariliz.Application.Cloud;
using Mariliz.Infrastructure.Cloud.Aws;
using Mariliz.Infrastructure.Cloud.Azure;
using Mariliz.Infrastructure.Cloud.Gcp;

namespace Mariliz.Application.Tests;

// Do erro de cada SDK para o estado que a pessoa vê; a mensagem do provedor nunca passa adiante.
public sealed class CloudErrorMappingTests
{
    private static readonly CloudSettings AwsProfile = new("profile", new Dictionary<string, string> { ["profile"] = "dev", ["region"] = "us-east-1" });
    private static readonly CloudSettings AzureCli = new("azure-cli", new Dictionary<string, string>());

    [Theory]
    [InlineData("ExpiredToken", HttpStatusCode.BadRequest, CloudErrors.ExpiredCode)]
    [InlineData("InvalidClientTokenId", HttpStatusCode.Forbidden, CloudErrors.InvalidIdentityCode)]
    [InlineData("UnauthorizedOperation", HttpStatusCode.Forbidden, CloudErrors.PermissionDeniedCode)]
    [InlineData("AccessDeniedException", HttpStatusCode.BadRequest, CloudErrors.PermissionDeniedCode)]
    [InlineData("OptInRequired", HttpStatusCode.Forbidden, CloudErrors.ServiceDisabledCode)]
    [InlineData("ThrottlingException", HttpStatusCode.BadRequest, CloudErrors.UnavailableCode)]
    [InlineData("Qualquer", HttpStatusCode.Forbidden, CloudErrors.PermissionDeniedCode)]
    public void Aws_por_codigo_de_erro(string code, HttpStatusCode status, string expected)
    {
        var ex = new AmazonServiceException("arn:aws:iam::123456789012:user/alguem não pode", null, ErrorType.Sender, code, "req", status);

        var mapped = AwsErrors.Map(ex, AwsProfile, CancellationToken.None)!;

        Assert.Equal(expected, mapped.Code);
        Assert.DoesNotContain("123456789012", mapped.Message);
    }

    [Fact]
    public void Aws_credencial_que_nao_montou_vira_expirada_ou_nao_autenticada_e_rede_vira_indisponivel()
    {
        Assert.Equal(CloudErrors.ExpiredCode, AwsErrors.Map(new AmazonClientException("The SSO session has expired"), AwsProfile, default)!.Code);
        Assert.Equal(CloudErrors.NotAuthenticatedCode, AwsErrors.Map(new AmazonClientException("Unable to get credentials"), AwsProfile, default)!.Code);
        Assert.Equal(CloudErrors.UnavailableCode,
            AwsErrors.Map(new AmazonClientException("falhou", new HttpRequestException("sem rede")), AwsProfile, default)!.Code);
        Assert.Contains("aws sso login --profile dev", AwsErrors.Map(new AmazonClientException("expired"), AwsProfile, default)!.Message);
    }

    [Fact]
    public void Cancelamento_pedido_e_erro_de_programacao_sobem_como_estao()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.Null(AwsErrors.Map(new OperationCanceledException(), AwsProfile, cancelled.Token));
        Assert.Null(AwsErrors.Map(new NullReferenceException(), AwsProfile, default));
        Assert.Null(AzureErrors.Map(new ArgumentException(), AzureCli, default));
        Assert.Null(GcpErrors.Map(new NullReferenceException(), default));
    }

    [Theory]
    [InlineData(401, "ExpiredAuthenticationToken", CloudErrors.ExpiredCode)]
    [InlineData(401, "InvalidAuthenticationToken", CloudErrors.InvalidIdentityCode)]
    [InlineData(403, "AuthorizationFailed", CloudErrors.PermissionDeniedCode)]
    [InlineData(404, "SubscriptionNotFound", CloudErrors.InvalidIdentityCode)]
    [InlineData(409, "MissingSubscriptionRegistration", CloudErrors.ServiceDisabledCode)]
    [InlineData(503, "ServerBusy", CloudErrors.UnavailableCode)]
    public void Azure_por_status_e_codigo(int status, string code, string expected)
    {
        Assert.Equal(expected, AzureErrors.Map(new RequestFailedException(status, "detalhe do Azure", code, null), AzureCli, default)!.Code);
    }

    [Fact]
    public void Azure_sem_login_e_login_vencido()
    {
        Assert.Equal(CloudErrors.NotAuthenticatedCode, AzureErrors.Map(new CredentialUnavailableException("Azure CLI not installed"), AzureCli, default)!.Code);
        Assert.Equal(CloudErrors.ExpiredCode,
            AzureErrors.Map(new AuthenticationFailedException("AADSTS700082: The refresh token has expired"), AzureCli, default)!.Code);
        Assert.Contains("az login", AzureErrors.Map(new CredentialUnavailableException("x"), AzureCli, default)!.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, CloudErrors.ExpiredCode)]
    [InlineData(HttpStatusCode.Forbidden, CloudErrors.PermissionDeniedCode)]
    [InlineData(HttpStatusCode.NotFound, CloudErrors.InvalidIdentityCode)]
    [InlineData(HttpStatusCode.InternalServerError, CloudErrors.UnavailableCode)]
    public void Google_por_status(HttpStatusCode status, string expected)
    {
        Assert.Equal(expected, GcpErrors.Map(new GoogleApiException("compute", "detalhe do Google") { HttpStatusCode = status }, default)!.Code);
    }

    [Fact]
    public void Google_api_desabilitada_e_sem_credencial()
    {
        var disabled = new GoogleApiException("compute", "x")
        {
            HttpStatusCode = HttpStatusCode.Forbidden,
            Error = new Google.Apis.Requests.RequestError { Errors = [new Google.Apis.Requests.SingleError { Reason = "accessNotConfigured" }] }
        };

        Assert.Equal(CloudErrors.ServiceDisabledCode, GcpErrors.Map(disabled, default)!.Code);
        Assert.Equal(CloudErrors.NotAuthenticatedCode,
            GcpErrors.Map(new InvalidOperationException("Your default credentials were not found."), default)!.Code);
    }
}
