namespace Mariliz.Application.Integrations;

public enum SyncStatus
{
    Running,
    Succeeded,

    // O ponto de retomada ficou gravado: a próxima continua dali.
    Partial,
    Failed
}
