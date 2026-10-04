using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using IksAdminApi;
using Microsoft.Extensions.Logging;

namespace IksAdmin_DiscordPunishments;

public sealed class DiscordLogsConfig : BasePluginConfig
{
    public override int Version { get; set; } = 1;
    public WebhookConfig Webhooks { get; set; } = new();
    public EmbedConfig EmbedSettings { get; set; } = new();
    public Dictionary<string, TemplateConfig> Templates { get; set; } = TemplateConfig.Defaults();
    public Dictionary<string, FieldConfig> Fields { get; set; } = FieldConfig.Defaults();
    public ReportConfig Reports { get; set; } = new();
    public ExportConfig Export { get; set; } = new();
    public PermissionConfig Permissions { get; set; } = new();
    public int MaxStoredRecords { get; set; } = 10000;
}

public sealed class WebhookConfig
{
    public string Punishments { get; set; } = "";
    // Compatibility with the older config format that used one shared webhook.
    public string Default { get; set; } = "";
    public string Reports { get; set; } = "";
    public string Anomalies { get; set; } = "";
    public string Errors { get; set; } = "";
}

public sealed class EmbedConfig
{
    public bool IncludeAvatar { get; set; } = false;
    public bool IncludeIp { get; set; } = false;
    public string TimeZone { get; set; } = "UTC";
    public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss zzz";
    public int MaxFieldLength { get; set; } = 1024;
    public string FooterName { get; set; } = "IksAdmin Discord Punishments";
    public int BanColor { get; set; } = 15158332;
    public int CommColor { get; set; } = 15105570;
    public int KickColor { get; set; } = 16776960;
    public int RemoveColor { get; set; } = 5763719;
    public int ExpiredColor { get; set; } = 9807270;
}

public sealed class TemplateConfig
{
    public string Title { get; set; } = "{emoji} {type}";
    public string Description { get; set; } = "Игрок **{player}** получил наказание от **{admin}**.";
    public string Emoji { get; set; } = "📋";
    public int Color { get; set; } = 9807270;

    public static Dictionary<string, TemplateConfig> Defaults() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["ban"] = new() { Title = "🔨 Бан выдан", Description = "Игрок **{player}** забанен администратором **{admin}**.", Emoji = "🔨", Color = 15158332 },
        ["mute"] = new() { Title = "🔇 Мут выдан", Description = "Игрок **{player}** получил мут от **{admin}**.", Emoji = "🔇", Color = 15105570 },
        ["gag"] = new() { Title = "💬 Гаг выдан", Description = "Игрок **{player}** получил ограничение чата от **{admin}**.", Emoji = "💬", Color = 15105570 },
        ["silence"] = new() { Title = "🤐 Сайленс выдан", Description = "Игрок **{player}** получил полную блокировку общения от **{admin}**.", Emoji = "🤐", Color = 15105570 },
        ["kick"] = new() { Title = "👢 Игрок кикнут", Description = "Игрок **{player}** кикнут администратором **{admin}**.", Emoji = "👢", Color = 16776960 },
        ["unban"] = new() { Title = "✅ Бан снят", Description = "Бан игрока **{player}** снят администратором **{admin}**.", Emoji = "✅", Color = 5763719 },
        ["uncomm"] = new() { Title = "✅ Наказание чата снято", Description = "Наказание чата игрока **{player}** снято администратором **{admin}**.", Emoji = "✅", Color = 5763719 },
        ["expired"] = new() { Title = "⏰ Срок наказания истёк", Description = "Срок наказания игрока **{player}** истёк.", Emoji = "⏰", Color = 9807270 }
    };
}

public sealed class FieldConfig
{
    public bool Player { get; set; } = true;
    public bool Administrator { get; set; } = true;
    public bool Reason { get; set; } = true;
    public bool Duration { get; set; } = true;
    public bool IssuedAt { get; set; } = true;
    public bool ExpiresAt { get; set; } = true;
    public bool Type { get; set; } = true;
    public bool Ip { get; set; } = false;
    public bool Server { get; set; } = true;
    public bool Map { get; set; } = false;
    public bool Online { get; set; } = true;
    public bool PreviousPunishments { get; set; } = true;

    public static Dictionary<string, FieldConfig> Defaults() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["ban"] = new(), ["mute"] = new(), ["gag"] = new(), ["silence"] = new(),
        ["kick"] = new() { Duration = false, ExpiresAt = false },
        ["unban"] = new() { Duration = false, ExpiresAt = false },
        ["uncomm"] = new() { Duration = false, ExpiresAt = false },
        ["expired"] = new()
    };
}

public sealed class ReportConfig
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
    public bool IncludeTopModerators { get; set; } = true;
    public bool IncludeTopPlayers { get; set; } = true;
    public bool IncludeReasons { get; set; } = true;
    public int AnomalyThresholdPerHour { get; set; } = 10;
    public bool SendScheduledReports { get; set; } = false;
}

public sealed class ExportConfig
{
    public bool Enabled { get; set; } = true;
    public string Directory { get; set; } = "discord-punishment-reports";
}

public sealed class PermissionConfig
{
    public string Report { get; set; } = "z";
    public string Export { get; set; } = "z";
    public string Reload { get; set; } = "z";
}

public sealed class PunishmentRecord
{
    public string EventType { get; set; } = "";
    public string Player { get; set; } = "";
    public string SteamId { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Administrator { get; set; } = "";
    public string AdministratorSteamId { get; set; } = "";
    public string Reason { get; set; } = "";
    public int Duration { get; set; }
    public int CreatedAt { get; set; }
    public int EndAt { get; set; }
    public int? RemovedAt { get; set; }
    public string RemoveReason { get; set; } = "";
}

public sealed class Main : AdminModule, IPluginConfig<DiscordLogsConfig>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly object _sync = new();
    private readonly List<PunishmentRecord> _records = new();
    private readonly HashSet<string> _expiredSent = new(StringComparer.Ordinal);
    private string _dataPath = "";
    private string _failedPath = "";
    private SemaphoreSlim _sendLock = new(1, 1);

    public override string ModuleName => "IksAdmin_DiscordPunishments";
    public override string ModuleVersion => $"v{typeof(Main).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";
    public override string ModuleAuthor => "iks__ modules";
    public DiscordLogsConfig Config { get; set; } = new();
    public void OnConfigParsed(DiscordLogsConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Webhooks.Punishments))
            config.Webhooks.Punishments = config.Webhooks.Default;
        Config = config;
    }

    public override void InitializeCommands()
    {
        _dataPath = Path.Combine(AdminUtils.ConfigsDir, ModuleName, "records.json");
        _failedPath = Path.Combine(AdminUtils.ConfigsDir, ModuleName, "failed-webhooks.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(_dataPath)!);
        LoadRecords();
        Logger.LogInformation("[{Module}] loaded. Punishment webhook configured: {Configured}", ModuleName, !string.IsNullOrWhiteSpace(Config.Webhooks.Punishments));

        Api.RegisterPermission("discord_logs.report", Config.Permissions.Report);
        Api.RegisterPermission("discord_logs.export", Config.Permissions.Export);
        Api.RegisterPermission("discord_logs.reload", Config.Permissions.Reload);
        Api.RegisterPermission("discord_logs.test", Config.Permissions.Reload);
        Api.AddNewCommand("report", "Отчёт по наказаниям", "discord_logs.report", "css_report [day|week|month|all]", OnReport, CommandUsage.CLIENT_ONLY);
        Api.AddNewCommand("discord_logs_reload", "Перечитать конфиг Discord-логирования", "discord_logs.reload", "css_discord_logs_reload", OnReload, CommandUsage.CLIENT_AND_SERVER);
        Api.AddNewCommand("report_export", "Экспорт наказаний в CSV", "discord_logs.export", "css_report_export <day|week|month|all>", OnExport, CommandUsage.CLIENT_ONLY, minArgs: 1);
        Api.AddNewCommand("discord_logs_test", "Отправить тестовый Embed", "discord_logs.test", "css_discord_logs_test", OnTest, CommandUsage.CLIENT_AND_SERVER);

        Api.OnBanPost += OnBanPost;
        Api.OnCommPost += OnCommPost;
        Api.SuccessUnban += OnUnban;
        Api.SuccessUnComm += OnUncomm;
        Api.OnKickPost += OnKickPost;
        AddTimer(60.0f, OnPeriodicCheck, TimerFlags.REPEAT);
    }

    public override void Unload(bool hotReload)
    {
        Api.OnBanPost -= OnBanPost;
        Api.OnCommPost -= OnCommPost;
        Api.SuccessUnban -= OnUnban;
        Api.SuccessUnComm -= OnUncomm;
        Api.OnKickPost -= OnKickPost;
        _sendLock.Dispose();
        base.Unload(hotReload);
    }

    private HookResult OnBanPost(PlayerBan ban, ref bool announce)
    {
        var record = new PunishmentRecord
        {
            EventType = "ban", Player = ban.NameString, SteamId = ban.SteamId ?? "", Ip = ban.IpString,
            Administrator = ban.Admin?.CurrentName ?? "CONSOLE", AdministratorSteamId = ban.Admin?.SteamId ?? "CONSOLE",
            Reason = ban.Reason, Duration = ban.Duration, CreatedAt = ban.CreatedAt, EndAt = ban.EndAt
        };
        RecordAndSend(record);
        return HookResult.Continue;
    }

    private HookResult OnCommPost(PlayerComm comm, ref bool announce)
    {
        var type = comm.MuteType switch { 0 => "mute", 1 => "gag", _ => "silence" };
        var record = new PunishmentRecord
        {
            EventType = type, Player = comm.Name ?? "[NOT SET]", SteamId = comm.SteamId, Ip = comm.Ip ?? "",
            Administrator = comm.Admin?.CurrentName ?? "CONSOLE", AdministratorSteamId = comm.Admin?.SteamId ?? "CONSOLE",
            Reason = comm.Reason, Duration = comm.Duration, CreatedAt = comm.CreatedAt, EndAt = comm.EndAt
        };
        RecordAndSend(record);
        return HookResult.Continue;
    }

    private void OnUnban(Admin admin, PlayerBan ban)
    {
        var record = new PunishmentRecord
        {
            EventType = "unban", Player = ban.NameString, SteamId = ban.SteamId ?? "", Ip = ban.IpString,
            Administrator = admin.CurrentName, AdministratorSteamId = admin.SteamId, Reason = ban.UnbanReason ?? "",
            Duration = ban.Duration, CreatedAt = ban.CreatedAt, EndAt = ban.EndAt, RemovedAt = AdminUtils.CurrentTimestamp(), RemoveReason = ban.UnbanReason ?? ""
        };
        RecordAndSend(record);
    }

    private void OnUncomm(Admin admin, PlayerComm comm)
    {
        var record = new PunishmentRecord
        {
            EventType = "uncomm", Player = comm.Name ?? "[NOT SET]", SteamId = comm.SteamId,
            Administrator = admin.CurrentName, AdministratorSteamId = admin.SteamId, Reason = comm.UnbanReason ?? "",
            Duration = comm.Duration, CreatedAt = comm.CreatedAt, EndAt = comm.EndAt, RemovedAt = AdminUtils.CurrentTimestamp(), RemoveReason = comm.UnbanReason ?? ""
        };
        RecordAndSend(record);
    }

    private void OnKickPost(Admin admin, CCSPlayerController player, string reason)
    {
        var record = new PunishmentRecord
        {
            EventType = "kick", Player = player.PlayerName, Administrator = admin.CurrentName,
            AdministratorSteamId = admin.SteamId, Reason = reason,
            CreatedAt = AdminUtils.CurrentTimestamp()
        };
        RecordAndSend(record);
    }

    private void RecordAndSend(PunishmentRecord record)
    {
        lock (_sync)
        {
            _records.Add(record);
            if (_records.Count > Config.MaxStoredRecords)
                _records.RemoveRange(0, _records.Count - Config.MaxStoredRecords);
            SaveRecords();
        }
        _ = SendAsync(record);
    }

    private void OnPeriodicCheck()
    {
        CheckExpired();
        if (!Config.Reports.Enabled || !Config.Reports.SendScheduledReports || Config.Reports.IntervalMinutes <= 0)
            return;
        if (DateTime.UtcNow.Minute % Math.Max(1, Config.Reports.IntervalMinutes) == 0)
            _ = SendScheduledReportAsync();
    }

    private void CheckExpired()
    {
        List<PunishmentRecord> expired;
        lock (_sync)
        {
            expired = _records.Where(x => x.EndAt > 0 && x.EndAt <= AdminUtils.CurrentTimestamp() && x.RemovedAt == null)
                .Where(x => _expiredSent.Add($"{x.EventType}:{x.SteamId}:{x.CreatedAt}:{x.EndAt}")).ToList();
        }
        foreach (var record in expired)
        {
            _ = SendAsync(new PunishmentRecord
            {
                EventType = "expired", Player = record.Player, SteamId = record.SteamId, Ip = record.Ip,
                Administrator = record.Administrator, AdministratorSteamId = record.AdministratorSteamId,
                Reason = record.Reason, Duration = record.Duration, CreatedAt = record.CreatedAt, EndAt = record.EndAt
            });
        }
    }

    private async Task SendScheduledReportAsync()
    {
        var from = AdminUtils.CurrentTimestamp() - Config.Reports.IntervalMinutes * 60;
        List<PunishmentRecord> records;
        lock (_sync) records = _records.Where(x => x.CreatedAt >= from).ToList();
        if (records.Count == 0) return;
        await SendReportAsync($"последние {Config.Reports.IntervalMinutes} минут", records, "scheduler");
        if (records.Count < Config.Reports.AnomalyThresholdPerHour || string.IsNullOrWhiteSpace(Config.Webhooks.Anomalies)) return;
        var anomaly = new DiscordEmbed
        {
            Title = "⚠️ Аномальная активность", Description = "Количество наказаний превысило настроенный порог.",
            Color = 15158332, Timestamp = DateTimeOffset.UtcNow,
            Footer = new DiscordFooter { Text = Config.EmbedSettings.FooterName }
        };
        anomaly.Fields.Add(Field("Порог", Config.Reports.AnomalyThresholdPerHour.ToString(CultureInfo.InvariantCulture)));
        anomaly.Fields.Add(Field("Фактическое значение", records.Count.ToString(CultureInfo.InvariantCulture)));
        await SendWebhookWithRetry(Config.Webhooks.Anomalies, new { username = "IksAdmin Logs", embeds = new[] { anomaly } });
    }

    private async Task SendAsync(PunishmentRecord record)
    {
        var url = record.EventType is "unban" or "uncomm" ? Config.Webhooks.Punishments : Config.Webhooks.Punishments;
        if (string.IsNullOrWhiteSpace(url))
        {
            Logger.LogWarning("[{Module}] webhook is empty; event {EventType} was recorded locally but not sent", ModuleName, record.EventType);
            return;
        }
        var payload = new { username = "IksAdmin Logs", embeds = new[] { BuildEmbed(record) } };
        await SendWebhookWithRetry(url, payload);
    }

    private async Task SendWebhookWithRetry(string url, object payload)
    {
        await _sendLock.WaitAsync();
        try
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    using var response = await Http.PostAsJsonAsync(url, payload);
                    if (response.IsSuccessStatusCode)
                    {
                        Logger.LogInformation("[{Module}] Discord webhook delivered", ModuleName);
                        return;
                    }
                    var body = await response.Content.ReadAsStringAsync();
                    Logger.LogWarning("[{Module}] Discord webhook returned HTTP {Status}: {Body}", ModuleName, (int)response.StatusCode, Sanitize(body));
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(exception, "[{Module}] Discord webhook attempt {Attempt} failed", ModuleName, attempt + 1);
                }
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)));
            }
            await File.AppendAllTextAsync(_failedPath, JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        finally { _sendLock.Release(); }
    }

    private DiscordEmbed BuildEmbed(PunishmentRecord record)
    {
        var template = Config.Templates.TryGetValue(record.EventType, out var found) ? found : new TemplateConfig();
        var fields = Config.Fields.TryGetValue(record.EventType, out var fieldConfig) ? fieldConfig : new FieldConfig();
        var values = Values(record);
        var embed = new DiscordEmbed
        {
            Title = Expand(template.Title, values), Description = Expand(template.Description, values), Color = template.Color,
            Footer = new DiscordFooter { Text = $"{Config.EmbedSettings.FooterName} | server:{Api.ThisServer?.Id}" }, Timestamp = DateTimeOffset.UtcNow
        };
        if (fieldConfig.Player) embed.Fields.Add(Field("Игрок", $"{Sanitize(record.Player)}\nSteamID64: `{Sanitize(record.SteamId)}`\nhttps://steamcommunity.com/profiles/{Sanitize(record.SteamId)}"));
        if (fieldConfig.Administrator) embed.Fields.Add(Field("Администратор", $"{Sanitize(record.Administrator)}\nSteamID64: `{Sanitize(record.AdministratorSteamId)}`"));
        if (fieldConfig.Reason) embed.Fields.Add(Field("Причина", record.Reason));
        if (fieldConfig.Duration) embed.Fields.Add(Field("Длительность", FormatDuration(record.Duration)));
        if (fieldConfig.IssuedAt) embed.Fields.Add(Field("Дата выдачи", FormatDate(record.CreatedAt)));
        if (fieldConfig.ExpiresAt && record.EndAt != 0) embed.Fields.Add(Field("Дата истечения", FormatDate(record.EndAt)));
        if (fieldConfig.Type) embed.Fields.Add(Field("Тип наказания", record.EventType));
        if (fieldConfig.Ip && Config.EmbedSettings.IncludeIp) embed.Fields.Add(Field("IP-адрес", record.Ip));
        if (fieldConfig.Server) embed.Fields.Add(Field("Сервер", Api.ThisServer?.Name ?? "unknown"));
        if (fieldConfig.Online) embed.Fields.Add(Field("Онлайн", Utilities.GetPlayers().Count(x => x != null && x.IsValid).ToString(CultureInfo.InvariantCulture)));
        if (fieldConfig.PreviousPunishments)
        {
            int previous;
            lock (_sync) previous = _records.Count(x => x.SteamId == record.SteamId && x.CreatedAt < record.CreatedAt && x.EventType is "ban" or "mute" or "gag" or "silence" or "kick");
            embed.Fields.Add(Field("Предыдущие наказания", previous.ToString(CultureInfo.InvariantCulture)));
        }
        if (record.RemovedAt.HasValue) embed.Fields.Add(Field("Причина снятия", record.RemoveReason));
        return embed;
    }

    private Dictionary<string, string> Values(PunishmentRecord r) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["player"] = r.Player, ["steamid"] = r.SteamId, ["steamid64"] = r.SteamId, ["admin"] = r.Administrator,
        ["adminsteamid"] = r.AdministratorSteamId, ["reason"] = r.Reason, ["duration"] = FormatDuration(r.Duration),
        ["issuedat"] = FormatDate(r.CreatedAt), ["expiresat"] = r.EndAt == 0 ? "Навсегда" : FormatDate(r.EndAt),
        ["server"] = Api.ThisServer?.Name ?? "unknown", ["type"] = r.EventType, ["emoji"] = "📋"
    };

    private DiscordField Field(string name, string value) => new() { Name = name, Value = Sanitize(value), Inline = false };
    private string Expand(string template, Dictionary<string, string> values)
    {
        foreach (var value in values) template = template.Replace("{" + value.Key + "}", Sanitize(value.Value), StringComparison.OrdinalIgnoreCase);
        return Sanitize(template);
    }
    private string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        var clean = value.Replace("@everyone", "@ everyone", StringComparison.OrdinalIgnoreCase).Replace("@here", "@ here", StringComparison.OrdinalIgnoreCase);
        clean = new string(clean.Where(c => !char.IsControl(c) || c is '\n' or '\r' or '\t').ToArray());
        return clean.Length <= Config.EmbedSettings.MaxFieldLength ? clean : clean[..Config.EmbedSettings.MaxFieldLength];
    }
    private string FormatDate(int unix) => unix == 0 ? "-" : DateTimeOffset.FromUnixTimeSeconds(unix).ToOffset(GetTimeZone()).ToString(Config.EmbedSettings.DateFormat, CultureInfo.InvariantCulture);
    private TimeSpan GetTimeZone() => TimeZoneInfo.TryFindSystemTimeZoneById(Config.EmbedSettings.TimeZone, out var zone) ? zone.BaseUtcOffset : TimeSpan.Zero;
    private static string FormatDuration(int seconds)
    {
        if (seconds == 0) return "Навсегда";
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalDays >= 1 && span.TotalDays % 1 == 0) return $"{(int)span.TotalDays} дн.";
        if (span.TotalHours >= 1 && span.TotalHours % 1 == 0) return $"{(int)span.TotalHours} ч.";
        return $"{Math.Max(1, (int)span.TotalMinutes)} мин.";
    }

    private void OnReport(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "day";
        var target = args.Count > 1 ? args[1] : "";
        var period = mode is "admin" or "player" ? (args.Count > 2 ? args[2].ToLowerInvariant() : "all") : mode;
        var from = PeriodStart(period);
        List<PunishmentRecord> records;
        lock (_sync) records = _records.Where(x => x.CreatedAt >= from).ToList();
        var own = mode switch
        {
            "all" => records,
            "admin" => records.Where(x => x.Administrator.Contains(target, StringComparison.OrdinalIgnoreCase) || x.AdministratorSteamId == target).ToList(),
            "player" => records.Where(x => x.Player.Contains(target, StringComparison.OrdinalIgnoreCase) || x.SteamId == target).ToList(),
            _ => caller == null ? records : records.Where(x => x.AdministratorSteamId == caller.GetSteamId()).ToList()
        };
        var text = $"Отчёт ({mode}) за {period}: всего {own.Count}, банов {own.Count(x => x.EventType == "ban")}, киков {own.Count(x => x.EventType == "kick")}, наказаний чата {own.Count(x => x.EventType is "mute" or "gag" or "silence")}, снятий {own.Count(x => x.EventType is "unban" or "uncomm")}.";
        caller?.Print(text);
        _ = SendReportAsync($"{mode}/{period}", own, caller?.PlayerName ?? "CONSOLE");
    }

    private void OnExport(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        var period = args[0].ToLowerInvariant();
        List<PunishmentRecord> records;
        lock (_sync) records = _records.Where(x => x.CreatedAt >= PeriodStart(period)).ToList();
        var directory = Path.Combine(AdminUtils.ConfigsDir, ModuleName, Config.Export.Directory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"report-{period}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        var sb = new StringBuilder("type,player,steamid,administrator,admin_steamid,reason,duration,created_at,end_at\n");
        foreach (var r in records) sb.AppendLine(string.Join(',', Csv(r.EventType), Csv(r.Player), Csv(r.SteamId), Csv(r.Administrator), Csv(r.AdministratorSteamId), Csv(r.Reason), r.Duration, r.CreatedAt, r.EndAt));
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        caller?.Print($"CSV отчёт сохранён: {path}");
    }

    private async Task SendReportAsync(string period, List<PunishmentRecord> records, string author)
    {
        if (string.IsNullOrWhiteSpace(Config.Webhooks.Reports)) return;
        var embed = new DiscordEmbed { Title = $"📊 Отчёт за {period}", Description = $"Сформировал: {Sanitize(author)}\nВсего событий: **{records.Count}**", Color = 3447003, Timestamp = DateTimeOffset.UtcNow, Footer = new DiscordFooter { Text = Config.EmbedSettings.FooterName } };
        embed.Fields.Add(Field("Баны", records.Count(x => x.EventType == "ban").ToString()));
        embed.Fields.Add(Field("Кики", records.Count(x => x.EventType == "kick").ToString()));
        embed.Fields.Add(Field("Муты/гаги/сайленсы", records.Count(x => x.EventType is "mute" or "gag" or "silence").ToString()));
        embed.Fields.Add(Field("Снятия", records.Count(x => x.EventType is "unban" or "uncomm").ToString()));
        await SendWebhookWithRetry(Config.Webhooks.Reports, new { username = "IksAdmin Logs", embeds = new[] { embed } });
    }

    private void OnReload(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        var path = Path.Combine(AdminUtils.ConfigsDir, ModuleName, ModuleName + ".json");
        if (!File.Exists(path)) { caller?.Print("Конфиг Discord-логирования не найден."); return; }
        try
        {
            var parsed = JsonSerializer.Deserialize<DiscordLogsConfig>(File.ReadAllText(path), JsonOptions());
            if (parsed == null) throw new InvalidDataException("empty config");
            Config = parsed;
            caller?.Print("Конфиг Discord-логирования перечитан.");
        }
        catch (Exception e) { caller?.Print($"Ошибка конфига: {e.Message}"); }
    }

    private void OnTest(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        var record = new PunishmentRecord
        {
            EventType = "kick", Player = "Test Player", SteamId = "0", Administrator = caller?.PlayerName ?? "CONSOLE",
            AdministratorSteamId = caller?.AuthorizedSteamID?.SteamId64.ToString() ?? "CONSOLE", Reason = "Discord webhook test",
            CreatedAt = AdminUtils.CurrentTimestamp()
        };
        _ = SendAsync(record);
        caller?.Print("Тестовый Embed поставлен в очередь отправки.");
    }

    private int PeriodStart(string period) => period switch
    {
        "week" => AdminUtils.CurrentTimestamp() - 7 * 86400,
        "month" => AdminUtils.CurrentTimestamp() - 30 * 86400,
        "all" => 0,
        _ => AdminUtils.CurrentTimestamp() - 86400
    };
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private void LoadRecords()
    {
        try { if (File.Exists(_dataPath)) _records.AddRange(JsonSerializer.Deserialize<List<PunishmentRecord>>(File.ReadAllText(_dataPath)) ?? []); }
        catch { }
    }
    private void SaveRecords()
    {
        try { File.WriteAllText(_dataPath, JsonSerializer.Serialize(_records, JsonOptions())); }
        catch (Exception e) { Logger.LogError(e, "Failed to save punishment records"); }
    }
    private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true, PropertyNameCaseInsensitive = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };
}

public sealed class DiscordEmbed
{
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("color")] public int Color { get; set; }
    [JsonPropertyName("fields")] public List<DiscordField> Fields { get; set; } = new();
    [JsonPropertyName("footer")] public DiscordFooter? Footer { get; set; }
    [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; set; }
}
public sealed class DiscordField
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("value")] public string Value { get; set; } = "";
    [JsonPropertyName("inline")] public bool Inline { get; set; }
}
public sealed class DiscordFooter
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";
}
