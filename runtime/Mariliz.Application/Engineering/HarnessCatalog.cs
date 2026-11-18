using System.Text.Json;
using System.Text.RegularExpressions;
using Mariliz.Application.Harness;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Engineering;

public sealed record HarnessTool(string Name, string Plain);

// Texto escrito à mão junto do ativo (Assets/catalog/components.json) e fora dos arquivos que vão para o projeto: mudar uma ficha não muda o hash do Harness aplicado em ninguém.
/// <param name="Tools">Lidas do próprio arquivo do agente, não digitadas à mão.</param>
public sealed record ComponentSheet(
    string Id,
    string Kind,
    string Title,
    string Version,
    string WhatItDoes,
    string WhenItActs,
    IReadOnlyList<string> Reads,
    IReadOnlyList<string> CanChange,
    IReadOnlyList<HarnessTool> Tools,
    string Origin,
    IReadOnlyList<string> Files,
    bool Manageable);

// Presence: applied (está no lock), excluded (mariliz.yaml pede para não aplicar) ou not_applied. State é o do pior dos arquivos e só existe com o componente no lock; num excluído, indica que os arquivos ainda estão no projeto até a próxima aplicação.
public sealed record CatalogEntry(ComponentSheet Sheet, string? Presence, string? State);

public sealed record HarnessCatalog(string? ProjectId, bool? Applied, IReadOnlyList<CatalogEntry> Entries);

public sealed class HarnessCatalogService(HarnessStatusService? status)
{
    public const string SheetsAsset = "catalog/components.json";

    private static readonly Dictionary<string, string> ToolPlain = new(StringComparer.Ordinal)
    {
        ["Read"] = "ler arquivos",
        ["Grep"] = "buscar texto nos arquivos",
        ["Glob"] = "listar arquivos pelo nome",
        ["Bash"] = "rodar comandos no terminal",
        ["Write"] = "gravar arquivos",
        ["Edit"] = "editar arquivos",
        ["MultiEdit"] = "editar arquivos",
        ["NotebookEdit"] = "editar cadernos de notebook"
    };

    private const string UnknownTool = "uma ferramenta que o Spock Studio ainda não descreve";

    public static string PlainOf(string tool) => ToolPlain.GetValueOrDefault(tool, UnknownTool);


    private static IReadOnlyList<string> FilesOf(string id, HarnessNaming naming) =>
        id == Catalog.SpecificationStructure ? [".specs/"] : Catalog.FilesOf(id, naming);

    private static readonly Lazy<IReadOnlyList<ComponentSheet>> Sheets = new(LoadSheets);

    public static IReadOnlyList<ComponentSheet> AllSheets => Sheets.Value;

    public HarnessCatalog Read(string? projectId)
    {
        if (projectId is null) return new HarnessCatalog(null, null, AllSheets.Select(sheet => new CatalogEntry(sheet, null, null)).ToList());

        var state = (status ?? throw new Execution.EngineException("HARNESS_STATUS_UNAVAILABLE", "O estado do Harness não está disponível nesta sessão do motor.")).Inspect(projectId);
        var byId = state.Components.ToDictionary(component => component.Id, StringComparer.Ordinal);
        var excluded = state.Excluded.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var naming = status.NamingOf(projectId);

        var entries = AllSheets.Select(sheet => sheet with { Files = FilesOf(sheet.Id, naming) }).Select(sheet =>
        {
            byId.TryGetValue(sheet.Id, out var applied);
            // O que mariliz.yaml pede para não aplicar vale como "excluído" mesmo que os arquivos ainda estejam no projeto: eles saem
            // na próxima aplicação, e o estado, quando existe, diz que ainda há arquivos.
            if (excluded.Contains(sheet.Id)) return new CatalogEntry(sheet, "excluded", applied?.State);
            return applied is null ? new CatalogEntry(sheet, "not_applied", null) : new CatalogEntry(sheet, "applied", applied.State);
        }).ToList();

        // Regras do próprio projeto não estão no catálogo fixo: aparecem pelo que o lock registrou, com uma ficha genérica.
        foreach (var custom in state.Components.Where(component => component.Id.StartsWith(Catalog.CustomRulePrefix, StringComparison.Ordinal)))
            entries.Add(new CatalogEntry(CustomRule(custom), "applied", custom.State));
        foreach (var custom in state.Excluded.Where(item => item.Id.StartsWith(Catalog.CustomRulePrefix, StringComparison.Ordinal)))
            entries.Add(new CatalogEntry(CustomRule(custom.Id, [], custom.Label), "excluded", null));

        return new HarnessCatalog(projectId, state.Applied, entries);
    }

    private static ComponentSheet CustomRule(HarnessComponentStatus component) =>
        CustomRule(component.Id, component.Files.Select(file => file.Path).ToList(), component.Label);

    private static ComponentSheet CustomRule(string id, IReadOnlyList<string> files, string title) => new(
        id, "rule", title, Catalog.ComponentVersion,
        "Uma regra escrita para este projeto: o Harness Engineer a propôs a partir do que viu no código e você a aprovou.",
        "Sempre: o Claude Code carrega as regras nas sessões deste projeto.",
        ["Nenhum arquivo do projeto: são instruções para o agente"],
        ["Nada. É só orientação."],
        [],
        "Proposta pelo Harness Engineer a partir do seu projeto e aprovada por você.",
        files,
        true);

    private static List<ComponentSheet> LoadSheets()
    {
        using var document = JsonDocument.Parse(Harness.AssetLoader.Read(SheetsAsset));
        var written = document.RootElement.GetProperty("components").EnumerateArray().ToDictionary(
            item => item.GetProperty("id").GetString()!, item => item, StringComparer.Ordinal);

        return Catalog.Components.Select(component =>
        {
            if (!written.TryGetValue(component.Id, out var sheet))
                throw new InvalidOperationException($"O componente {component.Id} não tem ficha em {SheetsAsset}.");

            return new ComponentSheet(
                component.Id,
                component.Kind.ToString().ToLowerInvariant(),
                HarnessLabels.Of(component.Id),
                component.Version,
                Text(sheet, "whatItDoes"),
                Text(sheet, "whenItActs"),
                List(sheet, "reads"),
                List(sheet, "canChange"),
                ToolsOf(component.Id),
                Text(sheet, "origin"),
                FilesOf(component.Id, HarnessNaming.Spock),
                ComponentRequest.IsManageable(component.Id));
        }).ToList();
    }

    private static List<HarnessTool> ToolsOf(string id)
    {
        if (Catalog.Find(id)?.Kind != ComponentKind.Agent) return [];
        var match = Regex.Match(Harness.AssetLoader.Read(Catalog.Asset(id)), @"^tools:\s*(?<tools>.+)$", RegexOptions.Multiline);
        if (!match.Success) return [];
        return match.Groups["tools"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(name => new HarnessTool(name, PlainOf(name))).ToList();
    }

    private static string Text(JsonElement sheet, string name) => sheet.GetProperty(name).GetString()!;

    private static List<string> List(JsonElement sheet, string name) =>
        sheet.GetProperty(name).EnumerateArray().Select(item => item.GetString()!).ToList();
}
