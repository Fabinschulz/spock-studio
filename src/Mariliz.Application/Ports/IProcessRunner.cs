namespace Mariliz.Application.Ports;

/// <summary>Executa processos sem shell: executável e argumentos separados, nunca uma linha de comando.</summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct);
}
