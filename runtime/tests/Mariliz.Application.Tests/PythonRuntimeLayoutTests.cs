using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class PythonRuntimeLayoutTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mariliz-python-" + Guid.NewGuid().ToString("N"));

    public PythonRuntimeLayoutTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Interpretador_do_instalador_vence_o_do_ambiente()
    {
        var packaged = Touch(PackagedName());
        var bundled = Touch(BundledRelative());
        var onPath = Touch(PathExecutableName());
        var path = Path.GetDirectoryName(onPath)!;

        var found = PythonRuntimeLayout.Candidates(Path.Combine(_root, "app"), overridePath: null, packaged, path).ToList();

        Assert.Equal([packaged], found);
        Assert.NotEqual(bundled, found[0]);
    }

    [Fact]
    public void Sem_pacote_usa_o_python_ao_lado_do_executavel_e_nao_o_do_ambiente()
    {
        var bundled = Touch(BundledRelative());
        var onPath = Touch(PathExecutableName());

        var found = PythonRuntimeLayout.Candidates(Path.Combine(_root, "app"), null, packagedPath: null, Path.GetDirectoryName(onPath)).ToList();

        Assert.Equal([bundled], found);
    }

    [Fact]
    public void Sem_nada_embarcado_usa_o_ambiente_de_desenvolvimento()
    {
        var onPath = Touch(PathExecutableName());

        var found = PythonRuntimeLayout.Candidates(Path.Combine(_root, "app"), null, null, Path.GetDirectoryName(onPath)).ToList();

        Assert.Equal([onPath], found);
    }

    private static string[] PackagedName() =>
        OperatingSystem.IsWindows() ? ["pack", "python.exe"] : ["pack", "python3"];

    private static string[] BundledRelative() =>
        OperatingSystem.IsWindows() ? ["app", "python", "python.exe"] : ["app", "python", "bin", "python3"];

    private static string[] PathExecutableName() =>
        OperatingSystem.IsWindows() ? ["tools", "python.exe"] : ["tools", "python3"];

    private string Touch(params string[] parts)
    {
        var path = Path.Combine(new[] { _root }.Concat(parts).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, []);
        return path;
    }
}
