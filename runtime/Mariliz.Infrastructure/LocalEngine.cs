using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Infrastructure;

/// <summary>
/// Composição dos casos de uso sobre o disco local, comum ao motor (sidecar) e à CLI. As diferenças entre os dois ficam
/// explícitas no <see cref="Create"/>: só o motor lembra as pastas autorizadas (na CLI a pasta passada já é a
/// autorização) e cada um decide para onde vão os eventos.
/// </summary>
public sealed class LocalEngine
{
    private LocalEngine(string dataRoot, Action<string, int, string, JsonObject> emit, bool rememberWorkspaces)
    {
        DataRoot = dataRoot;
        Store = new FileRunStore(dataRoot);
        Workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), rememberWorkspaces ? new FileWorkspaceRegistry(dataRoot) : null);
        Tracker = new RunTracker(emit);
        Settings = new FileModelSettings(dataRoot);
        Configure = new ConfigureProjectService(Workspace, Files, Runner, Store, Clock, Tracker, Settings);
        Runs = new RunQueries(Tracker, Store);
    }

    public string DataRoot { get; }
    public FileRunStore Store { get; }
    public WorkspaceAuthorizer Workspace { get; }
    public RunTracker Tracker { get; }
    public FileModelSettings Settings { get; }
    public ConfigureProjectService Configure { get; }
    public RunQueries Runs { get; }
    public IProcessRunner Runner { get; } = new SystemProcessRunner();
    public IProjectFilesFactory Files { get; } = new LocalProjectFilesFactory();
    public IClock Clock { get; } = new SystemClock();

    public static LocalEngine Create(string dataRoot, Action<string, int, string, JsonObject> emit, bool rememberWorkspaces) =>
        new(dataRoot, emit, rememberWorkspaces);

    /// <summary>UTF-8 sem BOM no console e Ctrl+C como cancelamento, em vez de encerrar o processo no meio de uma escrita.</summary>
    public static CancellationTokenSource ConfigureConsole()
    {
        var utf8 = new UTF8Encoding(false);
        Console.InputEncoding = utf8;
        Console.OutputEncoding = utf8;
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        return cts;
    }
}
