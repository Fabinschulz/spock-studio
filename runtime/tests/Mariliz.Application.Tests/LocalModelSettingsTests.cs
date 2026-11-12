using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Models;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Text;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

/// <summary>
/// A chave do provedor de modelo no cofre, e a migração de quem já a tinha em <c>model.json</c> em texto puro. O que mais
/// importa: a chave nunca se perde (o arquivo antigo só some depois de o cofre devolver a mesma chave) e nunca fica em
/// texto puro onde há cofre.
/// </summary>
public class LocalModelSettingsTests
{
    private const string Key = "sk-test-cofre-fase-a-7c1d9e";
    private const string Other = "sk-test-outra-chave-fase-a-4b2e";

    private static string LegacyFile(TempProject project) => Path.Combine(project.DataDir, "model.json");

    private static void WriteLegacy(TempProject project, string key)
    {
        Directory.CreateDirectory(project.DataDir);
        File.WriteAllText(LegacyFile(project), new JsonObject { ["provider"] = "openai", ["key"] = key }.ToJsonString());
    }

    [Fact]
    public void O_identificador_reservado_tem_o_formato_que_o_cofre_aceita()
    {
        Assert.True(Ids.Is(ReservedSecretIds.ModelKey, "conn-"));
    }

    [Fact]
    public void Com_cofre_a_chave_vai_para_o_cofre_e_nao_para_um_arquivo_em_texto_puro()
    {
        using var project = TempProject.Dotnet();
        var vault = new MemoryVault();
        var settings = new LocalModelSettings(project.DataDir, vault);

        settings.Save("openai", Key);

        Assert.Equal(Key, vault.Secrets[ReservedSecretIds.ModelKey]);
        Assert.False(File.Exists(LegacyFile(project)));
        Assert.Equal(new ModelStatus(true, "openai"), settings.Status());
        Assert.True(settings.TryGetKey(out var read));
        Assert.Equal(Key, read);
    }

    [Fact]
    public void Salvar_uma_chave_nova_apaga_um_arquivo_antigo_que_ainda_existisse()
    {
        using var project = TempProject.Dotnet();
        WriteLegacy(project, Other);
        var vault = new MemoryVault { Available = true };
        var settings = new LocalModelSettings(project.DataDir, vault);

        settings.Save("openai", Key);

        Assert.False(File.Exists(LegacyFile(project)));
        Assert.True(settings.TryGetKey(out var read));
        Assert.Equal(Key, read);
    }

    [Fact]
    public void Quem_ja_tinha_a_chave_em_arquivo_e_migrado_no_primeiro_uso_e_o_arquivo_some()
    {
        using var project = TempProject.Dotnet();
        WriteLegacy(project, Key);
        var vault = new MemoryVault();
        var settings = new LocalModelSettings(project.DataDir, vault);

        var status = settings.Status();

        Assert.Equal(new ModelStatus(true, "openai"), status);
        Assert.Equal(Key, vault.Secrets[ReservedSecretIds.ModelKey]);
        Assert.False(File.Exists(LegacyFile(project)));
        Assert.True(settings.TryGetKey(out var read));
        Assert.Equal(Key, read);
    }

    [Fact]
    public void A_migracao_tambem_acontece_quando_o_primeiro_uso_e_ler_a_chave()
    {
        using var project = TempProject.Dotnet();
        WriteLegacy(project, Key);
        var vault = new MemoryVault();
        var settings = new LocalModelSettings(project.DataDir, vault);

        Assert.True(settings.TryGetKey(out var read));

        Assert.Equal(Key, read);
        Assert.Equal(Key, vault.Secrets[ReservedSecretIds.ModelKey]);
        Assert.False(File.Exists(LegacyFile(project)));
    }

    [Fact]
    public void Se_o_cofre_nao_devolve_a_mesma_chave_o_arquivo_fica_e_a_chave_continua_valendo()
    {
        using var project = TempProject.Dotnet();
        WriteLegacy(project, Key);
        var settings = new LocalModelSettings(project.DataDir, new ForgetfulVault());

        Assert.Equal(new ModelStatus(true, "openai"), settings.Status());

        Assert.True(File.Exists(LegacyFile(project)));
        Assert.True(settings.TryGetKey(out var read));
        Assert.Equal(Key, read);
    }

    [Fact]
    public void Se_o_cofre_falha_na_migracao_nada_propaga_o_arquivo_fica_e_a_chave_continua_valendo()
    {
        using var project = TempProject.Dotnet();
        WriteLegacy(project, Key);
        var settings = new LocalModelSettings(project.DataDir, new ThrowingVault());

        var status = settings.Status();

        Assert.Equal(new ModelStatus(true, "openai"), status);
        Assert.True(File.Exists(LegacyFile(project)));
        Assert.True(settings.TryGetKey(out var read));
        Assert.Equal(Key, read);
    }

    [Fact]
    public void Com_outra_chave_ja_no_cofre_o_arquivo_fica_e_vale_a_do_cofre()
    {
        using var project = TempProject.Dotnet();
        WriteLegacy(project, Other);
        var vault = new MemoryVault();
        vault.Secrets[ReservedSecretIds.ModelKey] = Key;
        var settings = new LocalModelSettings(project.DataDir, vault);

        Assert.True(settings.TryGetKey(out var read));

        // Qual das duas a pessoa queria não é decisão deste código: nada é apagado.
        Assert.Equal(Key, read);
        Assert.True(File.Exists(LegacyFile(project)));
        Assert.Equal(Key, vault.Secrets[ReservedSecretIds.ModelKey]);
    }

    [Fact]
    public void Se_o_cofre_falha_ao_salvar_a_chave_nao_cai_em_texto_puro()
    {
        using var project = TempProject.Dotnet();
        var settings = new LocalModelSettings(project.DataDir, new ThrowingVault());

        var error = Assert.Throws<EngineException>(() => settings.Save("openai", Key));

        Assert.Equal("MODEL_KEY_STORE_FAILED", error.Code);
        Assert.DoesNotContain(Key, error.Message);
        Assert.False(File.Exists(LegacyFile(project)));
        Assert.False(settings.Status().Configured);
    }

    [Fact]
    public void Se_o_cofre_nao_devolve_a_chave_salva_a_gravacao_falha_em_vez_de_fingir()
    {
        using var project = TempProject.Dotnet();
        var settings = new LocalModelSettings(project.DataDir, new ForgetfulVault());

        var error = Assert.Throws<EngineException>(() => settings.Save("openai", Key));

        Assert.Equal("MODEL_KEY_STORE_FAILED", error.Code);
        Assert.False(File.Exists(LegacyFile(project)));
        Assert.False(settings.Status().Configured);
    }

    [Fact]
    public void Sem_cofre_a_chave_continua_no_arquivo_como_antes()
    {
        using var project = TempProject.Dotnet();
        var settings = new LocalModelSettings(project.DataDir, new MemoryVault { Available = false });

        settings.Save("openai", Key);

        Assert.True(File.Exists(LegacyFile(project)));
        Assert.Contains(Key, File.ReadAllText(LegacyFile(project)));
        Assert.Equal(new ModelStatus(true, "openai"), settings.Status());
        Assert.True(settings.TryGetKey(out var read));
        Assert.Equal(Key, read);
    }

    [Fact]
    public void Remover_apaga_a_chave_do_cofre_e_qualquer_arquivo()
    {
        using var project = TempProject.Dotnet();
        var vault = new MemoryVault();
        var settings = new LocalModelSettings(project.DataDir, vault);
        settings.Save("openai", Key);
        WriteLegacy(project, Other);

        settings.Clear();

        Assert.Empty(vault.Secrets);
        Assert.False(File.Exists(LegacyFile(project)));
        Assert.Equal(new ModelStatus(false, ""), settings.Status());
        Assert.False(settings.TryGetKey(out _));
    }

    [Theory]
    [InlineData("outro", Key, "MODEL_PROVIDER_DENIED")]
    [InlineData("openai", "curta", "MODEL_KEY_REJECTED")]
    [InlineData("openai", "tem espaço e símbolos !!! que o provedor não aceita", "MODEL_KEY_REJECTED")]
    public void Provedor_e_formato_de_chave_seguem_recusados_e_o_cofre_nao_e_tocado(string provider, string key, string code)
    {
        using var project = TempProject.Dotnet();
        var vault = new MemoryVault();
        var settings = new LocalModelSettings(project.DataDir, vault);

        var error = Assert.Throws<EngineException>(() => settings.Save(provider, key));

        Assert.Equal(code, error.Code);
        Assert.DoesNotContain(key, error.Message);
        Assert.Empty(vault.Secrets);
        Assert.False(File.Exists(LegacyFile(project)));
    }

    [WindowsFact]
    [SupportedOSPlatform("windows")]
    public void Na_DPAPI_real_a_chave_nao_aparece_em_texto_puro_em_nenhum_arquivo()
    {
        using var project = TempProject.Dotnet();
        var settings = new LocalModelSettings(project.DataDir, new DpapiSecretVault(project.DataDir));

        settings.Save("openai", Key);

        var files = Directory.GetFiles(project.DataDir, "*", SearchOption.AllDirectories);
        Assert.Contains(files, file => file.EndsWith(ReservedSecretIds.ModelKey + ".bin", StringComparison.Ordinal));
        Assert.DoesNotContain(files, file => file.EndsWith("model.json", StringComparison.Ordinal));
        Assert.All(files, file => Assert.DoesNotContain(Key, Encoding.UTF8.GetString(File.ReadAllBytes(file))));
        // Outra instância sobre o mesmo disco, com a DPAPI real, lê a chave de volta.
        var again = new LocalModelSettings(project.DataDir, new DpapiSecretVault(project.DataDir));
        Assert.True(again.TryGetKey(out var read));
        Assert.Equal(Key, read);
    }

    [WindowsFact]
    [SupportedOSPlatform("windows")]
    public void Na_DPAPI_real_o_arquivo_antigo_e_migrado_e_deixa_de_existir()
    {
        using var project = TempProject.Dotnet();
        WriteLegacy(project, Key);
        var settings = new LocalModelSettings(project.DataDir, new DpapiSecretVault(project.DataDir));

        Assert.True(settings.TryGetKey(out var read));

        Assert.Equal(Key, read);
        Assert.False(File.Exists(LegacyFile(project)));
        var files = Directory.GetFiles(project.DataDir, "*", SearchOption.AllDirectories);
        Assert.All(files, file => Assert.DoesNotContain(Key, Encoding.UTF8.GetString(File.ReadAllBytes(file))));
    }

    /// <summary>Um cofre que aceita guardar e não devolve nada: a promessa não cumprida que a migração precisa notar.</summary>
    private sealed class ForgetfulVault : ISecretVault
    {
        public bool Available => true;

        public void Store(string connectionId, string secret)
        {
        }

        public bool TryRead(string connectionId, out string secret)
        {
            secret = "";
            return false;
        }

        public void Remove(string connectionId)
        {
        }
    }

    /// <summary>Um cofre que falha ao escrever, como um disco cheio ou uma DPAPI que recusa.</summary>
    private sealed class ThrowingVault : ISecretVault
    {
        public bool Available => true;

        public void Store(string connectionId, string secret) => throw new IOException("sem espaço");

        public bool TryRead(string connectionId, out string secret)
        {
            secret = "";
            return false;
        }

        public void Remove(string connectionId)
        {
        }
    }
}
