namespace Mariliz.Application.Engineering;

/// <param name="Mode"><c>file</c> (o arquivo inteiro é gerenciado, conferido pelo hash) ou <c>merge</c> (só uma parte dele).</param>
/// <param name="State"><c>intact</c>, <c>kept</c> (versão da pessoa, mantida por decisão dela), <c>modified</c>, <c>missing</c> ou <c>outOfScope</c> (caminho fora do que o produto pode gerenciar).</param>
public sealed record HarnessFileStatus(string Path, string Mode, string State);

public sealed record HarnessExcludedComponent(string Id, string Label, bool Manageable);

/// <param name="ReferenceChanges">Numa regra gerada da pasta Organização, o que o catálogo deste motor tem de diferente do que ela
/// registrou (data da análise, padrões removidos, texto novo). Vazio quando está em dia ou não é regra de referência.</param>
public sealed record HarnessComponentStatus(string Id, string Label, string Kind, string Version, string State, bool Manageable, IReadOnlyList<HarnessFileStatus> Files,
    IReadOnlyList<string>? ReferenceChanges = null);

// Só leitura: mudar componentes é pela jornada Configurar, que planeja, pede aprovação e permite reverter.
public sealed record HarnessStatus(
    string ProjectId,
    DateTimeOffset InspectedAt,
    bool Applied,
    bool LockReadable,
    string? Engine,
    string? AiEnvironment,
    string? SpecificationVersion,
    IReadOnlyList<HarnessComponentStatus> Components,
    IReadOnlyList<HarnessExcludedComponent> Excluded,
    int Intact,
    int Kept,
    int Modified,
    int Missing);
