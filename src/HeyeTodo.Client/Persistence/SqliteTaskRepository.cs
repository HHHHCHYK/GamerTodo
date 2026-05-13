using System.Globalization;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using HeyeTodo.Shared.Contracts.Sync;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Enums;
using HeyeTodo.Shared.Sync;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Client.Persistence;

public sealed class SqliteTaskRepository : ITaskRepository
{
    private const string LastPulledServerVersionKey = "sync.lastPulledServerVersion";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _databasePath;

    public SqliteTaskRepository()
    {
        SQLitePCL.Batteries_V2.Init();

        var appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(appDataDirectory, "HeyeTodo");
        Directory.CreateDirectory(directory);
        _databasePath = Path.Combine(directory, "heyetodo.db");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            PRAGMA journal_mode=WAL;
            """, cancellationToken: cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS Projects (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                ServerVersion INTEGER NOT NULL DEFAULT 0,
                DeletedAt TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS Tasks (
                Id TEXT PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                ProjectNameSnapshot TEXT NOT NULL,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL,
                IsCompleted INTEGER NOT NULL,
                SortId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                StartTime TEXT NULL,
                EndTime TEXT NULL,
                AssigneeName TEXT NOT NULL,
                Urgency INTEGER NOT NULL,
                ServerVersion INTEGER NOT NULL DEFAULT 0,
                DeletedAt TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS SyncMetadata (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS SyncOutbox (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EntityType INTEGER NOT NULL,
                Operation INTEGER NOT NULL,
                EntityId TEXT NOT NULL,
                PayloadJson TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Tasks_ProjectId ON Tasks(ProjectId);
            CREATE INDEX IF NOT EXISTS IX_SyncOutbox_Entity ON SyncOutbox(EntityType, EntityId);
            """, cancellationToken: cancellationToken);
    }

    public async Task<TaskWorkspaceState> LoadWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var projects = new List<TaskProjectRecord>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT Id, Name, Description, CreatedAt, UpdatedAt, ServerVersion, DeletedAt
                FROM Projects
                WHERE DeletedAt IS NULL
                ORDER BY Name;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                projects.Add(new TaskProjectRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    ParseDate(reader.GetString(3)),
                    ParseDate(reader.GetString(4)),
                    reader.GetInt64(5),
                    ReadNullableDate(reader, 6)));
            }
        }

        var tasks = new List<TaskItemRecord>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT Id, ProjectId, ProjectNameSnapshot, Name, Description, IsCompleted, SortId, CreatedAt,
                       UpdatedAt, StartTime, EndTime, AssigneeName, Urgency, ServerVersion, DeletedAt
                FROM Tasks
                WHERE DeletedAt IS NULL
                ORDER BY SortId, CreatedAt;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tasks.Add(new TaskItemRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetInt32(5) != 0,
                    reader.GetInt32(6),
                    ParseDate(reader.GetString(7)),
                    ParseDate(reader.GetString(8)),
                    ReadNullableDate(reader, 9),
                    ReadNullableDate(reader, 10),
                    reader.GetString(11),
                    (ViewModels.TaskUrgencyLevel)reader.GetInt32(12),
                    reader.GetInt64(13),
                    ReadNullableDate(reader, 14)));
            }
        }

        return new TaskWorkspaceState(projects, tasks);
    }

    public async Task SaveProjectAsync(TaskProjectRecord project, bool enqueueSyncChange, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            INSERT INTO Projects (Id, Name, Description, CreatedAt, UpdatedAt, ServerVersion, DeletedAt)
            VALUES ($id, $name, $description, $createdAt, $updatedAt, $serverVersion, $deletedAt)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                Description = excluded.Description,
                UpdatedAt = excluded.UpdatedAt,
                ServerVersion = excluded.ServerVersion,
                DeletedAt = excluded.DeletedAt;
            """,
            transaction,
            cancellationToken,
            ("$id", project.Id),
            ("$name", project.Name),
            ("$description", project.Description),
            ("$createdAt", FormatDate(project.CreatedAt)),
            ("$updatedAt", FormatDate(project.UpdatedAt)),
            ("$serverVersion", project.ServerVersion),
            ("$deletedAt", FormatNullableDate(project.DeletedAt)));

        if (enqueueSyncChange)
        {
            await InsertOutboxAsync(connection, transaction, ChangeEntityType.Project, ChangeOperation.Upsert, project.Id, SerializeProject(project), project.UpdatedAt, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveTaskAsync(TaskItemRecord task, bool enqueueSyncChange, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            INSERT INTO Tasks (Id, ProjectId, ProjectNameSnapshot, Name, Description, IsCompleted, SortId, CreatedAt, UpdatedAt,
                               StartTime, EndTime, AssigneeName, Urgency, ServerVersion, DeletedAt)
            VALUES ($id, $projectId, $projectNameSnapshot, $name, $description, $isCompleted, $sortId, $createdAt, $updatedAt,
                    $startTime, $endTime, $assigneeName, $urgency, $serverVersion, $deletedAt)
            ON CONFLICT(Id) DO UPDATE SET
                ProjectId = excluded.ProjectId,
                ProjectNameSnapshot = excluded.ProjectNameSnapshot,
                Name = excluded.Name,
                Description = excluded.Description,
                IsCompleted = excluded.IsCompleted,
                SortId = excluded.SortId,
                UpdatedAt = excluded.UpdatedAt,
                StartTime = excluded.StartTime,
                EndTime = excluded.EndTime,
                AssigneeName = excluded.AssigneeName,
                Urgency = excluded.Urgency,
                ServerVersion = excluded.ServerVersion,
                DeletedAt = excluded.DeletedAt;
            """,
            transaction,
            cancellationToken,
            ("$id", task.Id),
            ("$projectId", task.ProjectId),
            ("$projectNameSnapshot", task.ProjectNameSnapshot),
            ("$name", task.Name),
            ("$description", task.Description),
            ("$isCompleted", task.IsCompleted ? 1 : 0),
            ("$sortId", task.SortId),
            ("$createdAt", FormatDate(task.CreatedAt)),
            ("$updatedAt", FormatDate(task.UpdatedAt)),
            ("$startTime", FormatNullableDate(task.StartTime)),
            ("$endTime", FormatNullableDate(task.EndTime)),
            ("$assigneeName", task.AssigneeName),
            ("$urgency", (int)task.Urgency),
            ("$serverVersion", task.ServerVersion),
            ("$deletedAt", FormatNullableDate(task.DeletedAt)));

        if (enqueueSyncChange)
        {
            await InsertOutboxAsync(connection, transaction, ChangeEntityType.TodoTask, ChangeOperation.Upsert, task.Id, SerializeTask(task), task.UpdatedAt, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public Task ApplyRemoteProjectAsync(TaskProjectRecord project, CancellationToken cancellationToken = default)
        => SaveProjectAsync(project, enqueueSyncChange: false, cancellationToken);

    public Task ApplyRemoteTaskAsync(TaskItemRecord task, CancellationToken cancellationToken = default)
        => SaveTaskAsync(task, enqueueSyncChange: false, cancellationToken);

    public async Task ApplyRemoteDeleteAsync(string entityId, ChangeEntityType entityType, DateTimeOffset deletedAt, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var table = entityType switch
        {
            ChangeEntityType.Project => "Projects",
            ChangeEntityType.TodoTask => "Tasks",
            _ => null,
        };

        if (table is null)
        {
            return;
        }

        await ExecuteNonQueryAsync(connection, $"UPDATE {table} SET DeletedAt = $deletedAt, UpdatedAt = $deletedAt WHERE Id = $id;", null, cancellationToken, ("$id", entityId), ("$deletedAt", FormatDate(deletedAt)));
    }

    public async Task SoftDeleteTaskAsync(string taskId, DateTimeOffset deletedAt, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            UPDATE Tasks SET DeletedAt = $deletedAt, UpdatedAt = $deletedAt WHERE Id = $id;
            """,
            transaction,
            cancellationToken,
            ("$id", taskId),
            ("$deletedAt", FormatDate(deletedAt)));

        await InsertOutboxAsync(connection, transaction, ChangeEntityType.TodoTask, ChangeOperation.Delete, taskId, "{}", deletedAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReplaceWorkspaceFromLocalImportAsync(TaskWorkspaceState state, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteNonQueryAsync(connection, "DELETE FROM Tasks;", transaction, cancellationToken);
        await ExecuteNonQueryAsync(connection, "DELETE FROM Projects;", transaction, cancellationToken);

        foreach (var project in state.Projects)
        {
            await SaveProjectInsideTransactionAsync(connection, transaction, project, cancellationToken);
        }

        foreach (var task in state.Tasks)
        {
            await SaveTaskInsideTransactionAsync(connection, transaction, task, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<long> GetLastPulledServerVersionAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM SyncMetadata WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", LastPulledServerVersionKey);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string text && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var serverVersion)
            ? serverVersion
            : 0L;
    }

    public async Task SetLastPulledServerVersionAsync(long serverVersion, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ExecuteNonQueryAsync(connection, """
            INSERT INTO SyncMetadata (Key, Value)
            VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """,
            null,
            cancellationToken,
            ("$key", LastPulledServerVersionKey),
            ("$value", serverVersion.ToString(CultureInfo.InvariantCulture)));
    }

    public async Task<IReadOnlyList<SyncOutboxRecord>> LoadPendingOutboxAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, EntityType, Operation, EntityId, PayloadJson, UpdatedAt
            FROM SyncOutbox
            ORDER BY Id;
            """;
        var records = new List<SyncOutboxRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new SyncOutboxRecord(
                reader.GetInt64(0),
                (ChangeEntityType)reader.GetInt32(1),
                (ChangeOperation)reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                ParseDate(reader.GetString(5))));
        }

        return records;
    }

    public async Task DeleteOutboxEntriesAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
        {
            return;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var id in idList)
        {
            await ExecuteNonQueryAsync(connection, "DELETE FROM SyncOutbox WHERE Id = $id;", transaction, cancellationToken, ("$id", id));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM SyncMetadata WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task SetSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ExecuteNonQueryAsync(connection, """
            INSERT INTO SyncMetadata (Key, Value)
            VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """,
            null,
            cancellationToken,
            ("$key", key),
            ("$value", value));
    }

    private async Task SaveProjectInsideTransactionAsync(SqliteConnection connection, DbTransaction transaction, TaskProjectRecord project, CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(connection, """
            INSERT INTO Projects (Id, Name, Description, CreatedAt, UpdatedAt, ServerVersion, DeletedAt)
            VALUES ($id, $name, $description, $createdAt, $updatedAt, $serverVersion, $deletedAt);
            """,
            transaction,
            cancellationToken,
            ("$id", project.Id),
            ("$name", project.Name),
            ("$description", project.Description),
            ("$createdAt", FormatDate(project.CreatedAt)),
            ("$updatedAt", FormatDate(project.UpdatedAt)),
            ("$serverVersion", project.ServerVersion),
            ("$deletedAt", FormatNullableDate(project.DeletedAt)));
    }

    private async Task SaveTaskInsideTransactionAsync(SqliteConnection connection, DbTransaction transaction, TaskItemRecord task, CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(connection, """
            INSERT INTO Tasks (Id, ProjectId, ProjectNameSnapshot, Name, Description, IsCompleted, SortId, CreatedAt, UpdatedAt,
                               StartTime, EndTime, AssigneeName, Urgency, ServerVersion, DeletedAt)
            VALUES ($id, $projectId, $projectNameSnapshot, $name, $description, $isCompleted, $sortId, $createdAt, $updatedAt,
                    $startTime, $endTime, $assigneeName, $urgency, $serverVersion, $deletedAt);
            """,
            transaction,
            cancellationToken,
            ("$id", task.Id),
            ("$projectId", task.ProjectId),
            ("$projectNameSnapshot", task.ProjectNameSnapshot),
            ("$name", task.Name),
            ("$description", task.Description),
            ("$isCompleted", task.IsCompleted ? 1 : 0),
            ("$sortId", task.SortId),
            ("$createdAt", FormatDate(task.CreatedAt)),
            ("$updatedAt", FormatDate(task.UpdatedAt)),
            ("$startTime", FormatNullableDate(task.StartTime)),
            ("$endTime", FormatNullableDate(task.EndTime)),
            ("$assigneeName", task.AssigneeName),
            ("$urgency", (int)task.Urgency),
            ("$serverVersion", task.ServerVersion),
            ("$deletedAt", FormatNullableDate(task.DeletedAt)));
    }

    private static async Task InsertOutboxAsync(
        SqliteConnection connection,
        DbTransaction transaction,
        ChangeEntityType entityType,
        ChangeOperation operation,
        string entityId,
        string payloadJson,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(connection, """
            INSERT INTO SyncOutbox (EntityType, Operation, EntityId, PayloadJson, UpdatedAt)
            VALUES ($entityType, $operation, $entityId, $payloadJson, $updatedAt);
            """,
            transaction,
            cancellationToken,
            ("$entityType", (int)entityType),
            ("$operation", (int)operation),
            ("$entityId", entityId),
            ("$payloadJson", payloadJson),
            ("$updatedAt", FormatDate(updatedAt)));
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string commandText,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.Transaction = (SqliteTransaction?)transaction;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string FormatDate(DateTimeOffset value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static object? FormatNullableDate(DateTimeOffset? value)
        => value is null ? null : FormatDate(value.Value);

    private static DateTimeOffset ParseDate(string value)
        => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static DateTimeOffset? ReadNullableDate(SqliteDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : ParseDate(reader.GetString(ordinal));

    private static string SerializeProject(TaskProjectRecord project)
    {
        var projectId = Guid.Parse(project.Id);
        return JsonSerializer.Serialize(new ProjectDto(
            projectId,
            Guid.Empty,
            project.Name,
            project.Description,
            project.CreatedAt,
            new SyncMeta
            {
                ServerVersion = project.ServerVersion,
                UpdatedAt = project.UpdatedAt,
                UpdatedBy = Guid.Empty,
                ClientId = Guid.Empty,
                DeletedAt = project.DeletedAt,
            }), JsonOptions);
    }

    private static string SerializeTask(TaskItemRecord task)
    {
        return JsonSerializer.Serialize(new TaskDto(
            Guid.Parse(task.Id),
            Guid.Parse(task.ProjectId),
            task.Name,
            task.Description,
            task.IsCompleted ? TaskStatus.Done : TaskStatus.Backlog,
            ToTaskPriority(task.Urgency),
            task.StartTime,
            task.EndTime,
            null,
            null,
            string.IsNullOrWhiteSpace(task.AssigneeName)
                ? null
                : new Dictionary<string, object?> { ["assigneeName"] = task.AssigneeName },
            new SyncMeta
            {
                ServerVersion = task.ServerVersion,
                UpdatedAt = task.UpdatedAt,
                UpdatedBy = Guid.Empty,
                ClientId = Guid.Empty,
                DeletedAt = task.DeletedAt,
            }), JsonOptions);
    }

    private static TaskPriority ToTaskPriority(ViewModels.TaskUrgencyLevel urgency)
        => urgency switch
        {
            ViewModels.TaskUrgencyLevel.Low => TaskPriority.Low,
            ViewModels.TaskUrgencyLevel.High => TaskPriority.High,
            ViewModels.TaskUrgencyLevel.Urgent => TaskPriority.Urgent,
            _ => TaskPriority.Normal,
        };
}
