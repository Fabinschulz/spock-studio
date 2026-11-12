namespace Mariliz.Application.Engineering;

/// <summary>Um arquivo gerenciado e como ele está em disco comparado ao que o Spock Studio escreveu.</summary>
/// <param name="Mode"><c>file</c> (o arquivo inteiro é gerenciado, conferido pelo hash) ou <c>merge</c> (só uma parte dele).</param>
/// <param name="State"><c>intact</c>, <c>kept</c> (versão da pessoa, mantida por decisão dela), <c>modified</c>, <c>missing</c> ou
/// <c>outOfScope</c> (caminho fora do que o produto pode gerenciar).</param>
public sealed record HarnessFileStatus(string Path, string Mode, string State);

/// <summary>Um componente do Harness registrado no lock, com o estado do pior dos seus arquivos.</summary>
public sealed record HarnessComponentStatus(string Id, string Label, string Kind, string Version, string State, IReadOnlyList<HarnessFileStatus> Files);

/// <summary>
/// O Harness aplicado num projeto, lido de <c>mariliz.lock</c> e conferido contra o disco. Só leitura: mudar componentes é
/// pela jornada Configurar, que planeja, pede aprovação e permite reverter.
/// </summary>
/// <param name="Applied">Falso quando o projeto ainda não tem <c>mariliz.lock</c>.</param>
/// <param name="LockReadable">Falso quando o lock existe mas não pôde ser lido.</param>
/// <param name="Excluded">Componentes que <c>mariliz.yaml</c> pede para não aplicar.</param>
/// <param name="Kept">Componentes com a versão da pessoa, mantida por decisão dela num conflito.</param>
public sealed record HarnessStatus(
    string ProjectId,
    DateTimeOffset InspectedAt,
    bool Applied,
    bool LockReadable,
    string? Engine,
    string? AiEnvironment,
    string? SpecificationVersion,
    IReadOnlyList<HarnessComponentStatus> Components,
    IReadOnlyList<string> Excluded,
    int Intact,
    int Kept,
    int Modified,
    int Missing);
