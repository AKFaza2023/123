using System.Diagnostics;

namespace STAVCMS.LocalServer.Server;

public sealed class ManagedProcess : IDisposable
{
    private Process? _process;
    public int? ProcessId => _process is { HasExited: false } ? _process.Id : null;
    public bool IsRunning => _process is { HasExited: false };

    public void Start(string executable, string arguments = "", string? workingDirectory = null)
    {
        if (IsRunning) return;
        if (!File.Exists(executable))
            throw new FileNotFoundException("Executable not found.", executable);

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.Start();
    }

    public async Task StopAsync(TimeSpan timeout)
    {
        if (!IsRunning || _process is null) return;

        try
        {
            _process.CloseMainWindow();
            using var cts = new CancellationTokenSource(timeout);
            await _process.WaitForExitAsync(cts.Token);
        }
        catch
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
    }

    public void Dispose() => _process?.Dispose();
}
