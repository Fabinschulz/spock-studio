using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public sealed class ExecutableLocatorTests : IDisposable
{
    private readonly string _first = Directory.CreateTempSubdirectory("locator-a").FullName;
    private readonly string _second = Directory.CreateTempSubdirectory("locator-b").FullName;

    public void Dispose()
    {
        Directory.Delete(_first, true);
        Directory.Delete(_second, true);
    }

    private static string Touch(string directory, string file)
    {
        var path = Path.Combine(directory, file);
        File.WriteAllText(path, "");
        return path;
    }

    [Fact]
    public void Acha_o_executavel_numa_das_pastas()
    {
        var expected = Touch(_second, "claude.cmd");

        var found = new ExecutableLocator([_first, _second], [".exe", ".cmd"]).Find("claude");

        Assert.Equal(expected, found);
    }

    [Fact]
    public void A_pasta_que_vem_primeiro_vence()
    {
        var expected = Touch(_first, "codex.cmd");
        Touch(_second, "codex.cmd");

        Assert.Equal(expected, new ExecutableLocator([_first, _second], [".cmd"]).Find("codex"));
    }

    [Fact]
    public void Na_mesma_pasta_a_ordem_das_extensoes_decide()
    {
        var expected = Touch(_first, "gemini.exe");
        Touch(_first, "gemini.cmd");

        Assert.Equal(expected, new ExecutableLocator([_first], [".exe", ".cmd"]).Find("gemini"));
    }

    [Fact]
    public void O_script_sem_extensao_ao_lado_do_cmd_nao_conta_no_Windows()
    {
        Touch(_first, "claude");

        Assert.Null(new ExecutableLocator([_first], [".exe", ".cmd"]).Find("claude"));
    }

    [Fact]
    public void Sem_o_executavel_devolve_nulo()
    {
        Assert.Null(new ExecutableLocator([_first, _second], [".exe"]).Find("cursor-agent"));
    }

    [Fact]
    public void Fora_do_Windows_o_nome_puro_basta()
    {
        var expected = Touch(_first, "codex");

        Assert.Equal(expected, new ExecutableLocator([_first], [""]).Find("codex"));
    }
}
