namespace Mariliz.Core.Evals;

// Blocked = não pôde rodar por falta de pré-condição; NotRun = não foi executado nesta rodada. Nenhum dos dois conta como aprovado.
public enum EvalStatus { Passed, Failed, Blocked, NotRun }
