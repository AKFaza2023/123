using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace STAVCMS.LocalServer.Server;

public sealed class PortManager
{
    public bool IsPortAvailable(int port)
    {
        var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        if (listeners.Any(x => x.Port == port)) return false;

        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public int FindAvailablePort(params int[] preferred)
    {
        foreach (var port in preferred)
            if (IsPortAvailable(port)) return port;
        throw new InvalidOperationException("No preferred TCP ports are available.");
    }
}
