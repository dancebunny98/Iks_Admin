using Dapper;
using MySqlConnector;

namespace IksAdmin_ChatModeration;

public sealed class PlayerWarning
{
    public long Id { get; set; }
    public int AdminId { get; set; }
    public ulong SteamId { get; set; }
    public string PlayerName { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Source { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string Message { get; set; } = "";
    public ulong? IssuedBy { get; set; }
    public long CreatedAt { get; set; }
    public long? RevokedAt { get; set; }
    public ulong? RevokedBy { get; set; }
}

public sealed class WarningStore(string connectionString)
{
    private const string Table = "iks_admins_warns";

    public async Task InitializeAsync()
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        // The core creates the table. Extend it in place so administrator warning IDs
        // and foreign keys survive the upgrade.
        var columns = (await connection.QueryAsync<ColumnInfo>("""
            SELECT COLUMN_NAME AS Name, IS_NULLABLE AS IsNullable,
                   CHARACTER_MAXIMUM_LENGTH AS MaxLength
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table;
            """, new { table = Table })).ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        if (columns.Count == 0)
            throw new InvalidOperationException($"{Table} is missing. Start IksAdmin before ChatModeration.");

        if (columns["target_id"].IsNullable == "NO")
            await connection.ExecuteAsync($"ALTER TABLE {Table} MODIFY COLUMN target_id int NULL;");
        if (columns["reason"].MaxLength < 255)
            await connection.ExecuteAsync($"ALTER TABLE {Table} MODIFY COLUMN reason varchar(255) NOT NULL;");

        var additions = new Dictionary<string, string>
        {
            ["target_steam_id"] = "bigint unsigned NULL",
            ["target_name"] = "varchar(128) NULL",
            ["source"] = "varchar(32) NULL",
            ["rule_id"] = "varchar(64) NULL",
            ["message"] = "varchar(512) NULL",
            ["issued_steam_id"] = "bigint unsigned NULL",
            ["revoked_by_steam_id"] = "bigint unsigned NULL"
        };
        foreach (var (name, definition) in additions)
        {
            if (!columns.ContainsKey(name))
                await connection.ExecuteAsync($"ALTER TABLE {Table} ADD COLUMN {name} {definition};");
        }

        var hasIndex = await connection.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table
              AND INDEX_NAME = 'idx_iks_warns_target_steam';
            """, new { table = Table });
        if (hasIndex == 0)
            await connection.ExecuteAsync($"CREATE INDEX idx_iks_warns_target_steam ON {Table} (target_steam_id, deleted_at, created_at);");
    }

    public async Task<long> AddAsync(PlayerWarning warning)
    {
        if (warning.AdminId <= 0)
            throw new InvalidOperationException("A valid IksAdmin issuer is required for player warnings.");
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        return await connection.QuerySingleAsync<long>($"""
            INSERT INTO {Table}
                (admin_id, target_id, target_steam_id, target_name, reason, source,
                 rule_id, message, issued_steam_id, duration, created_at, end_at, updated_at)
            VALUES
                (@AdminId, NULL, @SteamId, @PlayerName, @Reason, @Source,
                 @RuleId, @Message, @IssuedBy, 0, @CreatedAt, 0, @CreatedAt);
            SELECT LAST_INSERT_ID();
            """, warning);
    }

    public async Task<List<PlayerWarning>> ListAsync(ulong steamId, int limit = 50)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        var rows = await connection.QueryAsync<PlayerWarning>($"""
            {SelectSql}
            WHERE target_id IS NULL AND target_steam_id = @steamId
            ORDER BY created_at DESC, id DESC LIMIT @limit;
            """, new { steamId, limit = Math.Clamp(limit, 1, 200) });
        return rows.ToList();
    }

    public async Task<List<PlayerWarning>> ActiveAsync(ulong steamId, int activeDays)
    {
        var since = activeDays <= 0 ? 0 : DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (long)activeDays * 86400;
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        var rows = await connection.QueryAsync<PlayerWarning>($"""
            {SelectSql}
            WHERE target_id IS NULL AND target_steam_id = @steamId
              AND deleted_at IS NULL AND created_at >= @since
            ORDER BY created_at DESC, id DESC;
            """, new { steamId, since });
        return rows.ToList();
    }

    public async Task<Dictionary<ulong, int>> ActiveCountsAsync(IEnumerable<ulong> steamIds, int activeDays)
    {
        var ids = steamIds.Distinct().ToArray();
        if (ids.Length == 0) return new();
        var since = activeDays <= 0 ? 0 : DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (long)activeDays * 86400;
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        var rows = await connection.QueryAsync<CountRow>($"""
            SELECT target_steam_id SteamId, COUNT(*) Count
            FROM {Table}
            WHERE target_id IS NULL AND target_steam_id IN @ids
              AND deleted_at IS NULL AND created_at >= @since
            GROUP BY target_steam_id;
            """, new { ids, since });
        return rows.ToDictionary(x => x.SteamId, x => x.Count);
    }

    public async Task<bool> RevokeAsync(long id, ulong steamId, int moderatorId, ulong moderatorSteamId)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        var changed = await connection.ExecuteAsync($"""
            UPDATE {Table}
            SET deleted_at = @now, deleted_by = @moderatorId,
                revoked_by_steam_id = @moderatorSteamId, updated_at = @now
            WHERE id = @id AND target_id IS NULL AND target_steam_id = @steamId
              AND deleted_at IS NULL;
            """, new { id, steamId, moderatorId, moderatorSteamId,
                now = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
        return changed == 1;
    }

    private const string SelectSql = """
        SELECT id Id, admin_id AdminId, target_steam_id SteamId, target_name PlayerName,
               reason Reason, source Source, rule_id RuleId, message Message,
               issued_steam_id IssuedBy, created_at CreatedAt, deleted_at RevokedAt,
               revoked_by_steam_id RevokedBy
        FROM iks_admins_warns
        """;

    private sealed class ColumnInfo
    {
        public string Name { get; set; } = "";
        public string IsNullable { get; set; } = "";
        public long? MaxLength { get; set; }
    }

    private sealed class CountRow
    {
        public ulong SteamId { get; set; }
        public int Count { get; set; }
    }
}
