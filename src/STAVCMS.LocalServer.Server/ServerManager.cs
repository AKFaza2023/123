using System.Diagnostics;
using System.Net.Http;
using STAVCMS.LocalServer.Core;

namespace STAVCMS.LocalServer.Server;

public sealed class ServerManager : IAsyncDisposable
{
    private readonly PortablePaths _paths;
    private readonly ManagedProcess _apache = new();
    private readonly ManagedProcess _mariaDb = new();
    private readonly EnvironmentBootstrapper _bootstrapper;

    public ServerManager(PortablePaths paths)
    {
        _paths = paths;
        _bootstrapper = new EnvironmentBootstrapper(paths);
    }

    public bool ApacheRunning => _apache.IsRunning;
    public bool MariaDbRunning => _mariaDb.IsRunning;
    public int HttpPort { get; private set; } = 80;
    public int HttpsPort { get; private set; } = 443;
    public int DbPort { get; private set; } = 3306;

    public async Task<BootstrapResult> PrepareAsync(CancellationToken cancellationToken = default)
    {
        var result = await _bootstrapper.PrepareAsync(cancellationToken);
        HttpPort = result.HttpPort;
        HttpsPort = result.HttpsPort;
        DbPort = result.DbPort;
        return result;
    }

    public void StartApache()
    {
        var exe = Path.Combine(_paths.Bin, "apache", "bin", "httpd.exe");
        var conf = Path.Combine(_paths.Bin, "apache", "conf", "httpd-stavcms.conf");
        _apache.Start(exe, $"-f \"{conf}\"", Path.GetDirectoryName(exe));
    }

    public void StartMariaDb()
    {
        var exe = Path.Combine(_paths.Bin, "mariadb", "bin", "mysqld.exe");
        var defaults = Path.Combine(_paths.Bin, "mariadb", "my.ini");
        _mariaDb.Start(exe, $"--defaults-file=\"{defaults}\" --console", Path.GetDirectoryName(exe));
    }

    public async Task CreateDatabaseAsync(string database, CancellationToken cancellationToken = default)
    {
        if (database.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
            throw new InvalidOperationException("Некорректное имя базы данных.");
        if (!MariaDbRunning) throw new InvalidOperationException("MariaDB должна быть запущена для создания базы данных.");

        var client = new[] { "mariadb.exe", "mysql.exe" }
            .Select(name => Path.Combine(_paths.Bin, "mariadb", "bin", name))
            .FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("Не найден клиент MariaDB.");
        var sql = $"CREATE DATABASE IF NOT EXISTS `{database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
        var psi = new ProcessStartInfo
        {
            FileName = client,
            Arguments = $"-h 127.0.0.1 -P {DbPort} -u root --protocol=tcp -e \"{sql}\"",
            WorkingDirectory = Path.GetDirectoryName(client)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Не удалось запустить клиент MariaDB.");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Не удалось создать базу данных: {error}".Trim());
        }
    }

    public async Task<bool> CheckHttpAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        try
        {
            using var response = await client.GetAsync($"http://127.0.0.1:{HttpPort}/", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
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
