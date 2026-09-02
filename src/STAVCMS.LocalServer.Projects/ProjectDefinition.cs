namespace STAVCMS.LocalServer.Projects;

public sealed class ProjectDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Php { get; set; } = "8.4";
    public string Database { get; set; } = "";
    public bool Https { get; set; }
    public bool Enabled { get; set; } = true;
    public string Type { get; set; } = "stavcms";
}
