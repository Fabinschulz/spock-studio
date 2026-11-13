using System.Text.Json;
using System.Text.RegularExpressions;
using Mariliz.Application.Harness;

namespace Mariliz.Application.Engineering;

/// <summary>Uma ferramenta que o agente pode usar, com o que isso quer dizer em palavras simples.</summary>
public sealed record HarnessTool(string Name, string Plain);

/// <summary>
/// A ficha de um componente do Harness: o que faz, quando age, o que lê, o que pode alterar e de onde veio. É texto escrito
/// à mão junto do ativo (<c>Assets/catalog/components.json</c>) e fica <b>fora</b> dos arquivos que vão para o projeto: mudar uma
/// ficha não muda o hash do Harness aplicado em ninguém.
/// </summary>
/// <param name="Tools">Só para agentes: as ferramentas declaradas no próprio arquivo do agente, lidas do ativo e não digitadas à mão.</param>
/// <param name="Files">Onde o componente escreve no projeto.</param>
/// <param name="Manageable">Se pode ser dispensado e reaplicado pela jornada Configurar.</param>
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

/// <summary>
/// Um componente do catálogo no projeto aberto. <see cref="Presence"/>: <c>applied</c> (está no lock), <c>excluded</c>
/// (<c>mariliz.yaml</c> pede para não aplicar) ou <c>not_applied</c>. <see cref="State"/> é o estado do pior dos arquivos
/// (<c>intact</c>, <c>kept</c>, <c>modified</c>, <c>missing</c>, <c>outOfScope</c>) e só existe se o componente está no lock; num
/// componente excluído, indica que os arquivos ainda estão no projeto até a próxima aplicação.
/// </summary>
public sealed record CatalogEntry(ComponentSheet Sheet, string? Presence, string? State);

/// <param name="ProjectId">Nulo no catálogo geral, sem projeto.</param>
/// <param name="Applied">Se o projeto já tem o Harness aplicado (existe lock). Nulo sem projeto.</param>
public sealed record HarnessCatalog(string? ProjectId, bool? Applied, IReadOnlyList<CatalogEntry> Entries);

/// <summary>Lê as fichas dos componentes e as junta, se houver projeto, ao que está aplicado nele.</summary>
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

    /// <summary>O que uma ferramenta do Claude Code quer dizer, em palavras simples.</summary>
    public static string PlainOf(string tool) => ToolPlain.GetValueOrDefault(tool, UnknownTool);

    /// <summary>O arquivo do agente de onde as ferramentas são lidas.</summary>
    private static readonly Dictionary<string, string> AgentAssets = new(StringComparer.Ordinal)
    {
        [Catalog.AgentHarnessEngineer] = "agents/harness-engineer.md",
        [Catalog.AgentCodeReviewer] = "agents/code-reviewer.md",
        [Catalog.AgentVerifier] = "agents/verifier.md"
    };

    private static readonly Dictionary<string, string[]> FilesOf = new(StringComparer.Ordinal)
    {
        [Catalog.AgentHarnessEngineer] = [Catalog.PathHarnessEngineer, Catalog.PathProposalSchema],
        [Catalog.AgentCodeReviewer] = [Catalog.PathCodeReviewer],
        [Catalog.AgentVerifier] = [Catalog.PathVerifier],
        [Catalog.SkillHarnessContract] = [Catalog.PathHarnessContract],
        [Catalog.RuleGeneral] = [Catalog.PathRuleGeneral],
        [Catalog.RuleStackDotnet] = [Catalog.PathRuleDotnet],
        [Catalog.RuleStackJavascript] = [Catalog.PathRuleJavascript],
        [Catalog.RuleRepoCommands] = [Catalog.PathRuleRepo],
        [Catalog.HookPolicy] = [Catalog.PathHookScript, Catalog.PathSettings],
        [Catalog.SpecificationStructure] = [".specs/"],
        [Catalog.ConfigDeclaration] = ["mariliz.yaml", "mariliz.lock"]
    };

    private static readonly Lazy<IReadOnlyList<ComponentSheet>> Sheets = new(LoadSheets);

    /// <summary>As fichas de todos os componentes do catálogo, na ordem do catálogo.</summary>
    public static IReadOnlyList<ComponentSheet> AllSheets => Sheets.Value;

    public HarnessCatalog Read(string? projectId)
    {
        if (projectId is null) return new HarnessCatalog(null, null, AllSheets.Select(sheet => new CatalogEntry(sheet, null, null)).ToList());

        var state = (status ?? throw new Execution.EngineException("HARNESS_STATUS_UNAVAILABLE", "O estado do Harness não está disponível nesta sessão do motor.")).Inspect(projectId);
        var byId = state.Components.ToDictionary(component => component.Id, StringComparer.Ordinal);
        var excluded = state.Excluded.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);

        var entries = AllSheets.Select(sheet =>
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
                FilesOf.GetValueOrDefault(component.Id) ?? [],
                ComponentRequest.IsManageable(component.Id));
        }).ToList();
    }

    /// <summary>As ferramentas declaradas no cabeçalho do agente (<c>tools: Read, Grep</c>), lidas do próprio ativo.</summary>
    private static List<HarnessTool> ToolsOf(string id)
    {
        if (!AgentAssets.TryGetValue(id, out var asset)) return [];
        var match = Regex.Match(Harness.AssetLoader.Read(asset), @"^tools:\s*(?<tools>.+)$", RegexOptions.Multiline);
        if (!match.Success) return [];
        return match.Groups["tools"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(name => new HarnessTool(name, PlainOf(name))).ToList();
    }

    private static string Text(JsonElement sheet, string name) => sheet.GetProperty(name).GetString()!;

    private static List<string> List(JsonElement sheet, string name) =>
        sheet.GetProperty(name).EnumerateArray().Select(item => item.GetString()!).ToList();
}
