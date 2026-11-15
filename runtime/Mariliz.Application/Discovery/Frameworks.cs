using System.Text.RegularExpressions;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Discovery;

// Só dependências que dizem algo sobre como o projeto é feito; uma biblioteca utilitária qualquer não vira tecnologia.
internal static partial class Frameworks
{
    internal sealed record Known(string Id, string Name, string Category);

    private const string Web = "Framework web";
    private const string Ui = "Biblioteca de interface";
    private const string Tests = "Testes";
    private const string Data = "Acesso a dados";
    private const string Desktop = "Aplicativo desktop";
    private const string Ai = "Orquestração de IA";
    private const string Validation = "Validação de dados";
    private const string Build = "Ferramenta de build";
    private const string Async = "Runtime assíncrono";

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
        ["hibernate-core"] = new("hibernate", "Hibernate", Data)
    };

    private static readonly Dictionary<string, Known> ById = new[] { Node, Python, Rust, Go, Java }
        .SelectMany(map => map.Values)
        .GroupBy(known => known.Id, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    internal static string? Category(string id) => ById.GetValueOrDefault(id)?.Category;

    // A chave do catálogo pode ser só o começo do nome (versões maiores do Go mudam o caminho: echo/v4; artefatos do Spring têm sufixo).
    internal static void Add(List<FrameworkFinding> found, IReadOnlyDictionary<string, Known> catalog, string stackId, string root,
        string manifest, IReadOnlyDictionary<string, string> dependencies, bool byPrefix = false)
    {
        foreach (var (name, range) in dependencies.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var key = byPrefix ? catalog.Keys.FirstOrDefault(prefix => name.StartsWith(prefix, StringComparison.Ordinal)) : catalog.ContainsKey(name) ? name : null;
            if (key is null) continue;
            var known = catalog[key];
            if (found.Any(item => item.Id == known.Id && item.Root == root)) continue;
            found.Add(new FrameworkFinding(known.Id, known.Name, stackId, root, VersionOf(range),
                [new Evidence(manifest, range.Length == 0 ? name : $"{name}={range}")]));
        }
    }

    internal static string? VersionOf(string range) => VersionPattern().Match(range).Value is { Length: > 0 } version ? version : null;

    [GeneratedRegex(@"\d+(\.\d+){0,2}")]
    private static partial Regex VersionPattern();
}
