namespace Mariliz.Application.Execution;

/// <summary>Erros que mais de um caso de uso devolve: o mesmo código sempre com a mesma explicação para a pessoa.</summary>
public static class EngineErrors
{
    public static EngineException ProjectNotAuthorized() =>
        new("PROJECT_NOT_AUTHORIZED", "O projeto não foi autorizado. Selecione a pasta pelo diálogo do aplicativo.");

    public static EngineException RunInProgress() =>
        new("RUN_IN_PROGRESS", "Já há uma execução em andamento neste projeto.");

    public static EngineException RunNotFound() =>
        new("RUN_NOT_FOUND", "Execução não encontrada.");

    public static EngineException AiRuntimeInvalidResponse() =>
        new("AI_RUNTIME_INVALID_RESPONSE", "O runtime de IA respondeu fora do contrato.");

    /// <summary>O motivo (instalação, máquina, sem resposta) muda o que a pessoa pode fazer, então vem de quem detectou.</summary>
    public static EngineException AiRuntimeUnavailable(string reason) =>
        new("AI_RUNTIME_UNAVAILABLE", reason);
}
