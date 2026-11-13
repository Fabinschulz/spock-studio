using System.Net;
using System.Net.Sockets;

namespace Mariliz.Infrastructure;

internal static class LoopbackPort
{
    // Há uma janela entre soltar a porta e usá-la; quem a usa tenta de novo se falhar.
    public static int Free()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try { return ((IPEndPoint)probe.LocalEndpoint).Port; }
        finally { probe.Stop(); }
    }
}
