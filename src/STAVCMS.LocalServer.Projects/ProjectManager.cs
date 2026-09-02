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
        _registry = System.IO.Path.Combine(paths.Config, "projects.json");
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
        if (projects.Any(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Проект с таким именем уже существует.");
        if (projects.Any(p => p.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Этот локальный домен уже используется.");

        var relative = $"projects/{id}";
        var folder = System.IO.Path.Combine(_paths.Root, "projects", id);
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(System.IO.Path.Combine(folder, "public"));
        File.WriteAllText(System.IO.Path.Combine(folder, "public", "index.php"),
            $"<?php echo '<h1>{Escape(name)}</h1><p>STAVCMS Local Server project is ready.</p><p>PHP '.PHP_VERSION.'</p>'; ?>");

        var project = new ProjectDefinition
        {
            Id = id, Name = name.Trim(), Path = relative, Domain = domain, Php = php,
            Database = id.Replace('-', '_'), Https = https, Enabled = true, Type = type
        };
        projects.Add(project);
        Save(projects);
        GenerateApacheVHosts(projects);
        return project;
    }

    public void GenerateApacheVHosts(IEnumerable<ProjectDefinition>? source = null)
    {
        var projects = source?.Where(x => x.Enabled).ToList() ?? Load().Where(x => x.Enabled).ToList();
        var generated = System.IO.Path.Combine(_paths.Runtime, "generated");
        Directory.CreateDirectory(generated);
        var file = System.IO.Path.Combine(generated, "stavcms-vhosts.conf");
        using var writer = new StreamWriter(file, false);
        foreach (var p in projects)
        {
            var root = System.IO.Path.Combine(_paths.Root, p.Path.Replace('/', System.IO.Path.DirectorySeparatorChar), "public").Replace('\\', '/');
            writer.WriteLine("<VirtualHost *:80>");
            writer.WriteLine($"  ServerName {p.Domain}");
            writer.WriteLine($"  DocumentRoot \"{root}\"");
            writer.WriteLine($"  <Directory \"{root}\">");
            writer.WriteLine("    AllowOverride All");
            writer.WriteLine("    Require all granted");
            writer.WriteLine("  </Directory>");
            writer.WriteLine("</VirtualHost>");
            writer.WriteLine();
        }
    }

    private void Save(List<ProjectDefinition> projects) =>
        File.WriteAllText(_registry, JsonSerializer.Serialize(projects, JsonOptions()));

    private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static string NormalizeDomain(string value, string id)
    {
        var domain = value.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(domain)) domain = id + ".local";
        if (!domain.Contains('.')) domain += ".local";
        return domain;
    }
    private static string Slug(string value) => string.Join('-', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').Aggregate("", (s, c) => s + c).Trim('-', '_');
    private static string Escape(string value) => value.Replace("'", "&#39;").Replace("<", "&lt;").Replace(">", "&gt;");
}
