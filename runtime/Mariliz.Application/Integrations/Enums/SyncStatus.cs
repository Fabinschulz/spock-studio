namespace Mariliz.Application.Integrations;

public enum SyncStatus
{
    Running,
    Succeeded,

    /// <summary>Parou no teto de páginas por execução. O ponto de retomada ficou gravado: a próxima continua dali.</summary>
    Partial,
    Failed
}
