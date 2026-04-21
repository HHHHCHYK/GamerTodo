using System;
using System.IO;

namespace HeyeTodo.Client.Infrastructure;

/// <summary>
/// Resolves per-user paths for app data (local DB, settings, token store).
/// Windows: %APPDATA%/HeyeTodo
/// macOS: ~/Library/Application Support/HeyeTodo
/// Linux: ~/.config/HeyeTodo
/// </summary>
public static class AppPaths
{
    public static string DataDirectory { get; } = ResolveDataDir();

    public static string LocalDbPath => Path.Combine(DataDirectory, "heyetodo.local.db");
    public static string TokenStorePath => Path.Combine(DataDirectory, "tokens.bin");
    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public static string ClientIdPath => Path.Combine(DataDirectory, "client_id");

    private static string ResolveDataDir()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(baseDir))
        {
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }
        var dir = Path.Combine(baseDir, "HeyeTodo");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Returns persistent client installation id, creating one the first time.</summary>
    public static Guid GetOrCreateClientId()
    {
        try
        {
            if (File.Exists(ClientIdPath))
            {
                var s = File.ReadAllText(ClientIdPath).Trim();
                if (Guid.TryParse(s, out var existing)) return existing;
            }
        }
        catch { /* ignored */ }

        var id = Guid.NewGuid();
        File.WriteAllText(ClientIdPath, id.ToString("D"));
        return id;
    }
}
