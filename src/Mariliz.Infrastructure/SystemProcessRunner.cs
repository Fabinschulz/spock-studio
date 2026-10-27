using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure;

/// <summary>Executa processos sem shell: executável e argumentos ficam separados, então nada é interpretado como comando.</summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    private const int MaxOutputChars = 1_000_000;

    /// <summary>
    /// A entrada padrão do processo filho é <b>sempre</b> redirecionada. Sem isso ele herdaria a entrada do próprio motor,
    /// que no aplicativo é o canal do protocolo com o host: um script que leia a entrada ficaria esperando (e poderia
    /// engolir mensagens do protocolo). Sem <see cref="ProcessSpec.StandardInput"/>, o filho recebe uma entrada vazia e fechada.
    /// </summary>
    public static ProcessStartInfo BuildStartInfo(ProcessSpec spec)
    {
        var psi = new ProcessStartInfo(Executable(spec.Executable))
        {
            WorkingDirectory = spec.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in spec.Arguments) psi.ArgumentList.Add(arg);
        return psi;
    }

    public async Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct)
    {
        var psi = BuildStartInfo(spec);

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

            try
            {
                if (spec.StandardInput is not null) await process.StandardInput.WriteAsync(spec.StandardInput);
                process.StandardInput.Close();
            }
            catch (IOException) { /* o processo encerrou antes de ler: o código de saída diz o que houve */ }

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

    /// <summary>
    /// No Windows, npx e npm são scripts .cmd. Com o nome simples, o script resolve o npm a partir da pasta de trabalho
    /// (a do projeto) e quebra; por isso o caminho absoluto é procurado no PATH, como faz um shell.
    /// </summary>
    private static string Executable(string name)
    {
        if (!OperatingSystem.IsWindows() || name is not ("npx" or "npm")) return name;

        var script = name + ".cmd";
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim().Trim('"'), script);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { /* entrada inválida no PATH: segue para a próxima */ }
        }
        return script;
    }

    private static void Append(StringBuilder sb, string? line)
    {
        if (line is null || sb.Length > MaxOutputChars) return;
        sb.AppendLine(line);
    }
}
