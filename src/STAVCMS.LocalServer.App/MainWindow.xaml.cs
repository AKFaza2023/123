using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using STAVCMS.LocalServer.Core;
using STAVCMS.LocalServer.Projects;
using STAVCMS.LocalServer.Server;
using STAVCMS.LocalServer.Windows;

namespace STAVCMS.LocalServer.App;

public partial class MainWindow : Window
{
    private readonly PortablePaths _paths;
    private readonly ServerManager _serverManager;
    private readonly ProjectManager _projects;
    private readonly ElevatedHelperClient _windows;

    public MainWindow()
    {
        InitializeComponent();
        _paths = new PortablePaths(AppContext.BaseDirectory);
        _paths.EnsureDirectories();
        _serverManager = new ServerManager(_paths);
        _projects = new ProjectManager(_paths);
        _windows = new ElevatedHelperClient(_paths);
        RootPathText.Text = _paths.Root;
        RefreshStatus();
        RefreshProjects();
        AppendLog("STAVCMS Local Server 0.6 запущен.");
    }

    private void RefreshProjects()
    {
        ProjectsList.ItemsSource = null;
        ProjectsList.ItemsSource = _projects.Load();
        if (ProjectsList.Items.Count > 0) ProjectsList.SelectedIndex = 0;
    }

    private ProjectDefinition? SelectedProject => ProjectsList.SelectedItem as ProjectDefinition;

    private void ProjectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var p = SelectedProject;
        SelectedProjectText.Text = p == null ? "Проект не выбран" : $"{p.Domain}  •  PHP {p.Php}  •  БД: {p.Database}\n{p.Path}";
    }

    private async void CreateProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var php = (PhpBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "8.4";
            var type = (ProjectTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "stavcms";
            var p = _projects.Create(ProjectNameBox.Text, ProjectDomainBox.Text, php, HttpsBox.IsChecked == true, type);
            AppendLog($"Проект создан: {p.Name} ({p.Domain}).");

            AppendLog("Запрос Windows UAC для регистрации локального домена" + (p.Https ? " и SSL..." : "..."));
            await _windows.RegisterProjectAsync(p.Domain, p.Https);
            AppendLog(p.Https ? "Домен зарегистрирован, SSL выпущен и добавлен в доверенные." : "Домен зарегистрирован в hosts.");

            await _serverManager.PrepareAsync();
            _projects.GenerateApacheVHosts(_serverManager.HttpPort, _serverManager.HttpsPort);
            if (!_serverManager.MariaDbRunning)
            {
                _serverManager.StartMariaDb();
                await Task.Delay(1200);
            }
            await _serverManager.CreateDatabaseAsync(p.Database);
            AppendLog($"База MariaDB создана: {p.Database}.");

            if (_serverManager.ApacheRunning)
            {
                await _serverManager.StopAllAsync();
                _serverManager.StartMariaDb();
            }
            _serverManager.StartApache();
            AppendLog("VirtualHost активирован. Проект готов к работе.");
            RefreshProjects();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            AppendLog("Регистрация Windows отменена пользователем. Проект сохранён, но локальный домен пока не активен.");
            RefreshProjects();
        }
        catch (Exception ex) { AppendLog($"Ошибка создания/настройки проекта: {ex.Message}"); RefreshProjects(); }
        finally { RefreshStatus(); }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var p = SelectedProject; if (p == null) return;
        var path = Path.Combine(_paths.Root, p.Path.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private void OpenSite_Click(object sender, RoutedEventArgs e)
    {
        var p = SelectedProject; if (p == null) return;
        var scheme = p.Https ? "https" : "http";
        var port = p.Https ? _serverManager.HttpsPort : _serverManager.HttpPort;
        var suffix = (scheme == "https" && port == 443) || (scheme == "http" && port == 80) ? "" : $":{port}";
        Process.Start(new ProcessStartInfo($"{scheme}://{p.Domain}{suffix}") { UseShellExecute = true });
    }

    private void RefreshStatus()
    {
        ApacheStatusText.Text = _serverManager.ApacheRunning ? "Работает" : "Остановлен";
        MariaDbStatusText.Text = _serverManager.MariaDbRunning ? "Работает" : "Остановлена";
        var apacheExe = Path.Combine(_paths.Bin, "apache", "bin", "httpd.exe");
        var mariaExe = Path.Combine(_paths.Bin, "mariadb", "bin", "mysqld.exe");
        ApachePathText.Text = File.Exists(apacheExe) ? "Apache: встроен и готов" : "Apache: компонент не найден";
        MariaDbPathText.Text = File.Exists(mariaExe) ? "MariaDB: встроена и готова" : "MariaDB: компонент не найден";
        var busy = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(x => x.Port).Where(p => p is 80 or 443 or 3306 or 8080 or 8443 or 3307).Distinct().OrderBy(p => p).ToArray();
        PortStatusText.Text = busy.Length == 0 ? "Основные и резервные порты свободны" : $"Сейчас заняты порты: {string.Join(", ", busy)}";
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        try
        {
            await _serverManager.PrepareAsync();
            _projects.GenerateApacheVHosts(_serverManager.HttpPort, _serverManager.HttpsPort);
            if (!_serverManager.MariaDbRunning) _serverManager.StartMariaDb();
            if (!_serverManager.ApacheRunning) _serverManager.StartApache();
            await Task.Delay(1500);
            AppendLog(await _serverManager.CheckHttpAsync() ? "Apache + PHP отвечают." : "HTTP-проверка не прошла — смотрите журнал Apache.");
        }
        catch (Exception ex) { AppendLog($"Ошибка запуска: {ex.Message}"); }
        finally { StartButton.IsEnabled = true; RefreshStatus(); }
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        try { await _serverManager.StopAllAsync(); AppendLog("Apache и MariaDB остановлены."); }
        catch (Exception ex) { AppendLog($"Ошибка остановки: {ex.Message}"); }
        finally { RefreshStatus(); }
    }

    private async void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _serverManager.StopAllAsync();
            await _serverManager.PrepareAsync();
            _projects.GenerateApacheVHosts(_serverManager.HttpPort, _serverManager.HttpsPort);
            _serverManager.StartMariaDb();
            _serverManager.StartApache();
            AppendLog("Сервер перезапущен.");
        }
        catch (Exception ex) { AppendLog($"Ошибка перезапуска: {ex.Message}"); }
        finally { RefreshStatus(); }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) { RefreshStatus(); RefreshProjects(); AppendLog("Данные обновлены."); }
    private void AppendLog(string message) { LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"); LogBox.ScrollToEnd(); }
    protected override async void OnClosed(EventArgs e) { await _serverManager.DisposeAsync(); base.OnClosed(e); }
}
