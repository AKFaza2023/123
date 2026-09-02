using System.Net.NetworkInformation;
using System.Windows;
using STAVCMS.LocalServer.Core;
using STAVCMS.LocalServer.Server;

namespace STAVCMS.LocalServer.App;

public partial class MainWindow : Window
{
    private readonly PortablePaths _paths;
    private readonly ServerManager _serverManager;

    public MainWindow()
    {
        InitializeComponent();
        _paths = new PortablePaths(AppContext.BaseDirectory);
        _paths.EnsureDirectories();
        _serverManager = new ServerManager(_paths);
        RootPathText.Text = _paths.Root;
        RefreshStatus();
        AppendLog("STAVCMS Local Server 0.2 запущен.");
    }

    private void RefreshStatus()
    {
        ApacheStatusText.Text = _serverManager.ApacheRunning ? "Работает" : "Остановлен";
        MariaDbStatusText.Text = _serverManager.MariaDbRunning ? "Работает" : "Остановлена";

        var apacheExe = System.IO.Path.Combine(_paths.Bin, "apache", "bin", "httpd.exe");
        var mariaExe = System.IO.Path.Combine(_paths.Bin, "mariadb", "bin", "mysqld.exe");

        ApachePathText.Text = System.IO.File.Exists(apacheExe)
            ? "Apache: найден"
            : "Apache: не установлен в bin\\apache";

        MariaDbPathText.Text = System.IO.File.Exists(mariaExe)
            ? "MariaDB: найдена"
            : "MariaDB: не установлена в bin\\mariadb";

        var busy = IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Select(x => x.Port)
            .Where(p => p is 80 or 443 or 3306)
            .Distinct()
            .OrderBy(p => p)
            .ToArray();

        PortStatusText.Text = busy.Length == 0
            ? "Порты 80 / 443 / 3306 свободны"
            : $"Заняты порты: {string.Join(", ", busy)}";
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        TryRun(() =>
        {
            if (!_serverManager.ApacheRunning)
                _serverManager.StartApache();
            if (!_serverManager.MariaDbRunning)
                _serverManager.StartMariaDb();
            AppendLog("Команда запуска сервера выполнена.");
        });
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _serverManager.StopAllAsync();
            AppendLog("Apache и MariaDB остановлены.");
        }
        catch (Exception ex)
        {
            AppendLog($"Ошибка остановки: {ex.Message}");
        }
        finally
        {
            RefreshStatus();
        }
    }

    private async void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _serverManager.StopAllAsync();
            _serverManager.StartApache();
            _serverManager.StartMariaDb();
            AppendLog("Сервер перезапущен.");
        }
        catch (Exception ex)
        {
            AppendLog($"Ошибка перезапуска: {ex.Message}");
        }
        finally
        {
            RefreshStatus();
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
        AppendLog("Состояние среды обновлено.");
    }

    private void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            AppendLog($"Ошибка запуска: {ex.Message}");
        }
        finally
        {
            RefreshStatus();
        }
    }

    private void AppendLog(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    protected override async void OnClosed(EventArgs e)
    {
        await _serverManager.DisposeAsync();
        base.OnClosed(e);
    }
}
