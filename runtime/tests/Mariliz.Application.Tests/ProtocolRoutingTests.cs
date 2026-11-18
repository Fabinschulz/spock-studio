using System.Reflection;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Tests;

public sealed class ProtocolRoutingTests
{
    private static IEnumerable<(string Group, IReadOnlyList<string> Methods)> Groups() =>
        typeof(ProtocolMethods).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(IReadOnlyList<string>))
            .Select(field => (field.Name, (IReadOnlyList<string>)field.GetValue(null)!));

    // A sessão roteia pelo nome do método: um método em dois grupos faria a sessão recusar a montagem.
    [Fact]
    public void Cada_metodo_pertence_a_um_grupo_so()
    {
        var repeated = Groups()
            .SelectMany(group => group.Methods.Select(method => (method, group.Group)))
            .GroupBy(entry => entry.method)
            .Where(entries => entries.Count() > 1)
            .Select(entries => $"{entries.Key}: {string.Join(", ", entries.Select(entry => entry.Group))}")
            .ToList();

        Assert.Empty(repeated);
    }

    [Fact]
    public void Sessao_anuncia_os_grupos_presentes_e_recusa_os_ausentes()
    {
        var session = new EngineSession("test", ide: new Mariliz.Application.Mcp.IdeConnection(null));

        var hello = session.Handle("""{"v":1,"kind":"request","id":"1","method":"engine.hello","params":{"protocol":[1],"hostVersion":"t"}}""").Response;
        var ide = session.Handle("""{"v":1,"kind":"request","id":"2","method":"ide.connection"}""").Response;
        var missing = session.Handle("""{"v":1,"kind":"request","id":"3","method":"run.list","params":{}}""").Response;

        var capabilities = hello["result"]!["capabilities"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();
        Assert.Equal(ProtocolMethods.Ide, capabilities);
        Assert.NotNull(ide["result"]);
        Assert.Equal("METHOD_NOT_FOUND", missing["error"]!["code"]!.GetValue<string>());
    }
}
