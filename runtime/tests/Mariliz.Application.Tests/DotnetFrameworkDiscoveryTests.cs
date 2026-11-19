using Mariliz.Application.Engineering;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Tests;

// W2-1: a descoberta .NET parava no TargetFramework; EF Core, FluentValidation, Serilog e o resto não chegavam ao perfil.
public class DotnetFrameworkDiscoveryTests
{
    private static Task<DiscoveryReport> Discover(TempProject project) =>
        new Journey(project).Service.DiscoverAsync(new Journey(project).ProjectId, CancellationToken.None);

    private static FrameworkFinding Framework(DiscoveryReport report, string id) => Assert.Single(report.Frameworks, item => item.Id == id);

    private static string Csproj(string sdk, string body) =>
        $"<Project Sdk=\"{sdk}\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup>{body}</ItemGroup></Project>";

    private static string Package(string name, string version) => $"<PackageReference Include=\"{name}\" Version=\"{version}\" />";

    [Fact]
    public async Task Uma_api_em_camadas_lista_os_frameworks_conhecidos_e_ignora_bibliotecas_utilitarias()
    {
        using var project = new TempProject();
        project.Write("backend/Api.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        project.Write("backend/src/Api/Api.csproj", Csproj("Microsoft.NET.Sdk.Web",
            Package("Sentry.AspNetCore", "6.6.0") + Package("Swashbuckle.AspNetCore.SwaggerGen", "10.2.1") + Package("Newtonsoft.Json", "13.0.4")));
        project.Write("backend/src/Application/Application.csproj", Csproj("Microsoft.NET.Sdk",
            Package("FluentValidation", "12.1.1") + Package("FluentValidation.DependencyInjectionExtensions", "12.1.1")));
        project.Write("backend/src/Infra/Infra.csproj", Csproj("Microsoft.NET.Sdk",
            Package("Microsoft.AspNetCore.Identity.EntityFrameworkCore", "10.0.8") + Package("Microsoft.EntityFrameworkCore", "10.0.8")
            + Package("Npgsql.EntityFrameworkCore.PostgreSQL", "10.0.2") + Package("StackExchange.Redis", "2.10.1")
            + Package("AWSSDK.SimpleEmailV2", "4.0.104.2") + Package("OpenAI", "2.2.0")));
        project.Write("backend/tests/Tests.csproj", Csproj("Microsoft.NET.Sdk",
            Package("xunit", "2.9.3") + Package("xunit.runner.visualstudio", "3.1.5") + Package("Moq", "4.20.72") + Package("FluentAssertions", "8.10.0")));

        var report = await Discover(project);

        Assert.Equal(
            ["aspnetcore", "aspnetcore-identity", "aws-sdk", "efcore", "fluentvalidation", "openai", "redis", "sentry", "swashbuckle", "xunit"],
            report.Frameworks.Select(item => item.Id).Order(StringComparer.Ordinal));
        Assert.All(report.Frameworks, item => Assert.Equal(("dotnet", "backend"), (item.StackId, item.Root)));
        var efcore = Framework(report, "efcore");
        Assert.Equal("10.0.8", efcore.Version);
        Assert.Equal(new Evidence("backend/src/Infra/Infra.csproj", "Microsoft.EntityFrameworkCore=10.0.8"), Assert.Single(efcore.Evidence));
        Assert.Equal(("10.0", "Sdk=Microsoft.NET.Sdk.Web"), (Framework(report, "aspnetcore").Version, Framework(report, "aspnetcore").Evidence[0].Detail));
    }

    [Fact]
    public async Task Nome_do_pacote_nao_diferencia_maiusculas_e_a_resiliencia_da_microsoft_conta_como_polly()
    {
        using var project = new TempProject();
        project.Write("Worker.csproj", Csproj("Microsoft.NET.Sdk.Worker",
            Package("mediatr", "12.4.1") + Package("Microsoft.Extensions.Http.Resilience", "10.1.0")
            + Package("Serilog.AspNetCore", "9.0.0") + Package("OpenTelemetry.Extensions.Hosting", "1.12.0")));

        var report = await Discover(project);

        Assert.Equal(["mediatr", "opentelemetry", "polly", "serilog", "worker-service"], report.Frameworks.Select(item => item.Id).Order(StringComparer.Ordinal));
        Assert.Equal("MediatR", Framework(report, "mediatr").Name);
        var polly = Framework(report, "polly");
        Assert.Equal("Microsoft.Extensions.Http.Resilience=10.1.0", polly.Evidence[0].Detail);
        Assert.Null(polly.Version);
    }

    [Fact]
    public async Task Versao_central_vem_do_Directory_Packages_props_mais_proximo_e_o_override_vence()
    {
        using var project = new TempProject();
        project.Write("Directory.Packages.props",
            "<Project><ItemGroup><PackageVersion Include=\"Polly\" Version=\"8.0.0\" /><PackageVersion Include=\"Dapper\" Version=\"2.0.0\" /></ItemGroup></Project>");
        project.Write("src/Directory.Packages.props", "<Project><ItemGroup><PackageVersion Include=\"Polly\" Version=\"8.5.2\" /></ItemGroup></Project>");
        project.Write("src/Data/Data.csproj", Csproj("Microsoft.NET.Sdk",
            "<PackageReference Include=\"Polly\" /><PackageReference Include=\"Dapper\" VersionOverride=\"2.1.66\" />"));

        var report = await Discover(project);

        Assert.Equal("8.5.2", Framework(report, "polly").Version);
        Assert.Equal("2.1.66", Framework(report, "dapper").Version);
    }

    [Fact]
    public async Task Pacote_do_Directory_Build_props_entra_sem_aparecer_em_nenhum_projeto()
    {
        using var project = new TempProject();
        project.Write("Directory.Build.props", "<Project><ItemGroup><PackageReference Include=\"Serilog\"><Version>4.2.0</Version></PackageReference></ItemGroup></Project>");
        project.Write("src/App/App.csproj", Csproj("Microsoft.NET.Sdk", ""));

        var serilog = Framework(await Discover(project), "serilog");

        Assert.Equal("4.2.0", serilog.Version);
        Assert.Equal("Directory.Build.props", serilog.Evidence[0].Path);
    }

    [Fact]
    public async Task Blazor_e_MAUI_vem_do_SDK_dos_componentes_razor_e_do_UseMaui()
    {
        using var webassembly = new TempProject();
        webassembly.Write("Client/Client.csproj", Csproj("Microsoft.NET.Sdk.BlazorWebAssembly", ""));
        using var server = new TempProject();
        server.Write("Site/Site.csproj", Csproj("Microsoft.NET.Sdk.Web", ""));
        server.Write("Site/Components/Pages/Home.razor", "<h1>Início</h1>\n");
        using var pages = new TempProject();
        pages.Write("Site/Site.csproj", Csproj("Microsoft.NET.Sdk.Web", ""));
        pages.Write("Site/Pages/Index.cshtml", "<h1>Início</h1>\n");
        using var maui = new TempProject();
        maui.Write("App/App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net9.0-android;net9.0-ios</TargetFrameworks><UseMaui>true</UseMaui></PropertyGroup></Project>");

        Assert.Equal("Sdk=Microsoft.NET.Sdk.BlazorWebAssembly", Framework(await Discover(webassembly), "blazor").Evidence[0].Detail);
        Assert.Equal("Site/Components/Pages/Home.razor", Framework(await Discover(server), "blazor").Evidence[0].Path);
        Assert.DoesNotContain((await Discover(pages)).Frameworks, item => item.Id == "blazor");
        var app = Framework(await Discover(maui), "maui");
        Assert.Equal(("9.0", "UseMaui=true"), (app.Version, app.Evidence[0].Detail));
    }

    [Fact]
    public async Task Projeto_do_NET_Framework_nao_ganha_versao_de_ASP_NET_Core()
    {
        using var project = new TempProject();
        project.Write("Legado/Legado.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net48</TargetFramework></PropertyGroup><ItemGroup><FrameworkReference Include=\"Microsoft.AspNetCore.App\" /></ItemGroup></Project>");

        var aspnet = Framework(await Discover(project), "aspnetcore");

        Assert.Null(aspnet.Version);
        Assert.Equal("FrameworkReference=Microsoft.AspNetCore.App", aspnet.Evidence[0].Detail);
    }

    [Fact]
    public void Frameworks_dotnet_tem_categoria_no_perfil()
    {
        Assert.Equal("Acesso a dados", TechnologyCatalog.Category("efcore"));
        Assert.Equal("Resiliência", TechnologyCatalog.Category("polly"));
        Assert.Equal("Framework web", TechnologyCatalog.Category("aspnetcore"));
        Assert.Equal("Tarefas em segundo plano", TechnologyCatalog.Category("worker-service"));
    }
}
