using System.Security.Cryptography;
using Mariliz.Application.Integrations;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// Os dublês provam as regras do código; só este prova que os comandos reais se comportam como os dublês supõem. Mexe no cofre da pessoa, então só roda com MARILIZ_LIVE_VAULT=1 e apaga o que criou; fora disso aparece como ignorado, nunca como aprovado.
[AttributeUsage(AttributeTargets.Method)]
public sealed class UnixVaultFactAttribute : FactAttribute
{
    public UnixVaultFactAttribute()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) Skip = "Só roda no macOS e no Linux: usa o cofre do sistema.";
        else if (Environment.GetEnvironmentVariable("MARILIZ_LIVE_VAULT") != "1") Skip = "Mexe no cofre real: defina MARILIZ_LIVE_VAULT=1 para rodar.";
    }
}

public class UnixVaultRealityTests
{
    private static string NewId() => "conn-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();

    private static ISecretVault Real() => SecretVaults.For(SecretVaults.CurrentPlatform(), Path.GetTempPath(), new SystemProcessRunner());

    [UnixVaultFact]
    public void O_cofre_do_sistema_responde_ou_diz_o_que_falta()
    {
        var vault = Real();

        Assert.True(vault.Available, $"{vault.Name} não está disponível: {vault.UnavailableHint}");
        Assert.Null(vault.UnavailableHint);
    }

    [UnixVaultFact]
    public void Guardar_ler_e_remover_no_cofre_real_devolve_o_mesmo_segredo()
    {
        var vault = Real();
        Assert.True(vault.Available, $"{vault.Name} não está disponível: {vault.UnavailableHint}");
        var id = NewId();
        const string secret = "ghp_a b\"c'd$e`f\\g\nlinha2 ção ✓ -w x";
        try
        {
            vault.Store(id, secret);
            Assert.True(vault.TryRead(id, out var read));
            Assert.Equal(secret, read);

            vault.Store(id, "segundo-segredo-5678");
            Assert.True(vault.TryRead(id, out var replaced));
            Assert.Equal("segundo-segredo-5678", replaced);
        }
        finally
        {
            vault.Remove(id);
        }

        Assert.False(vault.TryRead(id, out _));
    }

    [UnixVaultFact]
    public void Um_identificador_que_nunca_foi_guardado_nao_existe_no_cofre_real()
    {
        var vault = Real();
        Assert.True(vault.Available, $"{vault.Name} não está disponível: {vault.UnavailableHint}");

        Assert.False(vault.TryRead(NewId(), out var secret));
        Assert.Equal("", secret);
    }
}
