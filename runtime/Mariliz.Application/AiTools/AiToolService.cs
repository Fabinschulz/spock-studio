using System.Text.RegularExpressions;
using Mariliz.Application.Discovery;
using Mariliz.Application.Ports;

namespace Mariliz.Application.AiTools;

public sealed class AiToolService(IExecutableLocator locator, IProcessRunner runner)
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
    private static readonly Regex VersionPattern = new(@"\d+(?:\.\d+)+", RegexOptions.None, TimeSpan.FromSeconds(1));

    public async Task<IReadOnlyList<AiToolStatus>> ListAsync(CancellationToken ct) =>
        await Task.WhenAll(AiToolCatalog.All.Select(tool => ProbeAsync(tool, ct)));

    private async Task<AiToolStatus> ProbeAsync(AiToolDefinition tool, CancellationToken ct)
    {
        var harnessSupport = AiEnvironments.Supported.Contains(tool.Id);
        var path = locator.Find(tool.Binary);
        if (path is null) return new AiToolStatus(tool.Id, tool.Name, tool.Binary, false, null, harnessSupport);

        var result = await runner.RunAsync(new ProcessSpec(path, ["--version"], Path.GetTempPath(), ProbeTimeout), ct);
        return new AiToolStatus(tool.Id, tool.Name, tool.Binary, true, ReadVersion(result), harnessSupport);
    }

    private static string? ReadVersion(ProcessResult result)
    {
        if (result.NotFound || result.TimedOut || result.ExitCode != 0) return null;
        var match = VersionPattern.Match(result.StandardOutput + " " + result.StandardError);
        return match.Success ? match.Value : null;
    }
}
