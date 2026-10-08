using Dapper;
using IksAdminApi;
using MySqlConnector;

namespace IksAdmin;

public static class DB
{
    public static string ConnectionString { get; set; } = string.Empty;
    private static readonly CancellationTokenSource HealthCancellation = new();

    public static async Task Init()
    {
        try
        {
            await using var conn = new MySqlConnection(ConnectionString);
            await OpenConnectionWithRetryAsync(conn);
            await conn.QueryAsync(@"
                create table if not exists iks_servers(
                    id int not null unique,
                    ip varchar(32) not null comment 'ip:port',
                    name varchar(255) not null,
                    rcon varchar(128) default null,
                    created_at int not null,
                    updated_at int not null,
                    deleted_at int default null
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                create table if not exists iks_groups(
                    id int not null auto_increment primary key,
                    name varchar(64) not null unique,
                    flags varchar(32) not null,
                    immunity int not null,
                    comment varchar(255) default null
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;
                create table if not exists iks_admins(
                    id int not null auto_increment primary key,
                    steam_id varchar(17) not null,
                    name varchar(64) not null,
                    flags varchar(32) default null,
                    immunity int default null,
                    group_id int default null,
                    discord varchar(64) default null,
                    vk varchar(64) default null,
                    is_disabled int(1) not null default 0,
                    end_at int null,
                    created_at int not null,
                    updated_at int not null,
                    deleted_at int default null,
                    foreign key (group_id) references iks_groups(id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;

                insert into iks_admins(steam_id, name, flags, immunity, created_at, updated_at)
                select 'CONSOLE', 'CONSOLE', null, 0, unix_timestamp(), unix_timestamp();
                DELETE from iks_admins where steam_id='CONSOLE' and id!=1;

                create table if not exists iks_admin_to_server(
                    id int not null auto_increment primary key,
                    admin_id int not null,
                    server_id int,
                    foreign key (admin_id) references iks_admins(id),
                    foreign key (server_id) references iks_servers(id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;

                create table if not exists iks_comms(
                    id int not null auto_increment primary key,
                    steam_id bigint not null,
                    ip varchar(32),
                    name varchar(128),
                    mute_type int not null comment '0 - voice(mute), 1 - chat(gag), 2 - both(silence)', 
                    duration int not null,
                    reason varchar(128) not null,
                    server_id int default null,
                    admin_id int not null,
                    unbanned_by int default null,
                    unban_reason varchar(128) default null,
                    created_at int not null,
                    end_at int not null,
                    updated_at int not null,
                    deleted_at int default null,
                    foreign key (admin_id) references iks_admins(id),
                    foreign key (unbanned_by) references iks_admins(id),
                    foreign key (server_id) references iks_servers(id),
                    index `idx_steam_id` (`steam_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;

                create table if not exists iks_bans(
                    id int not null auto_increment primary key,
                    steam_id bigint,
                    ip varchar(32),
                    name varchar(128),
                    duration int not null,
                    reason varchar(128) not null,
                    ban_type tinyint not null default 0 comment '0 - SteamId, 1 - Ip, 2 - Both',
                    server_id int default null,
                    admin_id int not null,
                    unbanned_by int default null,
                    unban_reason varchar(128) default null,
                    created_at int not null,
                    end_at int not null,
                    updated_at int not null,
                    deleted_at int default null,
                    foreign key (admin_id) references iks_admins(id),
                    foreign key (unbanned_by) references iks_admins(id),
                    foreign key (server_id) references iks_servers(id),
                    index `idx_steam_id` (`steam_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;
                create table if not exists iks_admins_warns(
                    id int not null auto_increment primary key,
                    admin_id int not null,
                    target_id int default null,
                    target_steam_id bigint unsigned default null,
                    issued_immunity int default null,
                    target_name varchar(128) default null,
                    source varchar(32) default null,
                    rule_id varchar(64) default null,
                    message varchar(512) default null,
                    issued_steam_id bigint unsigned default null,
                    revoked_by_steam_id bigint unsigned default null,
                    duration int not null,
                    reason varchar(255) not null,
                    created_at int not null,
                    end_at int not null,
                    updated_at int not null,
                    deleted_at int default null,
                    deleted_by int default null,
                    foreign key (admin_id) references iks_admins(id),
                    foreign key (target_id) references iks_admins(id),
                    foreign key (deleted_by) references iks_admins(id),
                    index idx_iks_warns_target_steam (target_steam_id, deleted_at, created_at)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;
                create table if not exists iks_groups_limitations( 
                    id int not null auto_increment primary key,
                    group_id int not null,
                    limitation_key varchar(64) not null,
                    limitation_value varchar(32) not null,
                    foreign key (group_id) references iks_groups(id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;

                create table if not exists iks_cookies(
                    id int not null auto_increment primary key,
                    steam_id bigint not null,
                    cookie_key varchar(255) not null, 
                    cookie_value text not null,
                    server_id int default null,
                    index idx_iks_cookies_steam_id (steam_id),
                    foreign key (server_id) references iks_servers(id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;
"
                );

            // Нормализуем размеры полей старых установок под актуальную схему.
            // Имя сервера из game конфигурации может быть длиннее 64 символов.
            await conn.ExecuteAsync(@"
                ALTER TABLE iks_servers
                    MODIFY name VARCHAR(255) NOT NULL,
                    MODIFY ip VARCHAR(32) NOT NULL,
                    MODIFY rcon VARCHAR(128) NULL;");

            var warnColumns = (await conn.QueryAsync<string>(@"
                select column_name from information_schema.columns
                where table_schema = database() and table_name = 'iks_admins_warns'"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!warnColumns.Contains("target_steam_id"))
                await conn.ExecuteAsync("alter table iks_admins_warns add column target_steam_id bigint unsigned null");
            if (!warnColumns.Contains("issued_immunity"))
                await conn.ExecuteAsync("alter table iks_admins_warns add column issued_immunity int null");

        if (await conn.QuerySingleAsync<int>(@"select count(*) from iks_admins") == 0)
        {
            await conn.QueryAsync(@"
            insert into iks_admins(id, steam_id, name, flags, immunity, created_at, updated_at)
            select 1, 'CONSOLE', 'CONSOLE', null, 0, unix_timestamp(), unix_timestamp();
            ");
        }
        
        }
        catch (MySqlException e)
        {
            AdminUtils.LogError(e.ToString());
            throw;
        }
    }

    public static Task StartHealthCheckAsync()
    {
        _ = Task.Run(HealthCheckLoopAsync);
        return Task.CompletedTask;
    }

    public static async Task<bool> IsConnectionHealthyAsync()
    {
        try
        {
            await using var conn = new MySqlConnection(ConnectionString);
            await OpenConnectionWithRetryAsync(conn, 1);
            await using var command = new MySqlCommand("SELECT 1", conn);
            await command.ExecuteScalarAsync();
            return true;
        }
        catch (Exception ex)
        {
            AdminUtils.LogError($"Database health check failed: {ex.Message}");
            return false;
        }
    }

    public static async Task OpenConnectionWithRetryAsync(MySqlConnection connection, int maxAttempts = 5)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (attempt < maxAttempts)
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(5000, 250 * attempt)), HealthCancellation.Token);
            }
        }

        throw lastError ?? new InvalidOperationException("Could not connect to MySQL.");
    }

    private static async Task HealthCheckLoopAsync()
    {
        while (!HealthCancellation.IsCancellationRequested)
        {
            try
            {
                if (!await IsConnectionHealthyAsync())
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), HealthCancellation.Token);
                    continue;
                }

                await Task.Delay(TimeSpan.FromSeconds(15), HealthCancellation.Token);
            }
            catch (OperationCanceledException) when (HealthCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                AdminUtils.LogError($"MySQL unavailable. Retrying in 5 seconds: {ex.Message}");
                try { await Task.Delay(TimeSpan.FromSeconds(5), HealthCancellation.Token); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
