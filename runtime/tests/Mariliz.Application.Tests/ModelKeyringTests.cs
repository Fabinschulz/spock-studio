using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Models;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class ModelKeyringTests
{
    private const string OpenAiKey = "sk-test-openai-fase-i1-7c2d41";
    private const string AnthropicKey = "sk-ant-test-anthropic-fase-i1-91ab_x";
    private const string GoogleKey = "AIzaSyTestGoogleFaseI1-3f9c_x2ab7d41";

    private static ModelKeyring Keyring(TempProject project, MemoryVault? vault = null) => new(project.DataDir, vault ?? new MemoryVault());

    [Fact]
    public void Cada_provedor_guarda_a_sua_chave_sem_mexer_nas_dos_outros()
    {
        using var project = TempProject.Dotnet();
        var keyring = Keyring(project);

        keyring.Save("openai", OpenAiKey);
        keyring.Save("anthropic", AnthropicKey);

        Assert.Equal(["openai", "anthropic"], keyring.Providers());
        Assert.True(keyring.TryGetKey("openai", out var openai));
        Assert.True(keyring.TryGetKey("anthropic", out var anthropic));
        Assert.Equal((OpenAiKey, AnthropicKey), (openai, anthropic));
        Assert.False(keyring.TryGetKey("google", out _));
    }

    [Fact]
    public void A_primeira_chave_vira_o_padrao_e_guardar_outra_nao_o_tira_de_quem_ja_era()
    {
        using var project = TempProject.Dotnet();
        var keyring = Keyring(project);

        keyring.Save("anthropic", AnthropicKey);
        keyring.Save("openai", OpenAiKey);

        Assert.Equal(new ModelStatus(true, "anthropic"), keyring.Status());
        Assert.True(keyring.TryGetKey(out var key));
        Assert.Equal(AnthropicKey, key);
    }

    [Fact]
    public void Mudar_o_padrao_vale_e_sobrevive_a_reiniciar()
    {
        using var project = TempProject.Dotnet();
        var vault = new MemoryVault();
        var keyring = Keyring(project, vault);
        keyring.Save("openai", OpenAiKey);
        keyring.Save("anthropic", AnthropicKey);

        keyring.SetDefault("anthropic");

        Assert.Equal("anthropic", Keyring(project, vault).Status().Provider);
    }

    [Fact]
    public void Remover_o_padrao_passa_o_padrao_para_quem_ainda_tem_chave()
    {
        using var project = TempProject.Dotnet();
        var keyring = Keyring(project);
        keyring.Save("openai", OpenAiKey);
        keyring.Save("anthropic", AnthropicKey);

        keyring.Remove("openai");

        Assert.Equal(["anthropic"], keyring.Providers());
        Assert.Equal(new ModelStatus(true, "anthropic"), keyring.Status());
        Assert.False(keyring.TryGetKey("openai", out _));
    }

    [Fact]
    public void Remover_a_ultima_deixa_tudo_sem_chave_e_Clear_apaga_todas()
    {
        using var project = TempProject.Dotnet();
        var vault = new MemoryVault();
        var keyring = Keyring(project, vault);
        keyring.Save("openai", OpenAiKey);
        keyring.Remove("openai");
        Assert.Equal(new ModelStatus(false, ""), keyring.Status());

        keyring.Save("openai", OpenAiKey);
        keyring.Save("google", GoogleKey);
        keyring.Clear();

        Assert.Empty(keyring.Providers());
        Assert.Empty(vault.Secrets);
    }

    [Fact]
    public void Provedor_desconhecido_nao_tem_lugar_e_a_chave_invalida_e_recusada()
    {
        using var project = TempProject.Dotnet();
        var keyring = Keyring(project);

        var unknown = Assert.Throws<EngineException>(() => keyring.Save("outro", OpenAiKey));
        var rejected = Assert.Throws<EngineException>(() => keyring.Save("openai", "curta"));

        Assert.Equal(("MODEL_PROVIDER_DENIED", "MODEL_KEY_REJECTED"), (unknown.Code, rejected.Code));
        Assert.Empty(keyring.Providers());
    }

    [Fact]
    public void Com_cofre_cada_chave_fica_em_seu_lugar_dele_e_sem_cofre_cada_uma_no_seu_arquivo()
    {
        using var project = TempProject.Dotnet();
        var vault = new MemoryVault();
        var keyring = Keyring(project, vault);

        keyring.Save("openai", OpenAiKey);
        keyring.Save("google", GoogleKey);

        Assert.Equal(ModelKeyProtection.Vault, keyring.Protection("openai"));
        Assert.Equal(ModelKeyProtection.Vault, keyring.Protection("google"));
        Assert.Null(keyring.Protection("anthropic"));
        Assert.Equal(OpenAiKey, vault.Secrets[ReservedSecretIds.ModelKeyOf("openai")]);
        Assert.Equal(GoogleKey, vault.Secrets[ReservedSecretIds.ModelKeyOf("google")]);

        using var other = TempProject.Dotnet();
        var noVault = Keyring(other, new MemoryVault { Available = false });
        noVault.Save("openai", OpenAiKey);
        noVault.Save("anthropic", AnthropicKey);
        Assert.Equal(ModelKeyProtection.File, noVault.Protection("openai"));
        Assert.Equal(ModelKeyProtection.File, noVault.Protection("anthropic"));
        Assert.Equal(["openai", "anthropic"], noVault.Providers());
    }

    [Fact]
    public void A_chave_de_antes_migra_para_o_lugar_do_provedor_dela_e_so_sai_do_lugar_antigo_depois()
    {
        using var project = TempProject.Dotnet();
        var vault = new MemoryVault();
        new LocalModelSettings(project.DataDir, vault).Save("anthropic", AnthropicKey);
        Assert.True(vault.Secrets.ContainsKey(ReservedSecretIds.ModelKey));

        var keyring = Keyring(project, vault);

        Assert.Equal(["anthropic"], keyring.Providers());
        Assert.True(keyring.TryGetKey("anthropic", out var key));
        Assert.Equal(AnthropicKey, key);
        Assert.Equal(AnthropicKey, vault.Secrets[ReservedSecretIds.ModelKeyOf("anthropic")]);
        Assert.False(vault.Secrets.ContainsKey(ReservedSecretIds.ModelKey));
        Assert.Equal("anthropic", keyring.Status().Provider);
    }

    [Fact]
    public void A_chave_de_antes_em_arquivo_tambem_migra_para_o_lugar_do_provedor_dela()
    {
        using var project = TempProject.Dotnet();
        new FileModelSettings(project.DataDir).Save("openai", OpenAiKey);

        var keyring = Keyring(project);

        Assert.True(keyring.TryGetKey("openai", out var key));
        Assert.Equal(OpenAiKey, key);
        Assert.False(new FileModelSettings(project.DataDir).TryGetKey(out _));
    }

    private sealed class RefusesNewSlots : ISecretVault
    {
        public Dictionary<string, string> Secrets { get; } = [];
        public bool Available => true;
        public void Store(string connectionId, string secret)
        {
            if (connectionId != ReservedSecretIds.ModelKey) throw new IOException("cofre indisponível");
            Secrets[connectionId] = secret;
        }

        public bool TryRead(string connectionId, out string secret) => Secrets.TryGetValue(connectionId, out secret!);
        public void Remove(string connectionId) => Secrets.Remove(connectionId);
    }

    [Fact]
    public void Se_a_migracao_falha_a_chave_antiga_continua_valendo_no_lugar_antigo()
    {
        using var project = TempProject.Dotnet();
        var vault = new RefusesNewSlots();
        new LocalModelSettings(project.DataDir, vault).Save("anthropic", AnthropicKey);

        var keyring = new ModelKeyring(project.DataDir, vault);

        Assert.Equal(["anthropic"], keyring.Providers());
        Assert.True(keyring.TryGetKey("anthropic", out var key));
        Assert.Equal(AnthropicKey, key);
        Assert.True(vault.Secrets.ContainsKey(ReservedSecretIds.ModelKey));
    }

    [Fact]
    public void Um_arquivo_de_padrao_ilegivel_vale_como_o_primeiro_provedor_com_chave()
    {
        using var project = TempProject.Dotnet();
        var keyring = Keyring(project);
        keyring.Save("anthropic", AnthropicKey);
        keyring.Save("openai", OpenAiKey);
        File.WriteAllText(Path.Combine(project.DataDir, "model-default.json"), "{ nao e json");

        Assert.Equal("openai", keyring.Status().Provider);
    }
}
