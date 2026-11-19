using System.Text.Json;
using Mariliz.Application.Harness;

namespace Mariliz.Application.Compatibility;

/// <param name="Status">active, maintenance, eol, preview, vendor (fim de suporte depende do fornecedor) ou unknown (sem política legível).</param>
/// <param name="Eol">Data do fim de suporte; o Python publica só o mês (AAAA-MM).</param>
/// <param name="Language">Versão padrão da linguagem nesta linha, quando a fonte oficial a define (C# por .NET).</param>
/// <param name="Requires">O que a versão mais nova da linha declara exigir, por tecnologia ("node": engines.node do npm).</param>
public sealed record ReleaseLine(
    string Cycle,
    string Channel,
    string Status,
    string? ReleaseDate,
    string? Eol,
    string? Latest,
    string? LatestDate,
    string? Language,
    IReadOnlyDictionary<string, string>? Requires = null);

/// <param name="Policy">lifecycle (datas da fonte), supported-window (só as listadas são suportadas), rolling (só a mais nova recebe
/// correção), vendor (depende do fornecedor) ou latest-major (só a maior mais nova é garantidamente mantida).</param>
/// <param name="Cycle">Como uma versão vira linha: major (22.11.0 → 22) ou major.minor (3.12.4 → 3.12).</param>
/// <param name="Stale">A última consulta à fonte falhou; as linhas são as da consulta anterior (CheckedAt).</param>
public sealed record CatalogTechnology(
    string Id,
    string Name,
    string Policy,
    string Cycle,
    string Source,
    DateTimeOffset CheckedAt,
    bool Stale,
    string? Error,
    string? Prerelease,
    IReadOnlyList<ReleaseLine> Lines);

// Gerado por scripts/refresh-release-catalog.py a partir das fontes oficiais e embutido no motor: as operações de
// desenvolvimento não dependem de rede, e uma atualização do catálogo é revisada como qualquer mudança de código.
public sealed record ReleaseCatalog(int SchemaVersion, DateTimeOffset GeneratedAt, IReadOnlyList<CatalogTechnology> Technologies)
{
    public const int SupportedSchema = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<ReleaseCatalog> EmbeddedCatalog = new(() => Parse(AssetLoader.Read("versions/release-catalog.json")));

    public static ReleaseCatalog Embedded => EmbeddedCatalog.Value;

    public static ReleaseCatalog Parse(string json)
    {
        var catalog = JsonSerializer.Deserialize<ReleaseCatalog>(json, Json)
                      ?? throw new InvalidDataException("Catálogo de versões vazio.");
        if (catalog.SchemaVersion != SupportedSchema)
            throw new InvalidDataException($"Catálogo de versões no formato {catalog.SchemaVersion}; o motor lê o formato {SupportedSchema}.");
        return catalog;
    }

    public CatalogTechnology? Find(string technologyId) =>
        Technologies.FirstOrDefault(technology => string.Equals(technology.Id, technologyId, StringComparison.Ordinal));
}
