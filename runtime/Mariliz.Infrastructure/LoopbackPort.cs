using System.Net;
using System.Net.Sockets;

namespace Mariliz.Infrastructure;

internal static class LoopbackPort
{
    /// <summary>Uma porta livre em 127.0.0.1. Há uma janela entre soltá-la e usá-la; quem a usa tenta de novo se falhar.</summary>
    public static int Free()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try { return ((IPEndPoint)probe.LocalEndpoint).Port; }
        finally { probe.Stop(); }
    }
}
