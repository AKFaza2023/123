using STAVCMS.LocalServer.Core;
using STAVCMS.LocalServer.Server;

var paths = new PortablePaths();
paths.EnsureDirectories();

var configPath = Path.Combine(paths.Config, "server.json");
var store = new ConfigStore();
var config = store.LoadServerConfig(configPath);
store.SaveServerConfig(configPath, config);

var ports = new PortManager();
Console.WriteLine("STAVCMS Local Server 1.0 - backend prototype");
Console.WriteLine($"Root: {paths.Root}");
Console.WriteLine($"HTTP {config.HttpPort}: {(ports.IsPortAvailable(config.HttpPort) ? "free" : "busy")}");
Console.WriteLine($"HTTPS {config.HttpsPort}: {(ports.IsPortAvailable(config.HttpsPort) ? "free" : "busy")}");
Console.WriteLine($"DB {config.DbPort}: {(ports.IsPortAvailable(config.DbPort) ? "free" : "busy")}");
Console.WriteLine("Commands: status, start, stop, exit");

await using var manager = new ServerManager(paths);
while (true)
{
    Console.Write("> ");
    var cmd = Console.ReadLine()?.Trim().ToLowerInvariant();
    try
    {
        switch (cmd)
        {
            case "status":
                Console.WriteLine($"Apache: {(manager.ApacheRunning ? "running" : "stopped")}");
                Console.WriteLine($"MariaDB: {(manager.MariaDbRunning ? "running" : "stopped")}");
                break;
            case "start":
                manager.StartMariaDb();
                manager.StartApache();
                Console.WriteLine("Start requested.");
                break;
            case "stop":
                await manager.StopAllAsync();
                Console.WriteLine("Stopped.");
                break;
            case "exit":
            case "quit":
                return;
            default:
                Console.WriteLine("Commands: status, start, stop, exit");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ERROR: {ex.Message}");
    }
}
