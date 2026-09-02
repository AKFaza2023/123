using System.Diagnostics;
using System.Net.NetworkInformation;
using STAVCMS.LocalServer.Core;

namespace STAVCMS.LocalServer.Server;

public sealed record BootstrapResult(int HttpPort, int HttpsPort, int DbPort, bool MariaDbInitialized, string DemoProjectPath);

public sealed class EnvironmentBootstrapper
{
    private readonly PortablePaths _paths;
    public EnvironmentBootstrapper(PortablePaths paths) => _paths = paths;

    public async Task<BootstrapResult> PrepareAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();
        var httpPort = ChoosePort(80, 8080);
        var httpsPort = ChoosePort(443, 8443);
        var dbPort = ChoosePort(3306, 3307);
        var demoProject = EnsureDemoProject();
        WriteApacheConfig(httpPort, httpsPort, demoProject);
        WriteMariaDbConfig(dbPort);
        var initialized = await EnsureMariaDbDataAsync(cancellationToken);
        return new BootstrapResult(httpPort, httpsPort, dbPort, initialized, demoProject);
    }

    private static int ChoosePort(int primary, int fallback)
    {
        var busy = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(x => x.Port).ToHashSet();
        if (!busy.Contains(primary)) return primary;
        if (!busy.Contains(fallback)) return fallback;
        for (var port = fallback + 1; port < fallback + 100; port++) if (!busy.Contains(port)) return port;
        throw new InvalidOperationException($"Не удалось найти свободный порт рядом с {primary}/{fallback}.");
    }

    private string EnsureDemoProject()
    {
        var project = Path.Combine(_paths.Projects, "stavcms-demo");
        Directory.CreateDirectory(project);
        var index = Path.Combine(project, "index.php");
        if (!File.Exists(index)) File.WriteAllText(index, "<?php header('Content-Type: text/html; charset=utf-8'); echo '<h1>STAVCMS Local Server работает</h1><p>PHP '.PHP_VERSION.'</p>'; ?>");
        return project;
    }

    private void WriteApacheConfig(int httpPort, int httpsPort, string documentRoot)
    {
        var apache = Path.Combine(_paths.Bin, "apache");
        var confDir = Path.Combine(apache, "conf");
        Directory.CreateDirectory(confDir);
        var php = Path.Combine(_paths.Bin, "php", "8.4");
        static string P(string path) => path.Replace('\\', '/');
        var vhosts = Path.Combine(_paths.Runtime, "generated", "stavcms-vhosts.conf");
        Directory.CreateDirectory(Path.GetDirectoryName(vhosts)!);
        if (!File.Exists(vhosts)) File.WriteAllText(vhosts, "# STAVCMS generated virtual hosts\n");

        var conf = $"""
ServerRoot "{P(apache)}"
Listen {httpPort}
Listen {httpsPort}
ServerName localhost:{httpPort}
LoadModule access_compat_module modules/mod_access_compat.so
LoadModule actions_module modules/mod_actions.so
LoadModule alias_module modules/mod_alias.so
LoadModule auth_basic_module modules/mod_auth_basic.so
LoadModule authn_core_module modules/mod_authn_core.so
LoadModule authn_file_module modules/mod_authn_file.so
LoadModule authz_core_module modules/mod_authz_core.so
LoadModule authz_host_module modules/mod_authz_host.so
LoadModule authz_user_module modules/mod_authz_user.so
LoadModule dir_module modules/mod_dir.so
LoadModule env_module modules/mod_env.so
LoadModule headers_module modules/mod_headers.so
LoadModule log_config_module modules/mod_log_config.so
LoadModule mime_module modules/mod_mime.so
LoadModule rewrite_module modules/mod_rewrite.so
LoadModule setenvif_module modules/mod_setenvif.so
LoadModule socache_shmcb_module modules/mod_socache_shmcb.so
LoadModule ssl_module modules/mod_ssl.so
LoadModule php_module "{P(Path.Combine(php, "php8apache2_4.dll"))}"
PHPIniDir "{P(php)}"
DirectoryIndex index.php index.html
TypesConfig conf/mime.types
AddType application/x-httpd-php .php
DocumentRoot "{P(documentRoot)}"
<Directory "{P(documentRoot)}">
    Options Indexes FollowSymLinks
    AllowOverride All
    Require all granted
</Directory>
ErrorLog "{P(Path.Combine(_paths.Logs, "apache-error.log"))}"
CustomLog "{P(Path.Combine(_paths.Logs, "apache-access.log"))}" common
SSLSessionCache "shmcb:{P(Path.Combine(_paths.Runtime, "ssl_scache"))}(512000)"
IncludeOptional "{P(vhosts)}"
""";
        File.WriteAllText(Path.Combine(confDir, "httpd-stavcms.conf"), conf);
    }

    private void WriteMariaDbConfig(int dbPort)
    {
        var maria = Path.Combine(_paths.Bin, "mariadb");
        var data = Path.Combine(_paths.Databases, "mariadb-data");
        Directory.CreateDirectory(data);
        static string P(string path) => path.Replace('\\', '/');
        var ini = $"""
[mysqld]
port={dbPort}
basedir={P(maria)}
datadir={P(data)}
character-set-server=utf8mb4
collation-server=utf8mb4_unicode_ci
skip-name-resolve

[client]
port={dbPort}
default-character-set=utf8mb4
""";
        File.WriteAllText(Path.Combine(maria, "my.ini"), ini);
    }

    private async Task<bool> EnsureMariaDbDataAsync(CancellationToken cancellationToken)
    {
        var data = Path.Combine(_paths.Databases, "mariadb-data");
        if (Directory.Exists(Path.Combine(data, "mysql"))) return false;
        var bin = Path.Combine(_paths.Bin, "mariadb", "bin");
        var installer = new[] { "mariadb-install-db.exe", "mysql_install_db.exe" }.Select(name => Path.Combine(bin, name)).FirstOrDefault(File.Exists);
        if (installer is null) throw new FileNotFoundException("Не найден инструмент инициализации MariaDB.");
        var psi = new ProcessStartInfo { FileName = installer, Arguments = $"--datadir=\"{data}\" --password= --auth-root-authentication-method=normal", WorkingDirectory = Path.GetDirectoryName(installer)!, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Не удалось запустить инициализацию MariaDB.");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Инициализация MariaDB завершилась с ошибкой {process.ExitCode}: {error} {output}".Trim());
        }
        return true;
    }
}
