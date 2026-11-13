namespace Mariliz.Application.Ports;

// Sem shell: executável e argumentos separados, nunca uma linha de comando.
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct);
}
