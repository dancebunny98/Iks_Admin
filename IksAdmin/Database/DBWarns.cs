using Dapper;
using IksAdminApi;
using MySqlConnector;

namespace IksAdmin;

public static class DBWarns
{
    private static string WarnSelect = @"
    select 
    w.id as id,
    w.admin_id as adminId,
    w.issued_immunity as IssuedImmunity,
    coalesce(w.target_id, 0) as targetId,
    w.target_steam_id as TargetSteamId,
    w.duration as duration,
    w.reason as reason,
    w.created_at as createdAt,
    w.updated_at as updatedAt,
    w.end_at as endAt,
    w.deleted_at as deletedAt,
    w.deleted_by as deletedBy,
    (w.target_id is null) as IsPlayerWarning
    from iks_admins_warns w
    ";

    public static async Task<List<Warn>> GetAllActive() {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await DB.OpenConnectionWithRetryAsync(conn);

            var warns = (await conn.QueryAsync<Warn>($@"
            {WarnSelect}
            where 
            (w.target_id is not null or exists
                (select 1 from iks_admins a where a.steam_id = cast(w.target_steam_id as char)))
            and w.deleted_at is null
            ")).ToList();
            return warns;
        }
        catch (MySqlException e)
        {
            AdminUtils.LogError(e.ToString());
            throw;
        }
    }
    public static async Task<List<Warn>> GetAll() {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await DB.OpenConnectionWithRetryAsync(conn);

            var warns = (await conn.QueryAsync<Warn>($@"
            {WarnSelect}
            where (w.target_id is not null or exists
                (select 1 from iks_admins a where a.steam_id = cast(w.target_steam_id as char)))
            ")).ToList();
            return warns;
        }
        catch (MySqlException e)
        {
            AdminUtils.LogError(e.ToString());
            throw;
        }
    }
    public static async Task<DBResult> InsertToBase(this Warn warn) {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await DB.OpenConnectionWithRetryAsync(conn);

            int id = await conn.QuerySingleAsync<int>(@"
            insert into iks_admins_warns
            (admin_id, target_id, issued_immunity, duration, reason, created_at, end_at, updated_at)
            values
            (@adminId, @targetId, @issuedImmunity, @duration, @reason, @createdAt, @endAt, @updatedAt);
            select last_insert_id();
            ", new {
                adminId = warn.AdminId,
                issuedImmunity = warn.IssuedImmunity,
                targetId = warn.TargetId,
                duration = warn.Duration,
                reason = warn.Reason,
                createdAt = warn.CreatedAt,
                endAt = warn.EndAt,
                updatedAt = warn.UpdatedAt,
            });

            warn.Id = id;

            return new DBResult(id, 0);
        }
        catch (MySqlException e)
        {
            AdminUtils.LogError(e.ToString());
            return new DBResult(null, -1, e.Message);
        }
    }
    public static async Task<DBResult> UpdateInBase(this Warn warn) {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await DB.OpenConnectionWithRetryAsync(conn);
            warn.UpdatedAt = AdminUtils.CurrentTimestamp();
            if (warn.IsPlayerWarning)
            {
                var changed = await conn.ExecuteAsync(@"
                    update iks_admins_warns set deleted_at=@deletedAt, deleted_by=@deletedBy,
                        updated_at=@updatedAt where id=@id and target_id is null and deleted_at is null",
                    new { id = warn.Id, deletedAt = warn.DeletedAt, deletedBy = warn.DeletedBy,
                        updatedAt = warn.UpdatedAt });
                return new DBResult(warn.Id, changed == 1 ? 0 : 1);
            }
            await conn.QueryAsync(@"
            update iks_admins_warns set
            admin_id = @adminId,
            target_id = @targetId,
            duration = @duration,
            reason = @reason,
            end_at = @endAt,
            updated_at = @updatedAt,
            deleted_at = @deletedAt,
            deleted_by = @deletedBy
            where id = @id
            ", new {
                id = warn.Id,
                adminId = warn.AdminId,
                targetId = warn.TargetId,
                duration = warn.Duration,
                reason = warn.Reason,
                createdAt = warn.CreatedAt,
                endAt = warn.EndAt,
                updatedAt = AdminUtils.CurrentTimestamp(),
                deletedBy = warn.DeletedBy,
                deletedAt = warn.DeletedAt
            });
            return new DBResult(warn.Id, 0);
        }
        catch (MySqlException e)
        {
            AdminUtils.LogError(e.ToString());
            throw;
        }
    }
    public static async Task<List<Warn>> GetAllActiveForAdmin(int id) {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await DB.OpenConnectionWithRetryAsync(conn);

            var warns = (await conn.QueryAsync<Warn>($@"
            {WarnSelect}
            where 
            (w.target_id=@id or w.target_steam_id =
                (select cast(steam_id as unsigned) from iks_admins where id=@id))
            and w.deleted_at is null
            ", new {id})).ToList();
            return warns;
        }
        catch (MySqlException e)
        {
            AdminUtils.LogError(e.ToString());
            throw;
        }
    }
    public static async Task<List<Warn>> GetAllActiveByAdmin(int id) {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await DB.OpenConnectionWithRetryAsync(conn);

            var warns = (await conn.QueryAsync<Warn>($@"
            {WarnSelect}
            where 
            w.admin_id=@id and
            (w.target_id is not null or exists
                (select 1 from iks_admins a where a.steam_id = cast(w.target_steam_id as char))) and
            w.deleted_at is null
            ", new {id})).ToList();
            return warns;
        }
        catch (MySqlException e)
        {
            AdminUtils.LogError(e.ToString());
            throw;
        }
    }
}
