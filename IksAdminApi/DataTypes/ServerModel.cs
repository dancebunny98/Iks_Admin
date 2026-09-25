using CounterStrikeSharp.API.Core;

namespace IksAdminApi;

public class ServerModel
{
    // Длины колонок в iks_servers. Держать синхронно с SQL-миграцией:
    //   ALTER TABLE iks_servers MODIFY name VARCHAR(255) NOT NULL;
    //   ALTER TABLE iks_servers MODIFY ip   VARCHAR(64)  NOT NULL;
    //   ALTER TABLE iks_servers MODIFY rcon VARCHAR(128) NULL;
    //
    // Обрезка в сеттерах — вторая линия защиты после Trunc в DBServers.cs.
    // Она спасает, если ServerModel создаётся в обход DBServers (например,
    // из другого модуля или из будущего кода), и name из конфига окажется
    // длиннее колонки.
    public const int MaxNameLen = 255;
    public const int MaxIpLen   = 64;
    public const int MaxRconLen = 128;

    public int Id { get; set; }

    private string _ip = "";
    public string Ip
    {
        get => _ip;
        set => _ip = Trunc(value, MaxIpLen);
    }

    private string _name = "";
    public string Name
    {
        get => _name;
        set => _name = Trunc(value, MaxNameLen);
    }

    private string? _rcon;
    public string? Rcon
    {
        get => _rcon;
        set => _rcon = value == null ? null : Trunc(value, MaxRconLen);
    }

    public int CreatedAt { get; set; } = AdminUtils.CurrentTimestamp();
    public int UpdatedAt { get; set; } = AdminUtils.CurrentTimestamp();
    public int? DeletedAt { get; set; }

    private static string Trunc(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length > max ? s.Substring(0, max) : s;
    }

    public ServerModel(int id, string ip, string name, int createdAt, int updatedAt, int? deletedAt, string? rcon = null)
    {
        Id = id;
        Ip = ip;
        Name = name;
        Rcon = rcon;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DeletedAt = deletedAt;
    }

    public ServerModel(int id, string ip, string name, string? rcon = null)
    {
        Id = id;
        Ip = ip;
        Name = name;
        Rcon = rcon;
    }
}
