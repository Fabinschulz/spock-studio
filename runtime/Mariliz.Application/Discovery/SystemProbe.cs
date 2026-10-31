using System.Text.RegularExpressions;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Discovery;

internal sealed class SystemProbe(IProcessRunner processRunner)
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    public async Task<ToolAvailability> ProbeToolsAsync(string root, CancellationToken ct)
    {
        async Task<string?> Version(string exe, string pattern)
        {
            var r = await processRunner.RunAsync(new ProcessSpec(exe, ["--version"], root, ProbeTimeout), ct);
            if (r.NotFound || r.TimedOut || r.ExitCode != 0) return null;
            var m = Regex.Match(r.StandardOutput + " " + r.StandardError, pattern);
            return m.Success ? m.Groups[1].Value : null;
        }

        var git = await Version("git", @"git version (\d+(?:\.\d+)+)");
        var node = await Version("node", @"v?(\d+\.\d+\.\d+)");
        string? python = null, pythonCommand = null;
        foreach (var candidate in new[] { "python3", "python" })
        {
            python = await Version(candidate, @"Python (3\.\d+\.\d+)");
            if (python is not null) { pythonCommand = candidate; break; }
        }
        return new ToolAvailability(git, node, python, pythonCommand);
    }

    public async Task<GitState> ReadGitStateAsync(string root, IProjectFiles files, ToolAvailability tools, CancellationToken ct)
    {
        if (tools.Git is null) return new GitState(files.DirectoryExists(".git"), null, []);

        var inside = await processRunner.RunAsync(new ProcessSpec("git", ["rev-parse", "--is-inside-work-tree"], root, ProbeTimeout), ct);
        if (inside.NotFound || inside.ExitCode != 0 || !inside.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
            return new GitState(false, null, []);

        var status = await processRunner.RunAsync(new ProcessSpec("git", ["status", "--porcelain", "--", "."], root, ProbeTimeout), ct);
        if (status.ExitCode != 0) return new GitState(true, null, []);

        var changed = status.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Length > 3 ? l[3..].Trim().Trim('"') : l.Trim()).ToList();
        return new GitState(true, changed.Count > 0, changed.Take(50).ToList());
    }
}
