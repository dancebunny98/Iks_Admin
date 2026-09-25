using Dapper;
using IksAdminApi;
using MySqlConnector;

namespace IksAdmin;

public static class DBAdmins
{
    private static string AdminSelect = @"
        select
        id as id,
        steam_id as steamId,
        name as name,
        flags as flags,
        immunity as immunity,
        group_id as groupId,
        discord as discord,
        vk as vk,
        is_disabled as isDisabled,
        end_at as endAt,
        created_at as createdAt,
        updated_at as updatedAt,
        deleted_at as deletedAt
        from iks_admins
    ";

    /// <summary>
    /// Возвращает Id текущего сервера или null, если он ещё не зарегистрирован
    /// в БД. ThisServer выставляется в AdminApi.ReloadDataFromDb асинхронно
    /// и может быть null, если DBServers.Add упал (например, Data too long
    /// for column 'name'). Без этого guard'а каждый метод ниже падал с NRE
    /// на Main.AdminApi.ThisServer.Id.
    /// </summary>
    private static int? ResolveServerId(int? serverId)
    {
        return serverId ?? Main.AdminApi?.ThisServer?.Id;
    }

    public static async Task SetAdminsToServer()
    {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();

            var adminsToServer = (await conn.QueryAsync<AdminToServer>(@"
            select
            admin_id as adminId,
            server_id as serverId
            from iks_admin_to_server
            ")).ToList();
            Main.AdminApi.AdminsToServer = adminsToServer;
        }
        catch (Exception e)
        {
            AdminUtils.LogError(e.ToString());
            // не throw: иначе RefreshAdmins упадёт и ReloadDataFromDb тоже
        }
    }

    public static async Task<Admin?> AddAdmin(Admin admin)
    {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();
            var existingAdmin = await GetAdmin(admin.SteamId, ignoreDeleted: false);
            if (existingAdmin != null)
            {
                AdminUtils.LogDebug($"Admin {admin.SteamId} already exists...");
                AdminUtils.LogDebug($"Set new admin {admin.SteamId} id = {existingAdmin.Id} ✔");
                admin.Id = existingAdmin.Id;
                AdminUtils.LogDebug($"Update admin in base...");
                return await UpdateAdminInBase(admin);
            }
            AdminUtils.LogDebug($"Add admin to base...");
            return await AddAdminToBase(admin);
        }
        catch (Exception e)
        {
            AdminUtils.LogError(e.ToString());
            return null;
        }
    }

    public static async Task AddServerIdToAdmin(int adminId, int? serverId)
    {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();
            var existingAdmin = await GetAdminById(adminId);
            if (existingAdmin == null)
            {
                AdminUtils.LogError($"Admin {adminId} not finded ✖");
                return;
            }
            AdminUtils.LogDebug($"Admin {existingAdmin.Name} finded ✔");
            AdminUtils.LogDebug($"Adding server id...");
            if (Main.AdminApi.AdminsToServer.Any(x => x.AdminId == adminId && x.ServerId == serverId))
            {
                AdminUtils.LogError($"Server ID already added");
                return;
            }
            await conn.QueryAsync(@"
            insert into iks_admin_to_server(admin_id, server_id)
            values
            (@adminId, @serverId)
            ", new {adminId, serverId});
        }
        catch (Exception e)
        {
            AdminUtils.LogError(e.ToString());
            // не throw — вызывается из CreateAdmin
        }
    }

    public static async Task RemoveServerIdFromAdmin(int adminId, int serverId)
    {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();
            var existingAdmin = await GetAdminById(adminId);
            if (existingAdmin == null)
            {
                AdminUtils.LogError($"Admin {adminId} not finded ✖");
                return;
            }
            AdminUtils.LogDebug($"Admin {existingAdmin.Name} finded ✔");
            AdminUtils.LogDebug($"Removing server id...");
            await conn.QueryAsync(@"
            delete from iks_admin_to_server where admin_id = @adminId and server_id = @serverId
            ", new {adminId, serverId});
        }
        catch (Exception e)
        {
            AdminUtils.LogError(e.ToString());
        }
    }

    public static async Task RemoveServerIdsFromAdmin(int adminId)
    {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();
            var existingAdmin = await GetAdminById(adminId);
            if (existingAdmin == null)
            {
                AdminUtils.LogError($"Admin {adminId} not finded ✖");
                return;
            }
            AdminUtils.LogDebug($"Admin {existingAdmin.Name} finded ✔");
            AdminUtils.LogDebug($"Removing server id...");
            await conn.QueryAsync(@"
            delete from iks_admin_to_server where admin_id = @adminId
            ", new {adminId});
            foreach (var adm in AdminUtils.CoreApi.AdminsToServer.ToList())
            {
                if (adm.AdminId == adminId)
                    AdminUtils.CoreApi.AdminsToServer.Remove(adm);
            }
        }
        catch (Exception e)
        {
            AdminUtils.LogError(e.ToString());
        }
    }

    public static async Task<Admin?> GetAdmin(string steamId, int? serverId = null, bool ignoreDeleted = true)
    {
        try
        {
            var sid = ResolveServerId(serverId);
            if (sid == null)
            {
                AdminUtils.LogDebug("DBAdmins.GetAdmin: ThisServer is null, skip.");
                return null;
            }

            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();
            var ignoreDeletedString = ignoreDeleted ? "and deleted_at is null" : "";
            var admins = (await conn.QueryAsync<Admin>($@"
                {AdminSelect}
                where steam_id = @steamId
                {ignoreDeletedString}
            ", new { steamId })).ToList();
            return admins.FirstOrDefault(x => x.Servers.Contains((int)sid));
        }
        catch (Exception e)
        {
            AdminUtils.LogError(e.ToString());
            return null;
        }
    }

    public static async Task<Admin?> GetAdminById(int id, int? serverId = null, bool ignoreDeleted = true)
    {
        try
        {
            // serverId здесь фактически не используется в SQL, но нужен для
            // ResolveServerId-guard'а: если ThisServer ещё не зарегистрирован,
            // не стоит дёргать БД из мест, где admin_id ещё не валиден.
            // Однако для оффлайн-операций (например, AddServerIdToAdmin) admin
            // может существовать и без ThisServer. Поэтому если serverId
            // явно не задан и ThisServer == null — всё равно пытаемся прочитать
            // строку, но логируем предупреждение.
            if (serverId == null && Main.AdminApi?.ThisServer == null)
            {
                AdminUtils.LogDebug("DBAdmins.GetAdminById: ThisServer is null, continuing without server filter.");
            }

            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();
            var ignoreDeletedString = ignoreDeleted ? "and deleted_at is null" : "";
            var admin = await conn.QueryFirstOrDefaultAsync<Admin>($@"
                {AdminSelect}
                where id = @id
                {ignoreDeletedString}
            ", new { id });

            return admin;
        }
        catch (Exception e)
        {
            AdminUtils.LogError(e.ToString());
            return null;
        }
    }

    public static async Task<List<Admin>> GetAllAdmins(int? serverId = null, bool ignoreDeleted = true)
    {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();
            var ignoreDeletedString = ignoreDeleted ? "where deleted_at is null" : "";
            var admins = (await conn.QueryAsync<Admin>($@"
                {AdminSelect}
                {ignoreDeletedString}
            ")).ToList();

            return admins;
        }
        catch (Exception e)
        {
            AdminUtils.LogError(e.ToString());
            return new List<Admin>();
        }
    }

    public static async Task<List<Admin>> GetAllAdminsBySteamId(string steamId, bool ignoreDeleted = true)
    {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();
            var ignoreDeletedString = ignoreDeleted ? "and deleted_at is null" : "";
            var admins = (await conn.QueryAsync<Admin>($@"
                {AdminSelect}
                where steam_id = @steamId 
                {ignoreDeletedString}
            ", new { steamId })).ToList();

            return admins;
        }
        catch (Exception e)
        {
            AdminUtils.LogError(e.ToString());
            return new List<Admin>();
        }
    }

    public static async Task<Admin?> AddAdminToBase(Admin admin)
    {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();

            // QuerySingleAsync<int> бросает InvalidOperationException, если
            // last_insert_id() не вернул ни одной строки (бывает при определённых
            // настройках соединения). QueryFirstOrDefaultAsync<int?> возвращает
            // null вместо исключения.
            var id = await conn.QueryFirstOrDefaultAsync<int?>(@"
                insert into iks_admins
                (steam_id, name, flags, immunity, group_id, discord, vk, end_at, created_at, updated_at)
                values
                (@steamId, @name, @flags, @immunity, @groupId, @discord, @vk, @endAt, unix_timestamp(), unix_timestamp());
                select last_insert_id();
            ", new {
                steamId = admin.SteamId,
                name = admin.Name,
                flags = admin.Flags,
                immunity = admin.Immunity,
                groupId = admin.GroupId,
                discord = admin.Discord,
                vk = admin.Vk,
                endAt = admin.EndAt
            });

            if (id == null)
            {
                AdminUtils.LogError("AddAdminToBase: last_insert_id() returned no rows.");
                return null;
            }

            AdminUtils.LogDebug($"Admin added to base ✔");
            admin.Id = id.Value;
            return admin;
        }
        catch (Exception e)
        {
            AdminUtils.LogError(e.ToString());
            return null;
        }
    }

    public static async Task<Admin?> UpdateAdminInBase(Admin admin)
    {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();
            await conn.QueryAsync(@"
                update iks_admins set 
                steam_id = @steamId,
                name = @name,
                flags = @flags,
                immunity = @immunity,
                group_id = @groupId,
                discord = @discord,
                vk = @vk,
                is_disabled = @disabled,
                end_at = @endAt,
                updated_at = unix_timestamp(),
                deleted_at = @deletedAt
                where id = @id 
            ", new {
                id = admin.Id,
                steamId = admin.SteamId,
                name = admin.Name,
                flags = admin.Flags,
                immunity = admin.Immunity,
                groupId = admin.GroupId,
                disabled = admin.Disabled,
                discord = admin.Discord,
                vk = admin.Vk,
                endAt = admin.EndAt,
                deletedAt = admin.DeletedAt
            });
            AdminUtils.LogDebug($"Admin updated in base ✔");

            var updatedAdmin = await GetAdmin(admin.SteamId);
            if (updatedAdmin == null)
            {
                // GetAdmin мог вернуть null из-за ThisServer == null или из-за
                // того, что админ не привязан к текущему серверу. Возвращаем
                // исходный admin — он уже содержит актуальные поля в памяти.
                AdminUtils.LogDebug("UpdateAdminInBase: GetAdmin returned null after update, returning original admin.");
                return admin;
            }
            return updatedAdmin;
        }
        catch (Exception e)
        {
            AdminUtils.LogError(e.ToString());
            return null;
        }
    }

    public static async Task DeleteAdmin(int id)
    {
        try
        {
            await using var conn = new MySqlConnection(DB.ConnectionString);
            await conn.OpenAsync();
            await conn.QueryAsync(@"
                update iks_admins set 
                deleted_at = unix_timestamp()
                where id = @id
            ", new {
                id
            });
            AdminUtils.LogDebug($"Admin deleted ✔");
        }
        catch (Exception e)
        {
            AdminUtils.LogError(e.ToString());
        }
    }

    public static async Task RefreshAdmins()
    {
        try
        {
            await DBGroups.RefreshGroups();
            AdminUtils.LogDebug("Refreshing admins to server...");
            await SetAdminsToServer();
            AdminUtils.LogDebug("1/5 Admin to server setted ✔");
            AdminUtils.LogDebug("Refreshing admins...");
            var admins = await GetAllAdmins();
            AdminUtils.LogDebug("2/5 Admins getted ✔");

            // First(...) бросает InvalidOperationException, если в iks_admins нет
            // строки с steam_id = 'console'. На чистой БД её точно нет, поэтому
            // сервер падал прямо здесь. FirstOrDefault безопасен.
            var consoleAdmin = admins.FirstOrDefault(x => x.SteamId.ToLower() == "console");
            if (consoleAdmin == null)
            {
                AdminUtils.LogError(
                    "RefreshAdmins: no 'console' admin found in iks_admins. " +
                    "Add a row with steam_id='console' to enable console actions.");
            }
            Main.AdminApi.ConsoleAdmin = consoleAdmin!;
            AdminUtils.LogDebug("3/5 Console admin setted ✔");
            admins = admins.Where(x => x.SteamId.ToLower() != "console").ToList();
            Main.AdminApi.AllAdmins = await GetAllAdmins(ignoreDeleted: false);
            var serverId = Main.AdminApi.Config.ServerId;
            bool ignoreAdminServers = Main.AdminApi.Config.IgnoreAdminServers;
            Main.AdminApi.ServerAdmins.Clear();
            foreach (var admin in Main.AdminApi.AllAdmins)
            {
                if ((admin.Servers.Contains(serverId) || admin.OnAllServers || ignoreAdminServers) && admin.DeletedAt == null)
                {
                    if (ulong.TryParse(admin.SteamId, out var uSteamId))
                        Main.AdminApi.ServerAdmins[uSteamId] = admin;
                }
            }
            AdminUtils.LogDebug("4/5 All admins setted ✔");
            AdminUtils.LogDebug("5/5 Server admins setted ✔");
            AdminUtils.LogDebug("Admins refreshed ✔");
            AdminUtils.LogDebug("---------------");
            AdminUtils.LogDebug("Server admins:");
            AdminUtils.LogDebug($"id | name | steamId | flags | immunity | group | serverIds | discord | vk | isDisabled");
            foreach (var admin in Main.AdminApi.ServerAdmins.Values)
            {
                AdminUtils.LogDebug($"{admin.Id} | {admin.Name} | {admin.SteamId} | {admin.CurrentFlags} | {admin.CurrentImmunity} | {admin.Group?.Name ?? "NONE"} | {string.Join(";", admin.Servers)} | {admin.Discord ?? "NONE"} | {admin.Vk ?? "NONE"} | {admin.IsDisabled}");
            }
        }
        catch (Exception e)
        {
            AdminUtils.LogError(e.ToString());
            // Не throw: RefreshAdmins вызывается из ReloadDataFromDb, а тот
            // из конструктора AdminApi / команд css_am_reload. Падение здесь
            // оставляет ThisServer выставленным, но ломает последующую логику.
        }
    }
}
