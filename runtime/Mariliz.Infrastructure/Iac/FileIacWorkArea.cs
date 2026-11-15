using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mariliz.Application.Iac;
using Mariliz.Application.Integrations;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure.Iac;

// <dados>/projects/<id>/iac/: operations.json (histórico) e operations/<op>/ (cópia da configuração, .terraform da
// operação e o arquivo de plano). O plano guarda valores de atributo em texto puro: fica só aqui, nunca sai pelo protocolo.
public sealed class FileIacWorkArea(string dataRoot) : IIacWorkArea
{
    public const int MaxHistory = 50;
    private const int MaxFiles = 5000;
    private const long MaxFileBytes = 20L * 1024 * 1024;
    private const long MaxTotalBytes = 200L * 1024 * 1024;

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".terraform", ".git", "node_modules", ".venv", "bin", "obj"
    };

    private readonly object _gate = new();

    public string OperationDirectory(string projectId, string operationId) =>
        Path.Combine(ProjectDirectory(projectId), "operations", operationId);

    public IacSnapshot Snapshot(string projectRoot, string iacRoot, string destination, bool includeLocalState)
    {
        var source = Path.GetFullPath(Path.Combine(projectRoot, iacRoot));
        if (!source.StartsWith(Path.GetFullPath(projectRoot), StringComparison.OrdinalIgnoreCase)) throw IacErrors.FolderUnknown(iacRoot);
        Directory.CreateDirectory(destination);

        var copied = 0;
        long total = 0;
        var count = 0;
        var localState = false;
        foreach (var file in Files(source))
        {
            var relative = Path.GetRelativePath(source, file).Replace('\\', '/');
            var state = IsState(relative);
            if (state && !includeLocalState) continue;
            if (IsPlanOrLog(relative)) continue;
            var length = new FileInfo(file).Length;
            if (length > MaxFileBytes || (total += length) > MaxTotalBytes || ++count > MaxFiles) throw IacErrors.ConfigurationTooLarge();

            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
            if (state) localState = true;
            else copied++;
        }

        return new IacSnapshot(destination, ConfigHash(destination), copied, localState);
    }

    public string ConfigHash(string directory)
    {
        var hashed = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (Directory.Exists(directory))
            foreach (var file in Files(directory))
            {
                var relative = Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/');
                // O lock de providers fica fora: o init da ferramenta pode reescrevê-lo na cópia, e o plano já registra os providers.
                if (IsState(relative) || IsPlanOrLog(relative) || relative.EndsWith(".terraform.lock.hcl", StringComparison.Ordinal)) continue;
                hashed[relative] = HashFile(file);
            }
        var manifest = string.Join('\n', hashed.Select(f => $"{f.Key}\n{f.Value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
    }

    public void DiscardLocalState(string snapshotDirectory)
    {
        if (!Directory.Exists(snapshotDirectory)) return;
        foreach (var file in Directory.EnumerateFiles(snapshotDirectory, "*", SearchOption.AllDirectories)
                     .Where(f => IsState(Path.GetRelativePath(snapshotDirectory, f).Replace('\\', '/'))))
            File.Delete(file);
    }

    public void DeleteOperation(string projectId, string operationId)
    {
        var directory = OperationDirectory(projectId, operationId);
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    public void DiscardData(string dataDirectory)
    {
        if (Directory.Exists(dataDirectory)) Directory.Delete(dataDirectory, recursive: true);
    }

    public string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public IReadOnlyList<IacOperation> History(string projectId)
    {
        lock (_gate) return Read(projectId);
    }

    // Mais recente primeiro; o que passa do limite sai do histórico junto com a pasta (e o plano) dele.
    public void Save(IacOperation operation)
    {
        lock (_gate)
        {
            var list = Read(operation.ProjectId).Where(o => o.Id != operation.Id).Prepend(operation).ToList();
            foreach (var old in list.Skip(MaxHistory)) DeleteOperation(operation.ProjectId, old.Id);
            AtomicFile.Write(HistoryPath(operation.ProjectId), JsonSerializer.Serialize(list.Take(MaxHistory).ToList(), IntegrationJson.Options));
        }
    }

    private List<IacOperation> Read(string projectId) =>
        AtomicFile.ReadJson<List<IacOperation>>(HistoryPath(projectId), IntegrationJson.Options) ?? [];

    private string ProjectDirectory(string projectId)
    {
        if (projectId.Length == 0 || projectId.IndexOfAny(['/', '\\', '.']) >= 0) throw new ArgumentException("Projeto inválido.", nameof(projectId));
        return Path.Combine(dataRoot, "projects", projectId, "iac");
    }

    private string HistoryPath(string projectId) => Path.Combine(ProjectDirectory(projectId), "operations.json");

    private static bool IsState(string relative)
    {
        var name = relative[(relative.LastIndexOf('/') + 1)..];
        return name.StartsWith("terraform.tfstate", StringComparison.Ordinal) || relative.Contains("terraform.tfstate.d/", StringComparison.Ordinal);
    }

    private static bool IsPlanOrLog(string relative) =>
        relative.EndsWith(".tfplan", StringComparison.OrdinalIgnoreCase) || relative.EndsWith("crash.log", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> Files(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(directory))
                if (new FileInfo(file).LinkTarget is null) yield return file;
            foreach (var child in Directory.EnumerateDirectories(directory))
                if (!SkippedDirectories.Contains(Path.GetFileName(child)) && new DirectoryInfo(child).LinkTarget is null) pending.Push(child);
        }
    }
}
