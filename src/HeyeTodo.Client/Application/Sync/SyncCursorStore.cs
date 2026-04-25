using System.Text.Json;
using HeyeTodo.Client.Infrastructure;

namespace HeyeTodo.Client.Application.Sync;

public sealed class SyncCursorStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object _gate = new();

    public long Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(AppPaths.SyncCursorPath))
                {
                    return 0;
                }

                var json = File.ReadAllText(AppPaths.SyncCursorPath);
                return JsonSerializer.Deserialize<SyncCursorState>(json, JsonOptions)?.ServerVersion ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    public void Save(long serverVersion)
    {
        lock (_gate)
        {
            var json = JsonSerializer.Serialize(new SyncCursorState(serverVersion), JsonOptions);
            File.WriteAllText(AppPaths.SyncCursorPath, json);
        }
    }

    private sealed record SyncCursorState(long ServerVersion);
}
