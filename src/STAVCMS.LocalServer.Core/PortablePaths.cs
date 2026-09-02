namespace STAVCMS.LocalServer.Core;

public sealed class PortablePaths
{
    public string Root { get; }
    public string Config => Path.Combine(Root, "config");
    public string Logs => Path.Combine(Root, "logs");
    public string Runtime => Path.Combine(Root, "runtime");
    public string Bin => Path.Combine(Root, "bin");
    public string Projects => Path.Combine(Root, "projects");
    public string Databases => Path.Combine(Root, "databases");
    public string Backups => Path.Combine(Root, "backups");
    public string Ssl => Path.Combine(Root, "ssl");

    public PortablePaths(string? root = null)
    {
        Root = Path.GetFullPath(root ?? AppContext.BaseDirectory);
    }

    public void EnsureDirectories()
    {
        foreach (var path in new[] { Config, Logs, Runtime, Bin, Projects, Databases, Backups, Ssl })
            Directory.CreateDirectory(path);
    }
}
