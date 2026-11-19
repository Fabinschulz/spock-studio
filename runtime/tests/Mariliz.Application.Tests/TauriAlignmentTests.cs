using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mariliz.Application.Tests;

// MV-9: o crate tauri, a CLI e a API do npm sobem juntos. A CLI do Tauri 2 recusa o build com versões descasadas, e isso só
// aparecia no build do instalador; aqui aparece na suíte. Os plugins precisam estar na mesma versão maior do crate.
public partial class TauriAlignmentTests
{
    private static string Read(string relative) => File.ReadAllText(Path.Combine(ContractFiles.Root, relative));

    private static string Locked(string lockfile, string crate) =>
        Regex.Match(lockfile, $"(?m)^name = \"{Regex.Escape(crate)}\"\\r?\\nversion = \"([^\"]+)\"").Groups[1].Value;

    private static string Npm(string manifest, string package)
    {
        using var doc = JsonDocument.Parse(Read(manifest));
        foreach (var section in new[] { "dependencies", "devDependencies" })
            if (doc.RootElement.TryGetProperty(section, out var deps) && deps.TryGetProperty(package, out var version))
                return version.GetString()!;
        throw new InvalidOperationException($"{package} não está em {manifest}.");
    }

    private static string MajorMinor(string version) => ExactVersion().Match(version) is { Success: true } match
        ? $"{match.Groups[1].Value}.{match.Groups[2].Value}"
        : throw new InvalidOperationException($"{version} não é uma versão exata (o Spock fixa as do Tauri sem faixa).");

    [Fact]
    public void Crate_cli_e_api_do_tauri_estao_na_mesma_versao_maior_e_menor()
    {
        var lockfile = Read("desktop/host/src-tauri/Cargo.lock");
        var crate = Locked(lockfile, "tauri");
        var cli = Npm("desktop/host/package.json", "@tauri-apps/cli");
        var api = Npm("desktop/ui/package.json", "@tauri-apps/api");

        Assert.Equal(MajorMinor(crate), MajorMinor(api));
        Assert.Equal(MajorMinor(crate), MajorMinor(cli));
    }

    [Fact]
    public void Plugins_e_tauri_build_seguem_a_versao_maior_do_crate()
    {
        var lockfile = Read("desktop/host/src-tauri/Cargo.lock");
        var major = Locked(lockfile, "tauri").Split('.')[0];

        foreach (var crate in new[] { "tauri-build", "tauri-plugin-dialog", "tauri-plugin-shell", "tauri-plugin-single-instance" })
            Assert.True(Locked(lockfile, crate).Split('.')[0] == major, $"{crate} {Locked(lockfile, crate)} fora da versão maior {major} do tauri.");
    }

    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)$")]
    private static partial Regex ExactVersion();
}
