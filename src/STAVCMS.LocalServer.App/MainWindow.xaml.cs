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
        AppendLog("STAVCMS Local Server 0.4 запущен.");
        AppendLog("Среда будет автоматически подготовлена при первом запуске сервера.");
    }

    private void RefreshStatus()
    {
        ApacheStatusText.Text = _serverManager.ApacheRunning ? "Работает" : "Остановлен";
        MariaDbStatusText.Text = _serverManager.MariaDbRunning ? "Работает" : "Остановлена";

        var apacheExe = System.IO.Path.Combine(_paths.Bin, "apache", "bin", "httpd.exe");
        var mariaExe = System.IO.Path.Combine(_paths.Bin, "mariadb", "bin", "mysqld.exe");
        var phpExe = System.IO.Path.Combine(_paths.Bin, "php", "8.4", "php.exe");

        ApachePathText.Text = System.IO.File.Exists(apacheExe)
            ? "Apache: встроен и готов"
            : "Apache: компонент не найден";

        MariaDbPathText.Text = System.IO.File.Exists(mariaExe)
            ? "MariaDB: встроена и готова"
            : "MariaDB: компонент не найден";

        if (!System.IO.File.Exists(phpExe))
            AppendLog("PHP 8.4 не найден в portable-пакете.");

        var busy = IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Select(x => x.Port)
            .Where(p => p is 80 or 443 or 3306 or 8080 or 8443 or 3307)
            .Distinct()
            .OrderBy(p => p)
            .ToArray();

        PortStatusText.Text = busy.Length == 0
            ? "Основные и резервные порты свободны"
            : $"Сейчас заняты порты: {string.Join(", ", busy)}";
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        try
        {
            AppendLog("Подготовка portable-среды...");
            var prepared = await _serverManager.PrepareAsync();
            AppendLog($"Выбраны порты: HTTP {_serverManager.HttpPort}, HTTPS {_serverManager.HttpsPort}, MariaDB {_serverManager.DbPort}.");
            if (prepared.MariaDbInitialized)
                AppendLog("MariaDB инициализирована для первого запуска.");
            AppendLog($"Тестовый проект готов: {prepared.DemoProjectPath}");

            if (!_serverManager.MariaDbRunning)
                _serverManager.StartMariaDb();
            if (!_serverManager.ApacheRunning)
                _serverManager.StartApache();

            await Task.Delay(1800);
            var httpOk = await _serverManager.CheckHttpAsync();
            if (httpOk)
                AppendLog($"Проверка Apache + PHP успешна: http://127.0.0.1:{_serverManager.HttpPort}/");
            else
                AppendLog("Диагностика: Apache запущен, но HTTP-проверка не прошла. Проверьте журнал Apache.");
        }
        catch (Exception ex)
        {
            AppendLog($"Ошибка запуска: {ex.Message}");
        }
        finally
        {
            StartButton.IsEnabled = true;
            RefreshStatus();
        }
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
        RestartButton.IsEnabled = false;
        try
        {
            await _serverManager.StopAllAsync();
            await _serverManager.PrepareAsync();
            _serverManager.StartMariaDb();
            _serverManager.StartApache();
            await Task.Delay(1500);
            AppendLog(await _serverManager.CheckHttpAsync()
                ? $"Сервер перезапущен и отвечает на http://127.0.0.1:{_serverManager.HttpPort}/"
                : "Сервер перезапущен, но HTTP-проверка не прошла.");
        }
        catch (Exception ex)
        {
            AppendLog($"Ошибка перезапуска: {ex.Message}");
        }
        finally
        {
            RestartButton.IsEnabled = true;
            RefreshStatus();
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
        AppendLog("Состояние среды обновлено.");
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
