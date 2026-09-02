using System.Diagnostics;
using STAVCMS.LocalServer.Core;

namespace STAVCMS.LocalServer.Windows;

public sealed class ElevatedHelperClient
{
    private readonly PortablePaths _paths;

    public ElevatedHelperClient(PortablePaths paths) => _paths = paths;

    public async Task RegisterProjectAsync(string domain, bool https, CancellationToken cancellationToken = default)
    {
        var helper = Path.Combine(_paths.Root, "STAVCMS.ElevatedHelper.exe");
        if (!File.Exists(helper)) throw new FileNotFoundException("Не найден STAVCMS.ElevatedHelper.exe", helper);

        var args = $"register-project --domain \"{domain}\" --ssl-dir \"{_paths.Ssl}\" --https {(https ? "true" : "false")}";
        var psi = new ProcessStartInfo
        {
            FileName = helper,
            Arguments = args,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = _paths.Root
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Не удалось запустить Windows Integration helper.");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException($"Windows Integration завершилась с кодом {process.ExitCode}.");
    }
}
