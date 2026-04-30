using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HeyeTodo.Client.Persistence;

public sealed class FilePersistenceStore : IPersistenceStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly Regex SafeModuleIdPattern = new("^[a-zA-Z0-9._-]+$", RegexOptions.Compiled);

    private readonly string _persistenceDirectory;

    public FilePersistenceStore()
    {
        var appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _persistenceDirectory = Path.Combine(appDataDirectory, "HeyeTodo", "persistence");
    }

    public async Task<T?> LoadAsync<T>(string moduleId, CancellationToken cancellationToken = default)
    {
        var filePath = ResolveModuleFilePath(moduleId);
        if (!File.Exists(filePath))
        {
            return default;
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new PersistenceException($"模块 {moduleId} 的持久化文件无法解析，原文件已保留。", exception);
        }
        catch (IOException exception)
        {
            throw new PersistenceException($"模块 {moduleId} 的持久化文件读取失败，原文件已保留。", exception);
        }
    }

    public async Task SaveAsync<T>(string moduleId, T data, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_persistenceDirectory);

        var filePath = ResolveModuleFilePath(moduleId);
        var temporaryFilePath = $"{filePath}.tmp";

        try
        {
            await using (var stream = File.Create(temporaryFilePath))
            {
                await JsonSerializer.SerializeAsync(stream, data, SerializerOptions, cancellationToken);
            }

            File.Move(temporaryFilePath, filePath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            TryDeleteTemporaryFile(temporaryFilePath);
            throw new PersistenceException($"模块 {moduleId} 的持久化文件保存失败，原文件已保留。", exception);
        }
    }

    private string ResolveModuleFilePath(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId) || !SafeModuleIdPattern.IsMatch(moduleId))
        {
            throw new PersistenceException("模块持久化标识只能包含字母、数字、点、下划线和短横线。");
        }

        return Path.Combine(_persistenceDirectory, $"{moduleId}.json");
    }

    private static void TryDeleteTemporaryFile(string temporaryFilePath)
    {
        try
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
        catch
        {
        }
    }
}
