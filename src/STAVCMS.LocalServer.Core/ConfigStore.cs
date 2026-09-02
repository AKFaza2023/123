using System.Text.Json;

namespace STAVCMS.LocalServer.Core;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ServerConfig LoadServerConfig(string file)
    {
        if (!File.Exists(file)) return new ServerConfig();
        var json = File.ReadAllText(file);
        return JsonSerializer.Deserialize<ServerConfig>(json, JsonOptions) ?? new ServerConfig();
    }

    public void SaveServerConfig(string file, ServerConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(config, JsonOptions));
    }
}
