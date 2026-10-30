namespace Mariliz.Core.Evals;

/// <summary>
/// Contrato de resultado dos Evals. <c>Blocked</c> = não pôde rodar por falta de pré-condição;
/// <c>NotRun</c> = não foi executado nesta rodada. Nenhum dos dois conta como aprovado.
/// </summary>
public enum EvalStatus { Passed, Failed, Blocked, NotRun }
