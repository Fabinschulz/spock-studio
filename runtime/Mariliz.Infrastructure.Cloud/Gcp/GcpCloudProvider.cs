using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.CloudResourceManager.v3;
using Google.Apis.CloudResourceManager.v3.Data;
using Google.Apis.Compute.v1;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.ServiceUsage.v1;
using Google.Apis.Storage.v1;
using Mariliz.Application.Cloud;

namespace Mariliz.Infrastructure.Cloud.Gcp;

// Application Default Credentials (gcloud auth application-default login, GOOGLE_APPLICATION_CREDENTIALS, workload
// identity, metadados da máquina) ou uma chave de service account lida do arquivo onde ela já está: o Spock guarda só o
// projeto, a região de filtro e o caminho do arquivo, nunca a chave.
public sealed partial class GcpCloudProvider : ICloudProvider, ICloudPermissionTester, ICloudProcessCredentials
{
    public const string AdcMechanism = "adc";
    public const string KeyFileMechanism = "service-account-file";
    public const string ProjectField = "projectId";
    public const string KeyFileField = "keyFile";
    private const string Scope = "https://www.googleapis.com/auth/cloud-platform";

    private static readonly CloudField Project = new(ProjectField, "Projeto", "Id do projeto do Google Cloud, por exemplo minha-empresa-dev.", Required: true);
    private static readonly CloudField Region = new(CloudFields.Region, "Região (filtro)", "Opcional, por exemplo southamerica-east1. Só filtra as leituras que aceitam região.", Required: false);

    private static CloudColumn C(string id, string label) => new(id, label);

    // O arquivo da chave vai pelo caminho, como o provider google e o SDK já o procuram; o conteúdo nunca passa pelo Spock.
    public CloudProcessNames ProcessNames(CloudSettings settings) => new(CloudProcessVariables.Of(
        ("GOOGLE_CLOUD_PROJECT", settings.Value(ProjectField)),
        ("GOOGLE_PROJECT", settings.Value(ProjectField)),
        ("GOOGLE_REGION", settings.Region),
        ("GOOGLE_APPLICATION_CREDENTIALS", settings.Value(KeyFileField))), settings.Value(ProjectField));

    public Task<IReadOnlyDictionary<string, string>> ProcessEnvironmentAsync(CloudSettings settings, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

    public CloudProviderDescriptor Descriptor { get; } = new(
        CloudProviders.Gcp,
        "Google Cloud",
        "1.0.0",
        [
            new CloudMechanism(AdcMechanism, "Application Default Credentials",
                "A cadeia oficial do Google: GOOGLE_APPLICATION_CREDENTIALS, o login de gcloud auth application-default login, workload identity e os metadados da máquina.",
                [Project, Region]),
            new CloudMechanism(KeyFileMechanism, "Arquivo de chave de service account",
                "Uma chave JSON que já existe no disco. Prefira ADC ou workload identity: chave de longa duração é o mecanismo menos seguro.",
                [new CloudField(KeyFileField, "Arquivo da chave", "Caminho absoluto do .json da service account.", Required: true), Project, Region])
        ],
        [
            new("gcp.projects", "Projetos", "Projetos que a identidade enxerga.", "Resource Manager", ["resourcemanager.projects.get"], CloudRegionUse.None,
                [C("id", "Id"), C("name", "Nome"), C("state", "Estado")]),
            new("gcp.services", "APIs habilitadas", "APIs e serviços habilitados no projeto.", "Service Usage", ["serviceusage.services.list"], CloudRegionUse.None,
                [C("name", "Serviço"), C("title", "Nome")]),
            new("gcp.regions", "Regiões", "Regiões do Compute Engine no projeto.", "Compute Engine", ["compute.regions.list"], CloudRegionUse.None,
                [C("name", "Região"), C("status", "Estado")]),
            new("gcp.compute.instances", "Instâncias do Compute Engine", "VMs do projeto, de todas as zonas ou só das zonas da região pedida.", "Compute Engine",
                ["compute.instances.list"], CloudRegionUse.Filter,
                [C("name", "Nome"), C("zone", "Zona"), C("machineType", "Tipo"), C("status", "Estado"), C("internalIp", "IP interno")]),
            new("gcp.storage.buckets", "Buckets do Cloud Storage", "Buckets do projeto.", "Cloud Storage", ["storage.buckets.list"], CloudRegionUse.None,
                [C("name", "Nome"), C("location", "Local"), C("storageClass", "Classe"), C("createdAt", "Criado em")])
        ],
        "Quem é vem da conta da service account ou do tokeninfo do Google; o projeto, do Resource Manager.");

    public CloudSettings Normalize(CloudSettings settings)
    {
        var values = new Dictionary<string, string>(settings.Values, StringComparer.Ordinal);
        if (values.TryGetValue(ProjectField, out var project))
            values[ProjectField] = ProjectPattern().IsMatch(project.ToLowerInvariant()) ? project.ToLowerInvariant()
                : throw CloudErrors.SettingsInvalid("O id do projeto tem de 6 a 30 letras minúsculas, números e hífens, começando por letra.");
        if (values.TryGetValue(KeyFileField, out var file) && (!Path.IsPathFullyQualified(file) || !file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            throw CloudErrors.SettingsInvalid("Informe o caminho absoluto do arquivo .json da chave.");
        if (values.TryGetValue(CloudFields.Region, out var region)) values[CloudFields.Region] = NormalizeRegion(region);
        return settings with { Values = values };
    }

    public string NormalizeRegion(string region)
    {
        var clean = region.Trim().ToLowerInvariant();
        return RegionPattern().IsMatch(clean) ? clean : throw CloudErrors.RegionInvalid($"'{region.Trim()}' não tem o formato de uma região do Google Cloud, como southamerica-east1.");
    }

    public async Task<CloudIdentity> IdentifyAsync(CloudSettings settings, CancellationToken ct)
    {
        try
        {
            var credential = await CredentialAsync(settings, ct);
            var projectId = settings.Value(ProjectField)!;
            var project = await Service(credential, init => new CloudResourceManagerService(init)).Projects.Get($"projects/{projectId}").ExecuteAsync(ct);
            return new CloudIdentity(project.ProjectId ?? projectId, project.DisplayName, await PrincipalAsync(credential, ct), null);
        }
        catch (Exception ex) when (GcpErrors.Map(ex, ct) is { } mapped)
        {
            throw mapped;
        }
    }

    public async Task<CloudTable> ReadAsync(CloudSettings settings, CloudQuery query, CancellationToken ct)
    {
        try
        {
            var credential = await CredentialAsync(settings, ct);
            var project = settings.Value(ProjectField)!;
            return query.Capability switch
            {
                "gcp.projects" => await ProjectsAsync(credential, query.Limit, ct),
                "gcp.services" => await ServicesAsync(credential, project, query.Limit, ct),
                "gcp.regions" => await RegionsAsync(credential, project, query.Limit, ct),
                "gcp.compute.instances" => await InstancesAsync(credential, project, query, ct),
                "gcp.storage.buckets" => await BucketsAsync(credential, project, query.Limit, ct),
                _ => throw CloudErrors.CapabilityUnknown()
            };
        }
        catch (Exception ex) when (GcpErrors.Map(ex, ct) is { } mapped)
        {
            throw mapped;
        }
    }

    public async Task<IReadOnlyDictionary<string, bool>> TestPermissionsAsync(CloudSettings settings, IReadOnlyList<string> permissions, CancellationToken ct)
    {
        try
        {
            var credential = await CredentialAsync(settings, ct);
            var request = new TestIamPermissionsRequest { Permissions = [.. permissions] };
            var response = await Service(credential, init => new CloudResourceManagerService(init))
                .Projects.TestIamPermissions(request, $"projects/{settings.Value(ProjectField)}").ExecuteAsync(ct);
            var granted = (response.Permissions ?? []).ToHashSet(StringComparer.Ordinal);
            return permissions.ToDictionary(p => p, granted.Contains, StringComparer.Ordinal);
        }
        catch (Exception ex) when (GcpErrors.Map(ex, ct) is { } mapped)
        {
            throw mapped;
        }
    }

    private static async Task<GoogleCredential> CredentialAsync(CloudSettings settings, CancellationToken ct)
    {
        var credential = settings.Mechanism switch
        {
            AdcMechanism => await GoogleCredential.GetApplicationDefaultAsync(ct),
            KeyFileMechanism => await KeyFileAsync(settings.Value(KeyFileField)!, ct),
            _ => throw CloudErrors.MechanismUnknown()
        };
        return credential.IsCreateScopedRequired ? credential.CreateScoped(Scope) : credential;
    }

    private static async Task<GoogleCredential> KeyFileAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) throw CloudErrors.NotAuthenticated("O arquivo da chave não existe neste caminho.");
        var serviceAccount = await CredentialFactory.FromFileAsync<ServiceAccountCredential>(path, ct);
        return serviceAccount.ToGoogleCredential();
    }

    // Service account diz o e-mail na própria credencial; conta de usuário, no tokeninfo (o token vai no corpo, não na URL).
    private static async Task<string> PrincipalAsync(GoogleCredential credential, CancellationToken ct)
    {
        if (credential.UnderlyingCredential is ServiceAccountCredential serviceAccount) return serviceAccount.Id;
        var token = await ((ITokenAccess)credential).GetAccessTokenForRequestAsync(cancellationToken: ct);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await http.PostAsync("https://oauth2.googleapis.com/tokeninfo",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("access_token", token)]), ct);
        if (!response.IsSuccessStatusCode) return "conta de usuário (o Google não informou o e-mail)";
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.TryGetProperty("email", out var email) && email.GetString() is { Length: > 0 } text
            ? text : "conta de usuário (o Google não informou o e-mail)";
    }

    private static T Service<T>(IConfigurableHttpClientInitializer credential, Func<BaseClientService.Initializer, T> create) where T : BaseClientService
    {
        var service = create(new BaseClientService.Initializer { HttpClientInitializer = credential, ApplicationName = "spock-studio" });
        service.HttpClient.Timeout = TimeSpan.FromSeconds(20);
        return service;
    }

    private static async Task<CloudTable> ProjectsAsync(GoogleCredential credential, int limit, CancellationToken ct)
    {
        var request = Service(credential, init => new CloudResourceManagerService(init)).Projects.Search();
        request.PageSize = Math.Clamp(limit + 1, 1, 500);
        var response = await request.ExecuteAsync(ct);
        var rows = (response.Projects ?? []).Select(p => Row(p.ProjectId, p.DisplayName, p.State)).ToList();
        return Table(rows, limit, more: response.NextPageToken is { Length: > 0 });
    }

    private static async Task<CloudTable> ServicesAsync(GoogleCredential credential, string project, int limit, CancellationToken ct)
    {
        var request = Service(credential, init => new ServiceUsageService(init)).Services.List($"projects/{project}");
        request.Filter = "state:ENABLED";
        request.PageSize = Math.Clamp(limit + 1, 1, 200);
        var response = await request.ExecuteAsync(ct);
        var rows = (response.Services ?? []).Select(s => Row(s.Config?.Name, s.Config?.Title)).ToList();
        return Table(rows, limit, more: response.NextPageToken is { Length: > 0 });
    }

    private static async Task<CloudTable> RegionsAsync(GoogleCredential credential, string project, int limit, CancellationToken ct)
    {
        var request = Service(credential, init => new ComputeService(init)).Regions.List(project);
        request.MaxResults = Math.Clamp(limit + 1, 1, 500);
        var response = await request.ExecuteAsync(ct);
        var rows = (response.Items ?? []).Select(r => Row(r.Name, r.Status)).ToList();
        return Table(rows, limit, more: response.NextPageToken is { Length: > 0 });
    }

    private static async Task<CloudTable> InstancesAsync(GoogleCredential credential, string project, CloudQuery query, CancellationToken ct)
    {
        var request = Service(credential, init => new ComputeService(init)).Instances.AggregatedList(project);
        request.MaxResults = Math.Clamp(query.Limit + 1, 1, 500);
        var response = await request.ExecuteAsync(ct);
        var rows = new List<IReadOnlyList<string?>>();
        foreach (var (scope, list) in response.Items ?? new Dictionary<string, Google.Apis.Compute.v1.Data.InstancesScopedList>())
        {
            var zone = scope.StartsWith("zones/", StringComparison.Ordinal) ? scope["zones/".Length..] : scope;
            if (query.Region is not null && !zone.StartsWith(query.Region + "-", StringComparison.Ordinal)) continue;
            rows.AddRange((list.Instances ?? []).Select(i => Row(i.Name, zone, Last(i.MachineType), i.Status, i.NetworkInterfaces?.FirstOrDefault()?.NetworkIP)));
        }
        return Table(rows, query.Limit, more: response.NextPageToken is { Length: > 0 });
    }

    private static async Task<CloudTable> BucketsAsync(GoogleCredential credential, string project, int limit, CancellationToken ct)
    {
        var request = Service(credential, init => new StorageService(init)).Buckets.List(project);
        request.MaxResults = Math.Clamp(limit + 1, 1, 1000);
        var response = await request.ExecuteAsync(ct);
        var rows = (response.Items ?? []).Select(b => Row(b.Name, b.Location, b.StorageClass,
            b.TimeCreatedDateTimeOffset?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))).ToList();
        return Table(rows, limit, more: response.NextPageToken is { Length: > 0 });
    }

    private static CloudTable Table(List<IReadOnlyList<string?>> rows, int limit, bool more) => new(rows.Take(limit).ToList(), more || rows.Count > limit);

    private static IReadOnlyList<string?> Row(params string?[] values) => values;

    private static string? Last(string? url) => url?[(url.LastIndexOf('/') + 1)..];

    [GeneratedRegex(@"^[a-z][a-z0-9\-]{4,28}[a-z0-9]$")]
    private static partial Regex ProjectPattern();

    [GeneratedRegex(@"^[a-z]+-[a-z]+\d{1,2}$")]
    private static partial Regex RegionPattern();
}
