using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

/// <summary>
/// O cofre do macOS e o do Linux seguem as mesmas regras, então um contrato só roda contra os dois: o segredo nunca vai em
/// argumento, só vale o que volta igual, o identificador tem formato fixo e a falta de cofre tem motivo. Os comandos do sistema
/// são dublês fiéis (<see cref="FakeSecurity"/> e <see cref="FakeSecretTool"/>); os testes de verdade, com o cofre do sistema,
/// estão em <see cref="UnixVaultRealityTests"/> e só rodam quando a pessoa pede.
/// </summary>
public abstract class CommandVaultContract
{
    protected const string Id = "conn-0123456789ab";

    /// <summary>Um segredo que quebraria qualquer comando que o interpretasse: aspas, cifrão, crase, barra, quebra de linha, acento.</summary>
    protected const string Nasty = "ghp_a b\"c'd$e`f\\g\nlinha2 ção ✓ -w x";

    protected abstract FakeVaultCommand Command { get; }

    protected abstract ISecretVault Vault { get; }

    protected abstract bool ServiceDownBreaksProbe(bool broken);

    [Fact]
    public void Segredo_guardado_volta_igual_e_nunca_aparece_em_argumento_nem_em_texto_puro_na_entrada()
    {
        Vault.Store(Id, Nasty);

        Assert.True(Vault.TryRead(Id, out var secret));
        Assert.Equal(Nasty, secret);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Nasty));
        Assert.DoesNotContain(Nasty, Command.AllArguments());
        Assert.DoesNotContain(encoded, Command.AllArguments());
        Assert.DoesNotContain("ghp_", Command.AllArguments());
        // Na entrada vai o valor codificado, nunca o texto.
        Assert.Contains(encoded, Command.AllInput());
        Assert.DoesNotContain("ghp_", Command.AllInput());
        Assert.DoesNotContain("linha2", Command.AllInput());
    }

    [Fact]
    public void Guardar_de_novo_substitui_e_remover_apaga()
    {
        Vault.Store(Id, "primeiro-segredo-1234");
        Vault.Store(Id, "segundo-segredo-5678");

        Assert.True(Vault.TryRead(Id, out var current));
        Assert.Equal("segundo-segredo-5678", current);

        Vault.Remove(Id);
        Assert.False(Vault.TryRead(Id, out var gone));
        Assert.Equal("", gone);
        Vault.Remove(Id);
    }

    [Fact]
    public void Ler_o_que_nao_existe_diz_que_nao_ha()
    {
        Assert.False(Vault.TryRead(Id, out var secret));
        Assert.Equal("", secret);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../x")]
    [InlineData("conn-xyz")]
    [InlineData("conn-0123456789ab -w segredo")]
    [InlineData("conn-0123456789ab\n")]
    [InlineData("-a")]
    [InlineData("conn-0123456789AB")]
    public void Identificador_fora_do_formato_nunca_chega_ao_comando(string id)
    {
        Assert.Throws<ArgumentException>(() => Vault.Store(id, "segredo-qualquer-1234"));
        Assert.False(Vault.TryRead(id, out _));
        Vault.Remove(id);

        Assert.Empty(Command.Calls);
    }

    [Fact]
    public void O_identificador_reservado_da_chave_do_modelo_e_valido()
    {
        Vault.Store(ReservedSecretIds.ModelKey, "chave-do-modelo-12345678");

        Assert.True(Vault.TryRead(ReservedSecretIds.ModelKey, out var key));
        Assert.Equal("chave-do-modelo-12345678", key);
    }

    [Fact]
    public void Valor_ilegivel_no_cofre_vira_ausente_em_vez_de_derrubar()
    {
        Command.Items[Id] = "isto nao e base64 %%%";

        Assert.False(Vault.TryRead(Id, out var secret));
        Assert.Equal("", secret);
    }

    [Fact]
    public void Se_o_comando_diz_que_guardou_mas_nao_guardou_a_gravacao_falha()
    {
        Command.DropsWrites = true;

        var error = Assert.Throws<EngineException>(() => Vault.Store(Id, "segredo-qualquer-1234"));

        Assert.Equal("CREDENTIAL_VAULT_STORE_FAILED", error.Code);
        Assert.DoesNotContain("segredo-qualquer", error.Message);
    }

    [Fact]
    public void Se_o_comando_sai_com_erro_ao_guardar_a_gravacao_falha_sem_repetir_o_segredo()
    {
        Command.FailsWrites = true;

        var error = Assert.Throws<EngineException>(() => Vault.Store(Id, "segredo-qualquer-1234"));

        Assert.Equal("CREDENTIAL_VAULT_STORE_FAILED", error.Code);
        Assert.DoesNotContain("segredo-qualquer", error.Message);
    }

    [Fact]
    public void Cofre_que_responde_esta_disponivel_sem_dica_e_perguntar_nao_grava_nada()
    {
        Assert.True(Vault.Available);
        Assert.Null(Vault.UnavailableHint);
        Assert.NotEmpty(Vault.Name);
        Assert.Empty(Command.Items);
    }

    [Fact]
    public void Comando_que_nao_existe_deixa_o_cofre_indisponivel_com_o_que_instalar()
    {
        Command.NotInstalled = true;

        Assert.False(Vault.Available);
        Assert.False(string.IsNullOrWhiteSpace(Vault.UnavailableHint));
        Assert.DoesNotContain("\u2014", Vault.UnavailableHint);
    }

    [Fact]
    public void Comando_que_demora_alem_do_prazo_deixa_o_cofre_indisponivel_com_motivo()
    {
        Command.TimesOut = true;

        Assert.False(Vault.Available);
        Assert.Contains("prazo", Vault.UnavailableHint);
    }

    [Fact]
    public void Cofre_que_o_sistema_recusa_fica_indisponivel_com_motivo_e_nao_grava_em_texto_puro()
    {
        Assert.True(ServiceDownBreaksProbe(true));

        Assert.False(Vault.Available);
        Assert.False(string.IsNullOrWhiteSpace(Vault.UnavailableHint));
    }

    [Fact]
    public void A_disponibilidade_e_perguntada_uma_vez_so()
    {
        _ = Vault.Available;
        _ = Vault.Available;
        _ = Vault.UnavailableHint;

        Assert.Single(Command.Calls);
    }
}

public sealed class KeychainSecretVaultTests : CommandVaultContract
{
    private readonly FakeSecurity _security = new();
    private readonly KeychainSecretVault _vault;

    public KeychainSecretVaultTests() => _vault = new KeychainSecretVault(_security);

    protected override FakeVaultCommand Command => _security;

    protected override ISecretVault Vault => _vault;

    protected override bool ServiceDownBreaksProbe(bool broken)
    {
        _security.DeniedWith = broken ? 51 : null;
        return broken;
    }

    [Fact]
    public void Usa_o_comando_do_sistema_pelo_caminho_completo_e_grava_pelo_modo_interativo()
    {
        _vault.Store(Id, "segredo-qualquer-1234");

        Assert.All(_security.Calls, call => Assert.Equal("/usr/bin/security", call.Executable));
        var write = _security.Calls.Single(call => call.Arguments.SequenceEqual(["-i"]));
        Assert.Contains("add-generic-password -U -s \"Spock Studio\" -a \"conn-0123456789ab\" -w \"", write.StandardInput);
    }

    [Fact]
    public void Item_nao_encontrado_na_pergunta_e_cofre_disponivel_mas_acesso_negado_nao_e()
    {
        Assert.True(_vault.Available);

        var denied = new FakeSecurity { DeniedWith = 51 };
        var locked = new KeychainSecretVault(denied);
        Assert.False(locked.Available);
        Assert.Contains("51", locked.UnavailableHint);
        Assert.Contains("chaveiro", locked.UnavailableHint, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Chaveiro do macOS", locked.Name);
    }
}

public sealed class SecretServiceVaultTests : CommandVaultContract
{
    private readonly FakeSecretTool _tool = new();
    private readonly SecretServiceVault _vault;

    public SecretServiceVaultTests() => _vault = new SecretServiceVault(_tool);

    protected override FakeVaultCommand Command => _tool;

    protected override ISecretVault Vault => _vault;

    protected override bool ServiceDownBreaksProbe(bool broken)
    {
        _tool.ServiceDown = broken;
        return broken;
    }

    [Fact]
    public void O_segredo_vai_so_pela_entrada_e_os_atributos_identificam_o_servico_e_a_conta()
    {
        _vault.Store(Id, "segredo-qualquer-1234");

        var store = _tool.Calls.Single(call => call.Arguments[0] == "store");
        Assert.Equal(["store", "--label", "Spock Studio", "service", "spock-studio", "account", Id], store.Arguments);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("segredo-qualquer-1234")), store.StandardInput);
    }

    [Fact]
    public void Sem_servico_de_chaves_a_dica_diz_o_que_iniciar_e_sem_o_comando_diz_o_que_instalar()
    {
        var down = new SecretServiceVault(new FakeSecretTool { ServiceDown = true });
        var missing = new SecretServiceVault(new FakeSecretTool { NotInstalled = true });

        Assert.False(down.Available);
        Assert.Contains("GNOME Keyring", down.UnavailableHint);
        Assert.False(missing.Available);
        Assert.Contains("libsecret-tools", missing.UnavailableHint);
        Assert.Equal("Serviço de segredos do Linux (libsecret)", missing.Name);
    }

    [Fact]
    public void Item_que_nao_existe_na_pergunta_nao_derruba_a_disponibilidade()
    {
        // "lookup" sai com 1 e sem mensagem quando não há o item: o serviço respondeu.
        Assert.True(_vault.Available);
    }
}

public class SecretVaultSelectionTests
{
    [Fact]
    public void Cada_sistema_tem_o_seu_cofre_e_o_resto_fica_sem_cofre()
    {
        var runner = new FakeSecurity();

        Assert.IsType<KeychainSecretVault>(SecretVaults.For(VaultPlatform.MacOs, "dados", runner));
        Assert.IsType<SecretServiceVault>(SecretVaults.For(VaultPlatform.Linux, "dados", runner));
        Assert.IsType<UnavailableSecretVault>(SecretVaults.For(VaultPlatform.Other, "dados", runner));
        Assert.IsType(OperatingSystem.IsWindows() ? typeof(DpapiSecretVault) : typeof(UnavailableSecretVault), SecretVaults.For(VaultPlatform.Windows, "dados", runner));
    }

    [Fact]
    public void O_sistema_atual_e_reconhecido()
    {
        var expected = OperatingSystem.IsWindows() ? VaultPlatform.Windows : OperatingSystem.IsMacOS() ? VaultPlatform.MacOs : OperatingSystem.IsLinux() ? VaultPlatform.Linux : VaultPlatform.Other;

        Assert.Equal(expected, SecretVaults.CurrentPlatform());
    }

    [Fact]
    public void Sem_cofre_a_dica_diz_quais_sistemas_o_motor_atende_e_guardar_e_recusado()
    {
        var vault = new UnavailableSecretVault();

        Assert.False(vault.Available);
        Assert.Contains("Windows", vault.UnavailableHint);
        Assert.Throws<EngineException>(() => vault.Store("conn-0123456789ab", "segredo-qualquer-1234"));
    }
}

/// <summary>A central e a chave do modelo com um cofre de comando: o cofre muda de sistema, o resto não.</summary>
public class CommandVaultIntegrationTests
{
    private const string Key = "sk-test-chave-do-cofre-unix-1234";

    [Fact]
    public void A_chave_do_modelo_vai_para_o_cofre_do_linux_e_o_arquivo_antigo_some()
    {
        using var project = TempProject.Dotnet();
        var tool = new FakeSecretTool();
        new FileModelSettings(project.DataDir).Save("openai", Key);

        var settings = new LocalModelSettings(project.DataDir, new SecretServiceVault(tool));

        Assert.True(settings.TryGetKey(out var key));
        Assert.Equal(Key, key);
        Assert.Equal(ModelKeyProtection.Vault, settings.Protection());
        Assert.False(File.Exists(Path.Combine(project.DataDir, "model.json")));
        Assert.DoesNotContain(Key, tool.AllArguments());
    }

    [Fact]
    public void Sem_o_servico_de_chaves_a_chave_do_modelo_continua_em_arquivo_e_a_tela_pode_dizer_isso()
    {
        using var project = TempProject.Dotnet();
        var settings = new LocalModelSettings(project.DataDir, new SecretServiceVault(new FakeSecretTool { ServiceDown = true }));

        settings.Save("openai", Key);

        Assert.Equal(ModelKeyProtection.File, settings.Protection());
        Assert.True(File.Exists(Path.Combine(project.DataDir, "model.json")));
    }

    [Fact]
    public void O_catalogo_traz_o_nome_do_cofre_e_o_que_falta_quando_ele_nao_esta_disponivel()
    {
        using var project = TempProject.Dotnet();
        var workspace = new Mariliz.Application.Workspace.WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        IntegrationHub Hub(ISecretVault vault) => new(
            new FileIntegrationStore(project.DataDir), new FileSyncedDataStore(project.DataDir), vault,
            new ConnectorRegistry([]), workspace, new FakeClock(), new MemoryAudit());

        var down = Hub(new SecretServiceVault(new FakeSecretTool { NotInstalled = true }));
        var up = Hub(new KeychainSecretVault(new FakeSecurity()));

        Assert.Equal((false, "Serviço de segredos do Linux (libsecret)"), (down.VaultAvailable, down.VaultName));
        Assert.Contains("libsecret-tools", down.VaultHint);
        Assert.Equal((true, "Chaveiro do macOS", (string?)null), (up.VaultAvailable, up.VaultName, up.VaultHint));

        var session = new EngineSession("test", integrations: down);
        var response = session.Handle(new JsonObject
            { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = "integration.catalog", ["params"] = new JsonObject() }.ToJsonString()).Response;
        Assert.Equal("libsecret-tools", System.Text.RegularExpressions.Regex.Match(response["result"]!["vaultHint"]!.GetValue<string>(), "libsecret-tools").Value);
    }

    [Fact]
    public async Task Criar_conexao_sem_cofre_e_recusado_com_o_motivo_do_sistema_e_nada_vai_para_o_disco()
    {
        using var h = new IntegrationHarness();
        var down = new SecretServiceVault(new FakeSecretTool { ServiceDown = true });
        var connections = new ConnectionService(new FileIntegrationStore(h.DataDir), down, h.Hub.Connectors, h.Clock, h.Audit);

        var error = await Assert.ThrowsAsync<EngineException>(() => connections.CreateAsync(
            IntegrationProviders.GitHub, "GitHub", new Dictionary<string, string>(), FakeGitHub.Token, CancellationToken.None));

        Assert.Equal("CREDENTIAL_VAULT_UNAVAILABLE", error.Code);
        Assert.DoesNotContain(FakeGitHub.Token, h.EverythingOnDisk());
    }
}
