using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Iac;
using Mariliz.Application.Management.Investigation;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Tests;

// RS-10: o host não guarda mais conhecimento do motor. Os prazos chegam no engine.hello, e a lista de métodos que a
// interface pode chamar (protocol.rs) é conferida aqui contra ProtocolMethods.
public sealed partial class HostProtocolTests
{
    // Do ciclo de vida do host, do status que ele compõe (runtime.health) ou só pelo diálogo nativo (workspace.authorize
    // recebe um caminho): nunca vêm da interface.
    private static readonly string[] HostOnly =
        [ProtocolMethods.Hello, ProtocolMethods.Shutdown, ProtocolMethods.AuthorizeWorkspace, ProtocolMethods.RuntimeHealth];

    [Fact]
    public void O_hello_anuncia_em_segundos_a_espera_de_cada_metodo_longo()
    {
        var (response, _) = new EngineSession("0.1.0").Handle(
            """{"v":1,"kind":"request","id":"h","method":"engine.hello","params":{"protocol":[1],"hostVersion":"teste"}}""");

        var deadlines = response["result"]!["deadlines"]!.AsObject();

        Assert.Equal(ProtocolDeadlines.ByMethod.Keys.Order(StringComparer.Ordinal), deadlines.Select(entry => entry.Key));
        Assert.Equal(5400, deadlines[ProtocolMethods.IacApply]!.GetValue<int>());
        Assert.Equal(2700, deadlines[ProtocolMethods.IacPlan]!.GetValue<int>());
        Assert.Equal(180, deadlines[ProtocolMethods.ManagementInvestigate]!.GetValue<int>());
        Assert.Equal(660, deadlines[ProtocolMethods.IacRuntimeInstall]!.GetValue<int>());
    }

    [Fact]
    public void Cada_espera_anunciada_passa_do_prazo_do_proprio_motor()
    {
        Assert.True(ProtocolDeadlines.ByMethod[ProtocolMethods.ManagementInvestigate] > InvestigationService.StandardDeadline);
        Assert.True(ProtocolDeadlines.ByMethod[ProtocolMethods.IacRuntimeInstall] > IacTimeouts.Download);
        foreach (var (method, total) in IacTimeouts.ByMethod)
            Assert.True(ProtocolDeadlines.ByMethod[method] >= total + IacTimeouts.HostMargin, method);
    }

    [Fact]
    public void A_lista_de_metodos_da_interface_no_host_bate_com_os_metodos_do_motor()
    {
        var rust = File.ReadAllText(Path.Combine(ContractFiles.Root, "desktop", "host", "src-tauri", "src", "protocol.rs"));
        var block = FrontendBlock().Match(rust);
        Assert.True(block.Success, "protocol.rs não tem mais FRONTEND_METHODS no formato esperado.");
        var host = Quoted().Matches(block.Groups[1].Value).Select(match => match.Groups[1].Value).ToList();

        var engine = typeof(ProtocolMethods).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(host.Count, host.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(host.Where(method => !engine.Contains(method)));
        Assert.Empty(host.Intersect(HostOnly, StringComparer.Ordinal));
        // Um método novo no motor precisa de uma decisão: entra na lista da interface ou fica só com o host.
        Assert.Empty(engine.Except(host, StringComparer.Ordinal).Except(HostOnly, StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }

    [GeneratedRegex(@"pub const FRONTEND_METHODS: &\[&str\] = &\[(.*?)\];", RegexOptions.Singleline)]
    private static partial Regex FrontendBlock();

    [GeneratedRegex("\"([a-z][a-z0-9._]*)\"")]
    private static partial Regex Quoted();
}
