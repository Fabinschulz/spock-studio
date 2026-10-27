using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Core.Plan;
using Mariliz.Core.Text;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests.Support;

/// <summary>
/// Arquivos reais do projeto, mas com o "package.json mais próximo acima da raiz" sob controle do teste. Sem isso, o resultado
/// dependeria de haver (ou não) um package.json em alguma pasta acima da pasta temporária do computador que roda os testes.
/// </summary>
internal sealed class TestFiles(IProjectFiles inner, string? ancestorWithPackageJson) : IProjectFiles
{
    public string RootName => inner.RootName;
    public bool FileExists(string path) => inner.FileExists(path);
    public bool DirectoryExists(string path) => inner.DirectoryExists(path);
    public string? NearestAncestorFolderWith(string fileName) => fileName == "package.json" ? ancestorWithPackageJson : null;
    public byte[]? ReadBytes(string path) => inner.ReadBytes(path);
    public string? ReadText(string path) => inner.ReadText(path);
    public IReadOnlyList<string> EnumerateFiles(string directory, int maxDepth = 8) => inner.EnumerateFiles(directory, maxDepth);
    public void WriteAtomic(string path, byte[] content, WriteScope scope = WriteScope.Managed) => inner.WriteAtomic(path, content, scope);
    public void Delete(string path, WriteScope scope = WriteScope.Managed) => inner.Delete(path, scope);
    public void PruneEmptyDirectories(string directory) => inner.PruneEmptyDirectories(directory);
}

internal sealed class TestFilesFactory(string? ancestorWithPackageJson = null) : IProjectFilesFactory
{
    public IProjectFiles Open(string authorizedRoot) => new TestFiles(new LocalProjectFiles(authorizedRoot), ancestorWithPackageJson);
}

/// <summary>Projeto sintético em pasta temporária. Nada aqui é cópia de projeto de cliente.</summary>
internal sealed class TempProject : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("mariliz-proj-").FullName;
    public string DataDir { get; } = Directory.CreateTempSubdirectory("mariliz-data-").FullName;

    public void Write(string relative, string content)
    {
        var full = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content, new UTF8Encoding(false));
    }

    public string? Read(string relative)
    {
        var full = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }

    public bool Exists(string relative) => File.Exists(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)))
        || Directory.Exists(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));

    public string? Hash(string relative) => Read(relative) is { } t ? Hashing.Sha256(t.Replace("\r\n", "\n")) : null;

    /// <summary>Hash de todos os arquivos do projeto, para provar que nada mudou.</summary>
    public string Fingerprint()
    {
        var sb = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar)).Order(StringComparer.Ordinal))
            sb.Append(Path.GetRelativePath(Root, file)).Append('=').Append(Hashing.Sha256(File.ReadAllBytes(file))).Append('\n');
        return sb.ToString();
    }

    public void Git(params string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in new[] { "-c", "user.name=teste", "-c", "user.email=teste@example.com" }.Concat(args)) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        p.WaitForExit();
    }

    public static TempProject Dotnet(bool withTests = true)
    {
        var p = new TempProject();
        p.Write("backend/App.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        p.Write("backend/src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        if (withTests)
            p.Write("backend/tests/App.Tests/App.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"xunit\" Version=\"2.9.3\" /></ItemGroup></Project>");
        return p;
    }

    public static TempProject Next()
    {
        var p = new TempProject();
        p.Write("web/package.json", "{\"name\":\"web\",\"packageManager\":\"pnpm@9.1.2\",\"dependencies\":{\"next\":\"^16.0.1\",\"react\":\"^19.0.0\"},\"devDependencies\":{\"typescript\":\"~5.6.0\"},\"scripts\":{\"build\":\"next build\",\"test\":\"vitest run\",\"lint\":\"eslint .\"}}");
        p.Write("web/pnpm-lock.yaml", "lockfileVersion: '9.0'\n");
        p.Write("web/tsconfig.json", "{}");
        return p;
    }

    public void Dispose()
    {
        foreach (var dir in new[] { Root, DataDir })
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
                Directory.Delete(dir, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* limpeza best-effort */ }
        }
    }
}

/// <summary>Skill TLC sintética (instalador simulado): mesmos nomes de arquivo e hash que o instalador real calcula.</summary>
internal static class TlcFixture
{
    /// <summary>Arquivos que a 4.0.0 real removeu em relação à 3.3.0 e os que ela acrescentou (observado na instalação oficial).</summary>
    private static readonly string[] RemovedIn4 = ["references/code-analysis.md", "references/context-limits.md", "scripts/validate_state.py"];

    public static void Install(TempProject project, string version = "4.0.0")
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        string Frontmatter() => $"---\nname: tlc-spec-driven\ndescription: test double of the TLC skill\nlicense: CC-BY-4.0\nmetadata:\n  author: test\n  version: {version}\n---\n\n# Test double\n";
        files["SKILL.md"] = Encoding.UTF8.GetBytes(Frontmatter());
        foreach (var r in new[] { "code-analysis", "coding-principles", "context-limits", "design", "discuss", "implement", "lessons", "memory", "specify", "sub-agents", "tasks", "validate" })
            files[$"references/{r}.md"] = Encoding.UTF8.GetBytes($"# {r}\n");
        files["scripts/check_commit.py"] = Encoding.UTF8.GetBytes(
            "import argparse, re, sys\np = argparse.ArgumentParser(); p.add_argument('--message'); a = p.parse_args()\n"
            + "sys.exit(0 if re.match(r'^(feat|fix|refactor|docs|test|style|perf|build|ci|chore)(\\([^)]+\\))?!?: .+', a.message or '') else 1)\n");
        files["scripts/lessons.py"] = Encoding.UTF8.GetBytes("import sys\nif len(sys.argv) > 1 and sys.argv[1] == 'selftest':\n    print('selftest_norm: ok')\n    sys.exit(0)\nsys.exit(2)\n");
        foreach (var s in new[] { "validate_spec", "validate_state", "validate_tasks" })
            files[$"scripts/{s}.py"] = Encoding.UTF8.GetBytes("print('ok')\n");

        if (version.StartsWith("4.", StringComparison.Ordinal))
        {
            foreach (var removed in RemovedIn4) files.Remove(removed);
            foreach (var added in new[] { "references/verify-screens.md", "scripts/check_proofs.py", "scripts/mutate.py", "scripts/selftest.py", "scripts/validate_verification.py" })
                files[added] = Encoding.UTF8.GetBytes("# " + added + "\n");
        }

        foreach (var (rel, bytes) in files)
        {
            var full = Path.Combine(project.Root, ".claude", "skills", "tlc-spec-driven", rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, bytes);
        }
        var hash = Hashing.SkillContentHash(files);
        project.Write(".claude/skills/tlc-spec-driven/.skill-meta.json", $"{{\"contentHash\":\"{hash}\",\"downloadedAt\":1}}");
        project.Write(".agents/.skill-lock.json", $"{{\"version\":2,\"skills\":{{\"tlc-spec-driven\":{{\"name\":\"tlc-spec-driven\",\"source\":\"local\",\"contentHash\":\"{hash}\",\"installedAt\":\"x\",\"updatedAt\":\"x\",\"agents\":[\"claude-code\"],\"method\":\"copy\",\"global\":false}}}}}}");
    }
}

/// <summary>Executa processos reais, exceto os interceptados. O instalador (npx) é sempre um dublê de teste.</summary>
internal sealed class InterceptingRunner(Func<ProcessSpec, CancellationToken, Task<ProcessResult?>>? intercept = null) : IProcessRunner
{
    private readonly SystemProcessRunner _real = new();
    public List<ProcessSpec> Calls { get; } = [];

    public async Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct)
    {
        lock (Calls) Calls.Add(spec);
        if (intercept is not null && await intercept(spec, ct) is { } result) return result;
        if (spec.Executable is "npx" or "npm") throw new InvalidOperationException("O instalador real não pode rodar nos testes.");
        return await _real.RunAsync(spec, ct);
    }
}

/// <summary>Toda a aplicação ligada para um projeto, como o motor faz.</summary>
internal sealed class Journey
{
    public ConfigureProjectService Service { get; }
    public RunTracker Tracker { get; }
    public FileRunStore Store { get; }
    public string ProjectId { get; }
    public InterceptingRunner Runner { get; }
    public TempProject Project { get; }
    public List<(string Type, JsonObject Data)> Events { get; } = [];

    public Journey(TempProject project, Func<ProcessSpec, CancellationToken, Task<ProcessResult?>>? intercept = null, IProjectFilesFactory? files = null, IClock? clock = null)
    {
        Project = project;
        Runner = new InterceptingRunner(intercept);
        Store = new FileRunStore(project.DataDir);
        Tracker = new RunTracker((_, _, type, data) => { lock (Events) Events.Add((type, data)); });
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        ProjectId = workspace.Authorize(project.Root).Project!.ProjectId;
        Service = new ConfigureProjectService(workspace, files ?? new TestFilesFactory(), Runner, Store, clock ?? new SystemClock(), Tracker);
    }

    public static Func<ProcessSpec, CancellationToken, Task<ProcessResult?>> FakeInstaller(TempProject project, int exitCode = 0) => (spec, _) =>
    {
        if (spec.Executable != "npx") return Task.FromResult<ProcessResult?>(null);
        if (exitCode == 0) TlcFixture.Install(project);
        return Task.FromResult<ProcessResult?>(new ProcessResult(exitCode, "", exitCode == 0 ? "" : "boom", false, false));
    };

    public Task<ConfigurationPlan> PlanAsync(string? env = "claude-code", bool force = false) =>
        Service.PlanAsync(ProjectId, new PlanOptions(env, force), CancellationToken.None);

    public static ApplyApprovals ApproveAll(ConfigurationPlan plan, ConflictResolution? conflicts = null)
    {
        var changes = plan.Changes.Where(c => c.Action is ChangeAction.Create or ChangeAction.Update or ChangeAction.Remove).Select(c => c.Id).ToList();
        var resolutions = new Dictionary<string, ConflictResolution>();
        if (conflicts is { } resolution)
            foreach (var c in plan.Changes.Where(c => c.Action == ChangeAction.Conflict))
            {
                resolutions[c.Id] = resolution;
                if (resolution == ConflictResolution.Overwrite) changes.Add(c.Id);
            }
        return new ApplyApprovals(changes, plan.Commands.Select(c => c.Id).ToList(), resolutions);
    }

    public async Task<JsonObject> ApplyAsync(ConfigurationPlan plan, ApplyApprovals? approvals = null)
    {
        var run = Service.StartApply(ProjectId, plan.PlanId, approvals ?? ApproveAll(plan));
        await run.Completion!;
        return run.Result!;
    }

    public async Task<JsonObject> ValidateAsync()
    {
        var run = Service.StartValidate(ProjectId);
        await run.Completion!;
        return run.Result!;
    }

    public async Task<JsonObject> RevertAsync(string runId)
    {
        var run = Service.StartRevert(ProjectId, runId);
        await run.Completion!;
        return run.Result!;
    }

    public static string Status(JsonObject result, string evalId) =>
        result["validation"]!["results"]!.AsArray().First(r => r!["id"]!.GetValue<string>() == evalId)!["status"]!.GetValue<string>();
}
