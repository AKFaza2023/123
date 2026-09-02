namespace STAVCMS.LocalServer.Core;

public sealed class ServerConfig
{
    public string WebServer { get; set; } = "apache";
    public string DefaultPhp { get; set; } = "8.4";
    public string Database { get; set; } = "mariadb";
    public int HttpPort { get; set; } = 80;
    public int HttpsPort { get; set; } = 443;
    public int DbPort { get; set; } = 3306;
    public bool PortableMode { get; set; } = true;
    public bool AutoStart { get; set; } = false;
}
