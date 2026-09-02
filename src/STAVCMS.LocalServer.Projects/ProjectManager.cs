using System.Text.Json;
using STAVCMS.LocalServer.Core;

namespace STAVCMS.LocalServer.Projects;

public sealed class ProjectManager
{
    private readonly PortablePaths _paths;
    private readonly string _registry;

    public ProjectManager(PortablePaths paths)
    {
        _paths = paths;
        _registry = Path.Combine(paths.Config, "projects.json");
    }

    public IReadOnlyList<ProjectDefinition> Load()
    {
        if (!File.Exists(_registry)) return Array.Empty<ProjectDefinition>();
        return JsonSerializer.Deserialize<List<ProjectDefinition>>(File.ReadAllText(_registry), JsonOptions()) ?? [];
    }

    public ProjectDefinition Create(string name, string domain, string php = "8.4", bool https = false, string type = "stavcms")
    {
        var id = Slug(name);
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Не удалось сформировать идентификатор проекта.");
        domain = NormalizeDomain(domain, id);
        var projects = Load().ToList();
        if (projects.Any(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Проект с таким именем уже существует.");
        if (projects.Any(p => p.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Этот локальный домен уже используется.");

        var relative = $"projects/{id}";
        var folder = Path.Combine(_paths.Root, "projects", id);
        Directory.CreateDirectory(Path.Combine(folder, "public"));
        File.WriteAllText(Path.Combine(folder, "public", "index.php"), $"<?php echo '<h1>{Escape(name)}</h1><p>STAVCMS Local Server project is ready.</p><p>PHP '.PHP_VERSION.'</p>'; ?>");

        var project = new ProjectDefinition { Id = id, Name = name.Trim(), Path = relative, Domain = domain, Php = php, Database = id.Replace('-', '_'), Https = https, Enabled = true, Type = type };
        projects.Add(project);
        Save(projects);
        return project;
    }

    public ProjectDefinition Import(string sourceFolder, string name, string domain, string php = "8.4")
    {
        if (!Directory.Exists(sourceFolder)) throw new DirectoryNotFoundException("Папка импортируемого проекта не найдена.");
        var project = Create(name, domain, php, false, "imported");
        var destination = Path.Combine(_paths.Root, project.Path.Replace('/', Path.DirectorySeparatorChar), "public");
        CopyDirectory(sourceFolder, destination);
        return project;
    }

    public string Archive(ProjectDefinition project)
    {
        var source = Path.Combine(_paths.Root, project.Path.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("Папка проекта не найдена.");
        Directory.CreateDirectory(_paths.Backups);
        var zip = Path.Combine(_paths.Backups, $"{project.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(source, zip, System.IO.Compression.CompressionLevel.Optimal, false);
        return zip;
    }

    public void Remove(ProjectDefinition project, bool deleteFiles)
    {
        var projects = Load().Where(p => !p.Id.Equals(project.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        Save(projects);
        if (deleteFiles)
        {
            var folder = Path.Combine(_paths.Root, project.Path.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    public void GenerateApacheVHosts(int httpPort, int httpsPort)
    {
        var projects = Load().Where(x => x.Enabled).ToList();
        var generated = Path.Combine(_paths.Runtime, "generated");
        Directory.CreateDirectory(generated);
        var file = Path.Combine(generated, "stavcms-vhosts.conf");
        using var writer = new StreamWriter(file, false);
        foreach (var p in projects)
        {
            var root = Path.Combine(_paths.Root, p.Path.Replace('/', Path.DirectorySeparatorChar), "public").Replace('\\', '/');
            writer.WriteLine($"<VirtualHost *:{httpPort}>");
            writer.WriteLine($"  ServerName {p.Domain}");
            writer.WriteLine($"  DocumentRoot \"{root}\"");
            writer.WriteLine($"  <Directory \"{root}\">");
            writer.WriteLine("    AllowOverride All");
            writer.WriteLine("    Require all granted");
            writer.WriteLine("  </Directory>");
            writer.WriteLine("</VirtualHost>");
            writer.WriteLine();

            if (p.Https)
            {
                var cert = Path.Combine(_paths.Ssl, p.Domain + ".crt.pem").Replace('\\', '/');
                var key = Path.Combine(_paths.Ssl, p.Domain + ".key.pem").Replace('\\', '/');
                if (File.Exists(cert) && File.Exists(key))
                {
                    writer.WriteLine($"<VirtualHost *:{httpsPort}>");
                    writer.WriteLine($"  ServerName {p.Domain}");
                    writer.WriteLine($"  DocumentRoot \"{root}\"");
                    writer.WriteLine("  SSLEngine on");
                    writer.WriteLine($"  SSLCertificateFile \"{cert}\"");
                    writer.WriteLine($"  SSLCertificateKeyFile \"{key}\"");
                    writer.WriteLine($"  <Directory \"{root}\">");
                    writer.WriteLine("    AllowOverride All");
                    writer.WriteLine("    Require all granted");
                    writer.WriteLine("  </Directory>");
                    writer.WriteLine("</VirtualHost>");
                    writer.WriteLine();
                }
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source)) CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private void Save(List<ProjectDefinition> projects) => File.WriteAllText(_registry, JsonSerializer.Serialize(projects, JsonOptions()));
    private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static string NormalizeDomain(string value, string id)
    {
        var domain = value.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(domain)) domain = id + ".local";
        if (!domain.Contains('.')) domain += ".local";
        return domain;
    }
    private static string Slug(string value) => string.Join('-', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').Aggregate("", (s, c) => s + c).Trim('-', '_');
    private static string Escape(string value) => value.Replace("'", "&#39;").Replace("<", "&lt;").Replace(">", "&gt;");
}
