namespace Mariliz.Application.Engineering;

/// <summary>O que uma revisão mudou num componente, por arquivo.</summary>
public sealed record HarnessRevisionComponent(string Id, string Label, IReadOnlyList<string> Created, IReadOnlyList<string> Updated, IReadOnlyList<string> Removed);

/// <summary>
/// Uma revisão do Harness: uma aplicação ou uma reversão que mudou arquivos no projeto, numerada na ordem em que
/// aconteceu. Vem do diário da execução, o mesmo que permite reverter.
/// </summary>
/// <param name="Kind"><c>apply</c> ou <c>revert</c>.</param>
/// <param name="RolledBack">A aplicação falhou no meio e o que tinha gravado foi desfeito na hora.</param>
/// <param name="RevertedByRunId">A reversão que desfez esta aplicação, quando houve.</param>
/// <param name="RevertsRunId">A aplicação que esta reversão desfez.</param>
public sealed record HarnessRevision(
    int Number,
    string RunId,
    string Kind,
    DateTimeOffset At,
    string State,
    bool RolledBack,
    string? RevertedByRunId,
    string? RevertsRunId,
    IReadOnlyList<HarnessRevisionComponent> Components);

/// <summary>As revisões do Harness de um projeto, da mais recente para a mais antiga.</summary>
/// <param name="AppliedByEngine">Versão do motor que gravou o lock atual; nula sem lock.</param>
/// <param name="CurrentEngine">Versão deste motor.</param>
public sealed record HarnessRevisions(
    string ProjectId,
    string? AppliedByEngine,
    string CurrentEngine,
    IReadOnlyList<HarnessRevision> Revisions);
