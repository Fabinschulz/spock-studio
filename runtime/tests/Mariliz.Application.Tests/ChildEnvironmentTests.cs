using System.Text.RegularExpressions;
using Mariliz.Application.AiTools;
using Mariliz.Application.Ports;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// H0-5 do estudo de 16/11 (R-10): os filhos do motor herdavam o ambiente inteiro, segredos incluídos.
public sealed class ChildEnvironmentTests
{
    private static Dictionary<string, string?> Inherited(params string[] names) =>
        names.ToDictionary(name => name, _ => (string?)"valor", StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Variaveis_com_cara_de_segredo_ficam_de_fora_e_o_resto_passa()
    {
        var env = Inherited("GITHUB_TOKEN", "OPENAI_API_KEY", "AWS_SECRET_ACCESS_KEY", "AWS_ACCESS_KEY_ID", "DB_PASSWORD",
            "Azure_Client_Secret", "ConnectionStrings__Default", "GOOGLE_APPLICATION_CREDENTIALS", "PATH", "JAVA_HOME", "USERPROFILE");

        ChildEnvironment.Apply(env, inheritedSecrets: null, explicitValues: null);

        Assert.Equal(["PATH", "JAVA_HOME", "USERPROFILE"], env.Keys);
    }

    [Fact]
    public void So_passa_o_segredo_que_quem_inicia_o_processo_nomeia()
    {
        var env = Inherited("ANTHROPIC_API_KEY", "GITHUB_TOKEN", "PATH");

        ChildEnvironment.Apply(env, ["anthropic_api_key"], explicitValues: null);

        Assert.Equal(["ANTHROPIC_API_KEY", "PATH"], env.Keys);
    }

    [Fact]
    public void O_valor_que_o_motor_define_vale_mesmo_com_cara_de_segredo()
    {
        var env = Inherited("MARILIZ_AI_TOKEN", "PATH");

        ChildEnvironment.Apply(env, inheritedSecrets: null, new Dictionary<string, string> { ["MARILIZ_AI_TOKEN"] = "do-motor" });

        Assert.Equal("do-motor", env["MARILIZ_AI_TOKEN"]);
    }

    [Fact]
    public async Task O_segredo_do_motor_nao_chega_ao_processo_filho()
    {
        System.Environment.SetEnvironmentVariable("SPOCK_TEST_SECRET", "vazou");
        System.Environment.SetEnvironmentVariable("SPOCK_TEST_PLAIN", "chegou");
        try
        {
            var (shell, args) = OperatingSystem.IsWindows() ? ("cmd.exe", new[] { "/c", "set" }) : ("/usr/bin/env", Array.Empty<string>());

            var result = await new SystemProcessRunner().RunAsync(
                new ProcessSpec(shell, args, Path.GetTempPath(), TimeSpan.FromSeconds(30)), CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("SPOCK_TEST_PLAIN=chegou", result.StandardOutput);
            Assert.DoesNotContain("SPOCK_TEST_SECRET", result.StandardOutput);
            Assert.DoesNotContain("vazou", result.StandardOutput);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("SPOCK_TEST_SECRET", null);
            System.Environment.SetEnvironmentVariable("SPOCK_TEST_PLAIN", null);
        }
    }

    [Fact]
    public void A_inteligencia_em_python_recebe_o_token_do_motor_e_nenhum_segredo_herdado()
    {
        var env = Inherited("GITHUB_TOKEN", "OPENAI_API_KEY", "PATH");

        ChildEnvironment.Apply(env, inheritedSecrets: null, LoopbackAiRuntime.PythonEnvironment("abc"));

        Assert.Equal("abc", env["MARILIZ_AI_TOKEN"]);
        Assert.DoesNotContain("GITHUB_TOKEN", env.Keys);
        Assert.DoesNotContain("OPENAI_API_KEY", env.Keys);
    }

    [Fact]
    public void Cada_cli_de_ia_herda_so_as_variaveis_do_proprio_login()
    {
        foreach (var tool in AiToolCatalog.All.Where(tool => tool.ReadOnly is not null))
        {
            Assert.NotEmpty(tool.ReadOnly!.Credentials);
            Assert.All(tool.ReadOnly.Credentials, name => Assert.True(ChildEnvironment.IsSecret(name), $"{tool.Id}: {name} já passaria"));
        }
        Assert.DoesNotContain("OPENAI_API_KEY", AiToolCatalog.All.Single(tool => tool.Id == "claude-code").ReadOnly!.Credentials);
    }

    [Fact]
    public void A_regra_e_a_mesma_do_terminal_do_host()
    {
        var rust = File.ReadAllText(Path.Combine(ContractFiles.Root, "desktop", "host", "src-tauri", "src", "terminal", "env.rs"));
        var block = Regex.Match(rust, @"SECRET_MARKERS: &\[&str\] = &\[(?<body>.*?)\];", RegexOptions.Singleline).Groups["body"].Value;
        var markers = Regex.Matches(block, "\"(?<m>[A-Z_]+)\"").Select(match => match.Groups["m"].Value).ToList();

        Assert.NotEmpty(markers);
        Assert.Equal(markers.Order(), ChildEnvironment.SecretMarkers.Order());
    }

    [Fact]
    public void Todo_processo_que_o_motor_inicia_passa_pelo_filtro()
    {
        var sources = Directory.GetFiles(Path.Combine(ContractFiles.Root, "runtime"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        var offenders = sources
            .Where(path => File.ReadAllText(path) is var text && text.Contains("new ProcessStartInfo(") && !text.Contains("ChildEnvironment.Apply("))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0, "Processo iniciado sem o filtro de ambiente: " + string.Join(", ", offenders));
    }
}
