using STAVCMS.LocalServer.Core;

namespace STAVCMS.LocalServer.Server;

public sealed class ServerManager : IAsyncDisposable
{
    private readonly PortablePaths _paths;
    private readonly ManagedProcess _apache = new();
    private readonly ManagedProcess _mariaDb = new();

    public ServerManager(PortablePaths paths) => _paths = paths;

    public bool ApacheRunning => _apache.IsRunning;
    public bool MariaDbRunning => _mariaDb.IsRunning;

    public void StartApache()
    {
        var exe = Path.Combine(_paths.Bin, "apache", "bin", "httpd.exe");
        _apache.Start(exe, "-f conf/httpd.conf", Path.GetDirectoryName(exe));
    }

    public void StartMariaDb()
    {
        var exe = Path.Combine(_paths.Bin, "mariadb", "bin", "mysqld.exe");
        var defaults = Path.Combine(_paths.Bin, "mariadb", "my.ini");
        _mariaDb.Start(exe, $"--defaults-file=\"{defaults}\"", Path.GetDirectoryName(exe));
    }

    public async Task StopAllAsync()
    {
        await _apache.StopAsync(TimeSpan.FromSeconds(5));
        await _mariaDb.StopAsync(TimeSpan.FromSeconds(8));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync();
        _apache.Dispose();
        _mariaDb.Dispose();
    }
}
