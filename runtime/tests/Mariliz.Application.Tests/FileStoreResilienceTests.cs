using System.Text.Json;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

/// <summary>
/// Os arquivos do motor falham de dois jeitos: o conteúdo está corrompido, ou o arquivo está travado por outro processo (antivírus,
/// indexador). O segundo é passageiro e <b>não pode</b> virar "vazio": a próxima gravação reescreveria o arquivo inteiro e apagaria
/// o que estava lá. O primeiro vai para <c>.corrupt-*</c>, sem perder o conteúdo.
/// </summary>
public class FileStoreResilienceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("mariliz-store-").FullName;
    private static readonly JsonSerializerOptions Json = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static FileStream Lock(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.None);

    private string[] Quarantined(string name, string? root = null) => Directory.GetFiles(root ?? _dir, name + ".corrupt-*", SearchOption.AllDirectories);

    // ------------------------------------------------------------------ AtomicFile.ReadJson

    [Fact]
    public void Sem_arquivo_devolve_nulo_e_com_conteudo_valido_devolve_o_documento()
    {
        var path = Path.Combine(_dir, "doc.json");

        Assert.Null(AtomicFile.ReadJson<List<int>>(path, Json));

        AtomicFile.Write(path, "[1,2,3]");
        Assert.Equal([1, 2, 3], AtomicFile.ReadJson<List<int>>(path, Json));
    }

    [Fact]
    public void Conteudo_corrompido_ou_fora_da_forma_vai_para_quarentena_sem_perder_o_texto()
    {
        var broken = Path.Combine(_dir, "broken.json");
        File.WriteAllText(broken, "{isto não é json");
        var wrongShape = Path.Combine(_dir, "shape.json");
        File.WriteAllText(wrongShape, "[1]");

        Assert.Null(AtomicFile.ReadJson<List<int>>(broken, Json));
        Assert.Null(AtomicFile.ReadJson<List<int>>(wrongShape, Json, list => list.Count > 1));

        Assert.False(File.Exists(broken));
        Assert.Equal("{isto não é json", File.ReadAllText(Assert.Single(Quarantined("broken.json"))));
        Assert.Equal("[1]", File.ReadAllText(Assert.Single(Quarantined("shape.json"))));
    }

    [Fact]
    public void Arquivo_travado_falha_e_nao_vira_vazio_nem_vai_para_quarentena()
    {
        var path = Path.Combine(_dir, "locked.json");
        AtomicFile.Write(path, "[1]");

        using (Lock(path))
            Assert.ThrowsAny<IOException>(() => AtomicFile.ReadJson<List<int>>(path, Json));

        Assert.Empty(Quarantined("locked.json"));
        Assert.Equal([1], AtomicFile.ReadJson<List<int>>(path, Json));
    }

    // ------------------------------------------------------------------ Os stores

    [Fact]
    public async Task Conexoes_com_o_arquivo_travado_falham_em_vez_de_aparecer_vazias_e_nada_se_perde()
    {
        using var h = new IntegrationHarness();
        await h.ConnectGitHubAsync();
        var store = new FileIntegrationStore(h.DataDir);
        var path = Path.Combine(h.DataDir, "integrations", "connections.json");

        using (Lock(path))
        {
            Assert.ThrowsAny<IOException>(() => store.Connections());
            Assert.ThrowsAny<IOException>(() => store.RemoveConnection("qualquer"));
        }

        Assert.Single(store.Connections());
    }

    [Fact]
    public async Task Arquivo_de_conexoes_corrompido_e_guardado_ao_lado_e_a_gravacao_seguinte_recomeca()
    {
        using var h = new IntegrationHarness();
        var link = await h.LinkGitHubAsync();
        var path = Path.Combine(h.DataDir, "integrations", "connections.json");
        File.WriteAllText(path, "{corrompido");

        var store = new FileIntegrationStore(h.DataDir);
        Assert.Empty(store.Connections());
        store.SaveIntegration(link);

        Assert.Single(store.Integrations());
        Assert.Equal("{corrompido", File.ReadAllText(Assert.Single(Quarantined("connections.json", h.DataDir))));
    }

    [Fact]
    public void Historico_de_risco_travado_falha_e_corrompido_e_guardado_ao_lado()
    {
        var store = new FileRiskHistoryStore(_dir);
        const string project = "p-000000000001";
        var snapshot = new RiskSnapshot(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, [new RiskSnapshotEntry("risk.blocked", "Bloqueados", "high", ["1"])]);
        store.Save(project, [snapshot]);
        var path = Path.Combine(_dir, "projects", project, "risk-history.json");

        using (Lock(path))
            Assert.ThrowsAny<IOException>(() => store.Load(project));
        Assert.Single(store.Load(project));

        File.WriteAllText(path, "não é json");
        Assert.Empty(store.Load(project));
        Assert.Equal("não é json", File.ReadAllText(Assert.Single(Quarantined("risk-history.json"))));
    }

    [Fact]
    public void Dados_sincronizados_corrompidos_viram_vazios_com_o_original_guardado()
    {
        var store = new FileSyncedDataStore(_dir);
        var path = IntegrationPaths.SyncedData(_dir, "p-000000000001", "pint-000000000001");
        AtomicFile.Write(path, "{");

        Assert.Empty(store.Read("p-000000000001", "pint-000000000001").Items);

        Assert.Equal("{", File.ReadAllText(Assert.Single(Quarantined("pint-000000000001.json"))));
    }

    [Fact]
    public void Lista_de_pastas_autorizadas_travada_falha_e_corrompida_e_guardada_ao_lado()
    {
        var registry = new FileWorkspaceRegistry(_dir);
        registry.Save("p-000000000001", "C:/um");
        var path = Path.Combine(_dir, "workspaces.json");

        using (Lock(path))
        {
            Assert.ThrowsAny<IOException>(() => registry.Load());
            Assert.ThrowsAny<IOException>(() => registry.Save("p-000000000002", "C:/dois"));
        }
        Assert.Equal("C:/um", registry.Load()["p-000000000001"]);

        File.WriteAllText(path, "[]");
        Assert.Empty(registry.Load());
        Assert.Equal("[]", File.ReadAllText(Assert.Single(Quarantined("workspaces.json"))));
        registry.Save("p-000000000002", "C:/dois");
        Assert.Equal(["p-000000000002"], registry.Load().Keys);
    }

    // ------------------------------------------------------------------ Diário de execução

    [Fact]
    public async Task Gravar_e_ler_o_diario_ao_mesmo_tempo_nunca_falha_nem_devolve_pela_metade()
    {
        var store = new FileRunStore(_dir);
        var journal = new RunJournal { RunId = "run-000000000001", ProjectId = "p-000000000001", Kind = "apply", PlanId = "plan-1" };
        store.SaveJournal(journal);
        var stop = false;

        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 400; i++)
            {
                journal.PlanId = "plan-" + i;
                store.SaveJournal(journal);
                store.SaveLinks("p-000000000001", "run-000000000001", [new RunLink("int-1", "issues", i.ToString(), DateTimeOffset.UnixEpoch)]);
            }
            stop = true;
        });
        var reader = Task.Run(() =>
        {
            while (!stop)
            {
                Assert.NotNull(store.LoadJournal("p-000000000001", "run-000000000001"));
                store.LoadLinks("p-000000000001", "run-000000000001");
            }
        });

        await Task.WhenAll(writer, reader);
    }

    // ------------------------------------------------------------------ Chave do modelo

    [UnixFact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void A_chave_do_modelo_em_arquivo_so_e_legivel_pelo_dono()
    {
        new FileModelSettings(_dir).Save("openai", "sk-chave-de-teste-permissao-0001");

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(_dir, "model.json")));
    }
}
