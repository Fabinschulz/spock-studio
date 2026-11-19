using System.Text.RegularExpressions;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Discovery;

// Só dependências que dizem algo sobre como o projeto é feito; uma biblioteca utilitária qualquer não vira tecnologia.
internal static partial class Frameworks
{
    // Versioned = false: a versão do pacote não é a do framework (as extensões de resiliência 10.x trazem o Polly 8).
    internal sealed record Known(string Id, string Name, string Category, bool Versioned = true);

    private const string Web = "Framework web";
    private const string Ui = "Biblioteca de interface";
    private const string Tests = "Testes";
    private const string Data = "Acesso a dados";
    private const string Desktop = "Aplicativo desktop";
    private const string Ai = "Orquestração de IA";
    private const string Validation = "Validação de dados";
    private const string Build = "Ferramenta de build";
    private const string Async = "Runtime assíncrono";
    private const string Mediator = "Mediador de requisições";
    private const string Resilience = "Resiliência";
    private const string Observability = "Observabilidade";
    private const string Messaging = "Mensageria";
    private const string Jobs = "Tarefas em segundo plano";
    private const string Mapping = "Mapeamento de objetos";
    private const string Identity = "Autenticação";
    private const string ApiDocs = "Documentação de API";
    private const string Cache = "Cache";
    private const string Cloud = "SDK de nuvem";
    private const string App = "Aplicativo móvel e desktop";

    // O SDK do projeto e o UseMaui dizem tanto quanto um pacote: um projeto Web é ASP.NET Core sem PackageReference nenhum.
    internal static readonly Known AspNetCore = new("aspnetcore", "ASP.NET Core", Web);
    internal static readonly Known Blazor = new("blazor", "Blazor", Ui);
    internal static readonly Known Worker = new("worker-service", "Worker Service", Jobs);
    internal static readonly Known Maui = new("maui", ".NET MAUI", App);

    // O id do NuGet não diferencia maiúsculas e cada família tem vários pacotes (Microsoft.EntityFrameworkCore.SqlServer,
    // Serilog.Sinks.Console): a chave é o começo do nome.
    internal static readonly IReadOnlyDictionary<string, Known> Dotnet = new Dictionary<string, Known>(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.EntityFrameworkCore"] = new("efcore", "Entity Framework Core", Data),
        ["Npgsql.EntityFrameworkCore"] = new("efcore", "Entity Framework Core", Data),
        ["Pomelo.EntityFrameworkCore"] = new("efcore", "Entity Framework Core", Data),
        ["Dapper"] = new("dapper", "Dapper", Data),
        ["MediatR"] = new("mediatr", "MediatR", Mediator),
        ["FluentValidation"] = new("fluentvalidation", "FluentValidation", Validation),
        ["Polly"] = new("polly", "Polly", Resilience),
        ["Microsoft.Extensions.Http.Resilience"] = new("polly", "Polly", Resilience, Versioned: false),
        ["Microsoft.Extensions.Resilience"] = new("polly", "Polly", Resilience, Versioned: false),
        ["Serilog"] = new("serilog", "Serilog", Observability),
        ["OpenTelemetry"] = new("opentelemetry", "OpenTelemetry", Observability),
        ["Sentry"] = new("sentry", "Sentry", Observability),
        ["MassTransit"] = new("masstransit", "MassTransit", Messaging),
        ["Hangfire"] = new("hangfire", "Hangfire", Jobs),
        ["AutoMapper"] = new("automapper", "AutoMapper", Mapping),
        ["Microsoft.AspNetCore.Identity"] = new("aspnetcore-identity", "ASP.NET Core Identity", Identity),
        ["Microsoft.AspNetCore.Components.WebAssembly"] = Blazor,
        ["Microsoft.Maui"] = Maui,
        ["Swashbuckle.AspNetCore"] = new("swashbuckle", "Swashbuckle", ApiDocs),
        ["StackExchange.Redis"] = new("redis", "Redis", Cache),
        ["AWSSDK"] = new("aws-sdk", "AWS SDK", Cloud),
        ["ModelContextProtocol"] = new("mcp-sdk", "SDK do MCP", Ai),
        ["Microsoft.SemanticKernel"] = new("semantic-kernel", "Semantic Kernel", Ai),
        ["Microsoft.Agents.AI"] = new("agent-framework", "Microsoft Agent Framework", Ai),
        ["Microsoft.Extensions.AI"] = new("microsoft-extensions-ai", "Microsoft.Extensions.AI", Ai),
        ["OpenAI"] = new("openai", "OpenAI SDK", Ai),
        ["xunit"] = new("xunit", "xUnit", Tests),
        ["NUnit"] = new("nunit", "NUnit", Tests),
        ["MSTest"] = new("mstest", "MSTest", Tests)
    };

    internal static readonly IReadOnlyDictionary<string, Known> Node = new Dictionary<string, Known>(StringComparer.Ordinal)
    {
        ["typescript"] = new("typescript", "TypeScript", "Linguagem"),
        ["fastify"] = new("fastify", "Fastify", Web),
        ["express"] = new("express", "Express", Web),
        ["@nestjs/core"] = new("nestjs", "NestJS", Web),
        ["vue"] = new("vue", "Vue", Ui),
        ["svelte"] = new("svelte", "Svelte", Ui),
        ["vite"] = new("vite", "Vite", Build),
        ["vitest"] = new("vitest", "Vitest", Tests),
        ["jest"] = new("jest", "Jest", Tests),
        ["@playwright/test"] = new("playwright", "Playwright", Tests),
        ["zod"] = new("zod", "Zod", Validation),
        ["@prisma/client"] = new("prisma", "Prisma", Data),
        ["@tauri-apps/api"] = new("tauri", "Tauri", Desktop),
        ["electron"] = new("electron", "Electron", Desktop)
    };

    internal static readonly IReadOnlyDictionary<string, Known> Python = new Dictionary<string, Known>(StringComparer.Ordinal)
    {
        ["django"] = new("django", "Django", Web),
        ["fastapi"] = new("fastapi", "FastAPI", Web),
        ["flask"] = new("flask", "Flask", Web),
        ["langgraph"] = new("langgraph", "LangGraph", Ai),
        ["langchain"] = new("langchain", "LangChain", Ai),
        ["langchain-core"] = new("langchain", "LangChain", Ai),
        ["agent-framework"] = new("agent-framework", "Microsoft Agent Framework", Ai),
        ["agent-framework-core"] = new("agent-framework", "Microsoft Agent Framework", Ai),
        ["semantic-kernel"] = new("semantic-kernel", "Semantic Kernel", Ai),
        ["pydantic"] = new("pydantic", "Pydantic", Validation),
        ["sqlalchemy"] = new("sqlalchemy", "SQLAlchemy", Data),
        ["pytest"] = new("pytest", "pytest", Tests)
    };

    internal static readonly IReadOnlyDictionary<string, Known> Rust = new Dictionary<string, Known>(StringComparer.Ordinal)
    {
        ["tauri"] = new("tauri", "Tauri", Desktop),
        ["axum"] = new("axum", "Axum", Web),
        ["actix-web"] = new("actix-web", "Actix Web", Web),
        ["tokio"] = new("tokio", "Tokio", Async),
        ["sqlx"] = new("sqlx", "SQLx", Data),
        ["diesel"] = new("diesel", "Diesel", Data)
    };

    internal static readonly IReadOnlyDictionary<string, Known> Go = new Dictionary<string, Known>(StringComparer.Ordinal)
    {
        ["github.com/gin-gonic/gin"] = new("gin", "Gin", Web),
        ["github.com/labstack/echo"] = new("echo", "Echo", Web),
        ["github.com/gofiber/fiber"] = new("fiber", "Fiber", Web),
        ["gorm.io/gorm"] = new("gorm", "GORM", Data)
    };

    internal static readonly IReadOnlyDictionary<string, Known> Java = new Dictionary<string, Known>(StringComparer.Ordinal)
    {
        ["spring-boot"] = new("spring-boot", "Spring Boot", Web),
        ["quarkus"] = new("quarkus", "Quarkus", Web),
        ["junit-jupiter"] = new("junit", "JUnit", Tests),
        ["hibernate-core"] = new("hibernate", "Hibernate", Data),
        // Plugins do Gradle, lidos do bloco plugins: a linguagem e o build do Android, que limitam o que o código pode usar.
        ["kotlin"] = new("kotlin", "Kotlin", "Linguagem"),
        ["android-gradle-plugin"] = new("android-gradle-plugin", "Android Gradle Plugin", App)
    };

    internal static readonly IReadOnlyDictionary<string, Known> Php = new Dictionary<string, Known>(StringComparer.Ordinal)
    {
        ["laravel/framework"] = new("laravel", "Laravel", Web),
        ["symfony/framework-bundle"] = new("symfony", "Symfony", Web),
        ["slim/slim"] = new("slim", "Slim", Web),
        ["doctrine/orm"] = new("doctrine", "Doctrine ORM", Data),
        ["phpunit/phpunit"] = new("phpunit", "PHPUnit", Tests),
        ["pestphp/pest"] = new("pest", "Pest", Tests)
    };

    internal static readonly IReadOnlyDictionary<string, Known> Ruby = new Dictionary<string, Known>(StringComparer.Ordinal)
    {
        ["rails"] = new("rails", "Ruby on Rails", Web),
        ["sinatra"] = new("sinatra", "Sinatra", Web),
        ["sidekiq"] = new("sidekiq", "Sidekiq", Jobs),
        ["rspec"] = new("rspec", "RSpec", Tests),
        ["rspec-core"] = new("rspec", "RSpec", Tests),
        ["minitest"] = new("minitest", "Minitest", Tests)
    };

    private static readonly Dictionary<string, Known> ById = new[] { Node, Python, Rust, Go, Java, Dotnet, Php, Ruby }
        .SelectMany(map => map.Values)
        .Concat([AspNetCore, Worker])
        .GroupBy(known => known.Id, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    internal static string? Category(string id) => ById.GetValueOrDefault(id)?.Category;

    // A chave do catálogo pode ser só o começo do nome (versões maiores do Go mudam o caminho: echo/v4; artefatos do Spring têm sufixo).
    internal static void Add(List<FrameworkFinding> found, IReadOnlyDictionary<string, Known> catalog, string stackId, string root,
        string manifest, IReadOnlyDictionary<string, string> dependencies, bool byPrefix = false,
        StringComparison comparison = StringComparison.Ordinal)
    {
        foreach (var (name, range) in dependencies.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var key = byPrefix ? catalog.Keys.FirstOrDefault(prefix => name.StartsWith(prefix, comparison)) : catalog.ContainsKey(name) ? name : null;
            if (key is null) continue;
            Add(found, catalog[key], stackId, root, catalog[key].Versioned ? VersionOf(range) : null, new Evidence(manifest, range.Length == 0 ? name : $"{name}={range}"));
        }
    }

    internal static void Add(List<FrameworkFinding> found, Known known, string stackId, string root, string? version, Evidence evidence)
    {
        if (found.Any(item => item.Id == known.Id && item.Root == root)) return;
        found.Add(new FrameworkFinding(known.Id, known.Name, stackId, root, version, [evidence]));
    }

    internal static string? VersionOf(string range) => VersionPattern().Match(range).Value is { Length: > 0 } version ? version : null;

    [GeneratedRegex(@"\d+(\.\d+){0,2}")]
    private static partial Regex VersionPattern();
}
