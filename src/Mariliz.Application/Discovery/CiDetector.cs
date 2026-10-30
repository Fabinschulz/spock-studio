using Mariliz.Core.Discovery;

namespace Mariliz.Application.Discovery;

internal static class CiDetector
{
    internal static void Detect(IReadOnlyList<string> all, List<ToolingFinding> tooling)
    {
        var workflows = all.Where(p => p.StartsWith(".github/workflows/", StringComparison.Ordinal) && (p.EndsWith(".yml") || p.EndsWith(".yaml"))).ToList();
        if (workflows.Count > 0) tooling.Add(new ToolingFinding("ci", "GitHub Actions", null, workflows.Take(8).Select(w => new Evidence(w)).ToList()));
        foreach (var (file, name) in new[] { ("azure-pipelines.yml", "Azure Pipelines"), (".gitlab-ci.yml", "GitLab CI"), ("Jenkinsfile", "Jenkins") })
            if (all.Contains(file)) tooling.Add(new ToolingFinding("ci", name, null, [new Evidence(file)]));
    }
}
