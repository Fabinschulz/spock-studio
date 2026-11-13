namespace Mariliz.Infrastructure;

// Lê as variáveis de ambiente MARILIZ_AI, MARILIZ_PYTHON e PATH.
internal static class AiRuntimeLocator
{
    internal static string? Script()
    {
        var env = Environment.GetEnvironmentVariable("MARILIZ_AI");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "intelligence", "server.py");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    public static IEnumerable<string> PythonCandidates(string? pythonOverride) =>
        PythonRuntimeLayout.Candidates(
            AppContext.BaseDirectory,
            pythonOverride,
            Environment.GetEnvironmentVariable("MARILIZ_PYTHON"),
            Environment.GetEnvironmentVariable("PATH"));
}
