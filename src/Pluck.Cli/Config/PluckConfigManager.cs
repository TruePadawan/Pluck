using System.Text.Json;
using Pluck.Cli.Utils;

namespace Pluck.Cli.Config;

public record PluckConfig(string ServerUrl, string ApiUrl);

public static class PluckConfigManager
{
    public static void Save(PluckConfig config)
    {
        var jsonString = JsonSerializer.Serialize(config);
        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pluck");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "config.json");
        File.WriteAllText(configPath, jsonString);
        SpectreOutput.Info($"Config saved to {configPath}");
    }

    private static PluckConfig? GetConfig()
    {
        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pluck");
        var configPath = Path.Combine(configDir, "config.json");
        if (!Path.Exists(configPath)) return null;
        var config = JsonSerializer.Deserialize<PluckConfig>(File.ReadAllText(configPath));
        return config;
    }

    public static PluckConfig GetConfigOrThrow()
    {
        var config = GetConfig();
        if (config is null)
            throw new InvalidOperationException(
                "Pluck config not found. Please run 'pluck config' to set up the config.");
        return config;
    }
}