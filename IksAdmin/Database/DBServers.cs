using Dapper;
using IksAdminApi;
using MySqlConnector;

namespace IksAdmin;

public static class DBServers
{
    // Реальные длины колонок в БД (с запасом). Держи синхронно с SQL-миграцией:
    //   ALTER TABLE iks_servers MODIFY name VARCHAR(255) NOT NULL;
    //   ALTER TABLE iks_servers MODIFY ip   VARCHAR(64)  NOT NULL;
    //   ALTER TABLE iks_servers MODIFY rcon VARCHAR(128) NULL;
    private const int MaxNameLen = 255;
    private const int MaxIpLen   = 64;
    private const int MaxRconLen = 128;

    private static string Trunc(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length > max ? s.Substring(0, max) : s;
    }

    public static async Task Add(ServerModel server)
    {
        // Обрезаем поля ДО подключения к БД — иначе MySqlException "Data too long"
        // на name/ip/rcon валит весь ReloadDataFromDb, ThisServer остаётся null,
        // и дальше весь плагин сыпет NullReferenceException в DBBans/DBAdmins.
        var name = Trunc(server.Name, MaxNameLen);
        var ip   = Trunc(server.Ip,   MaxIpLen);
        var rcon = Trunc(server.Rcon, MaxRconLen);

        try
        {
            AdminUtils.LogDebug($"Add server to base... id={server.Id} ip={ip} name_len={name.Length}");
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();

            var existingServer = await Get(server.Id);
            if (existingServer != null)
            {
                AdminUtils.LogDebug("Server exists with id " + existingServer.Id);
                server.Id = existingServer.Id;
                await Update(server, name, ip, rcon);
                return;
            }

            await conn.QueryAsync(@"
                insert into iks_servers(id, ip, name, rcon, created_at, updated_at)
                values(@serverId, @ip, @name, @rcon, unix_timestamp(), unix_timestamp())
            ", new {
                serverId = server.Id,
                ip,
                name,
                rcon
            });
            AdminUtils.LogDebug("Server added to db ✔");
        }
        catch (MySqlException e)
        {
            AdminUtils.LogError(e.ToString());
            throw;
        }
    }

    private static async Task Update(ServerModel server, string name, string ip, string rcon)
    {
        try
        {
            AdminUtils.LogDebug("Server update...");
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();
            await conn.QueryAsync(@"
                update iks_servers set
                ip = @ip,
                name = @name,
                rcon = @rcon,
                updated_at = unix_timestamp(),
                deleted_at = null
                where id = @id
            ",
            new {
                ip,
                name,
                rcon,
                id = server.Id
            }
            );
            AdminUtils.LogDebug("Server updated ✔");
        }
        catch (MySqlException e)
        {
            AdminUtils.LogError(e.ToString());
            throw;
        }
    }

    public static async Task<ServerModel?> Get(int id)
    {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();

            var server = await conn.QueryFirstOrDefaultAsync<ServerModel>(@"
                select
                id as id,
                ip as ip,
                name as name,
                created_at as createdAt,
                updated_at as updatedAt,
                deleted_at as deletedAt,
                rcon as rcon
                from iks_servers
                where id = @id
                and deleted_at is null
            ", new {
                id
            });
            return server;
        }
        catch (MySqlException e)
        {
            AdminUtils.LogError(e.ToString());
            throw;
        }
    }

    public static async Task<List<ServerModel>> GetAll()
    {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();

            var servers = (await conn.QueryAsync<ServerModel>(@"
                select
                id as id,
                ip as ip,
                name as name,
                created_at as createdAt,
                updated_at as updatedAt,
                deleted_at as deletedAt,
                rcon as rcon
                from iks_servers
                where deleted_at is null
            ")).ToList();

            return servers;
        }
        catch (MySqlException e)
        {
            AdminUtils.LogError(e.ToString());
            throw;
        }
    }
}
