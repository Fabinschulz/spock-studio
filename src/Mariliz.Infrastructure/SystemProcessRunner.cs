using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure;

/// <summary>Executa processos sem shell: executável e argumentos ficam separados, então nada é interpretado como comando.</summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    private const int MaxOutputChars = 1_000_000;

    public async Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(Executable(spec.Executable))
        {
            WorkingDirectory = spec.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = spec.StandardInput is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in spec.Arguments) psi.ArgumentList.Add(arg);

        Process process;
        try { process = Process.Start(psi) ?? throw new InvalidOperationException(); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return new ProcessResult(-1, "", "", false, true);
        }

        using (process)
        {
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => Append(stdout, e.Data);
            process.ErrorDataReceived += (_, e) => Append(stderr, e.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (spec.StandardInput is not null)
            {
                try
                {
                    await process.StandardInput.WriteAsync(spec.StandardInput);
                    process.StandardInput.Close();
                }
                catch (IOException) { /* o processo encerrou antes de ler: o código de saída diz o que houve */ }
            }

            using var timeout = new CancellationTokenSource(spec.Timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            try
            {
                await process.WaitForExitAsync(linked.Token);
                // Garante que a saída assíncrona terminou de ser lida.
                process.WaitForExit();
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                if (ct.IsCancellationRequested) throw;
                return new ProcessResult(-1, stdout.ToString(), stderr.ToString(), true, false);
            }

            return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), false, false);
        }
    }

    private static string Executable(string name) =>
        OperatingSystem.IsWindows() && name is "npx" or "npm" ? name + ".cmd" : name;

    private static void Append(StringBuilder sb, string? line)
    {
        if (line is null || sb.Length > MaxOutputChars) return;
        sb.AppendLine(line);
    }
}
