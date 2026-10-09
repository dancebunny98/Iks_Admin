using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using System.Collections.Concurrent;
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
    public Dictionary<string, JsonElement> Messages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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

public sealed class WarningLogConfig
{
    public bool Enabled { get; set; } = true;
    public string Webhook { get; set; } = "";
    public string PlayerWebhook { get; set; } = "";
    public string ModeratorWebhook { get; set; } = "";
    public bool Issued { get; set; } = true;
    public bool Removed { get; set; } = true;
    public bool Automatic { get; set; } = true;
    public bool Test { get; set; } = true;
    public bool IncludeWarningId { get; set; } = true;
    public bool IncludeSource { get; set; } = true;
    public bool IncludeMessage { get; set; } = true;
    public bool IncludeOriginalIssuer { get; set; } = true;
    public bool IncludeRemovedAt { get; set; } = true;
    public JsonElement IssuedTemplate { get; set; } = JsonSerializer.SerializeToElement(new
    {
        username = "IksAdmin Warns",
        embeds = new[] { new { title = "Варн выдан #{warningid}", description = "**{player}** получил варн от **{admin}**. Причина: {reason}", color = 15105570 } }
    });
    public JsonElement RemovedTemplate { get; set; } = JsonSerializer.SerializeToElement(new
    {
        username = "IksAdmin Warns",
        embeds = new[] { new { title = "Варн снят #{warningid}", description = "**{admin}** снял варн с **{player}**.", color = 5763719 } }
    });
    public Dictionary<string, JsonElement> Messages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public FieldConfig Fields { get; set; } = new()
    {
        Duration = false, ExpiresAt = false, Ip = false,
        Online = false, PreviousPunishments = false
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
    public long PunishmentId { get; set; }
    public string PunishmentKind { get; set; } = "";
    public int AdminId { get; set; }
    public int? TargetAdminId { get; set; }
    public int? PunishmentServerId { get; set; }
    public sbyte? BanType { get; set; }
    public long WarningId { get; set; }
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public string OriginalIssuer { get; set; } = "";
    public bool IsModeratorWarning { get; set; }
    public bool IsTest { get; set; }
}

public sealed class Main : AdminModule, IPluginConfig<DiscordLogsConfig>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly object _sync = new();
    private readonly List<PunishmentRecord> _records = new();
    private readonly HashSet<string> _expiredSent = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _dispatchKeys = new(StringComparer.Ordinal);
    private static readonly Regex Placeholder = new(@"\{([a-z][a-z0-9_]*)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private string _dataPath = "";
    private string _dispatchStatePath = "";
    private string _failedPath = "";
    private string _warningConfigPath = "";
    private WarningLogConfig _warningConfig = new();
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
        _dispatchStatePath = Path.Combine(AdminUtils.ConfigsDir, ModuleName, "dispatch-state.json");
        _failedPath = Path.Combine(AdminUtils.ConfigsDir, ModuleName, "failed-webhooks.jsonl");
        _warningConfigPath = Path.Combine(AdminUtils.ConfigsDir, ModuleName, "warnings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_dataPath)!);
        try { _warningConfig = ReadWarningConfig(); }
        catch (Exception ex) { Logger.LogError(ex, "[{Module}] failed to load warning config", ModuleName); }
        LoadRecords();
        LoadDispatchState();
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
        Api.OnDynamicEvent += OnDynamicEvent;
        AddTimer(60.0f, OnPeriodicCheck, TimerFlags.REPEAT);
    }

    public override void Unload(bool hotReload)
    {
        Api.OnBanPost -= OnBanPost;
        Api.OnCommPost -= OnCommPost;
        Api.SuccessUnban -= OnUnban;
        Api.SuccessUnComm -= OnUncomm;
        Api.OnKickPost -= OnKickPost;
        Api.OnDynamicEvent -= OnDynamicEvent;
        _sendLock.Dispose();
        base.Unload(hotReload);
    }

    private HookResult OnBanPost(PlayerBan ban, ref bool announce)
    {
        if (ban.Id <= 0) return HookResult.Continue;
        var record = new PunishmentRecord
        {
            EventType = "ban", PunishmentId = ban.Id, PunishmentKind = "ban",
            AdminId = ban.AdminId, PunishmentServerId = ban.ServerId, BanType = ban.BanType,
            Player = ban.NameString, SteamId = ban.SteamId ?? "", Ip = ban.Ip ?? "",
            Administrator = ban.Admin?.CurrentName ?? "CONSOLE", AdministratorSteamId = ban.Admin?.SteamId ?? "CONSOLE",
            Reason = ban.Reason, Duration = ban.Duration, CreatedAt = ban.CreatedAt, EndAt = ban.EndAt
        };
        RecordAndSend(record);
        return HookResult.Continue;
    }

    private HookResult OnCommPost(PlayerComm comm, ref bool announce)
    {
        if (comm.Id <= 0) return HookResult.Continue;
        var type = comm.MuteType switch { 0 => "mute", 1 => "gag", _ => "silence" };
        var record = new PunishmentRecord
        {
            EventType = type, PunishmentId = comm.Id, PunishmentKind = type,
            AdminId = comm.AdminId, PunishmentServerId = comm.ServerId,
            Player = comm.Name ?? "[NOT SET]", SteamId = comm.SteamId, Ip = comm.Ip ?? "",
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
            EventType = "unban", PunishmentId = ban.Id, PunishmentKind = "ban",
            AdminId = admin.Id, PunishmentServerId = ban.ServerId, BanType = ban.BanType,
            Player = ban.NameString, SteamId = ban.SteamId ?? "", Ip = ban.Ip ?? "",
            Administrator = admin.CurrentName, AdministratorSteamId = admin.SteamId, Reason = ban.UnbanReason ?? "",
            Duration = ban.Duration, CreatedAt = ban.CreatedAt, EndAt = ban.EndAt, RemovedAt = AdminUtils.CurrentTimestamp(), RemoveReason = ban.UnbanReason ?? ""
        };
        RecordAndSend(record);
    }

    private void OnUncomm(Admin admin, PlayerComm comm)
    {
        var type = comm.MuteType switch { 0 => "mute", 1 => "gag", _ => "silence" };
        var record = new PunishmentRecord
        {
            EventType = "uncomm", PunishmentId = comm.Id, PunishmentKind = type,
            AdminId = admin.Id, PunishmentServerId = comm.ServerId,
            Player = comm.Name ?? "[NOT SET]", SteamId = comm.SteamId, Ip = comm.Ip ?? "",
            Administrator = admin.CurrentName, AdministratorSteamId = admin.SteamId, Reason = comm.UnbanReason ?? "",
            Duration = comm.Duration, CreatedAt = comm.CreatedAt, EndAt = comm.EndAt, RemovedAt = AdminUtils.CurrentTimestamp(), RemoveReason = comm.UnbanReason ?? ""
        };
        RecordAndSend(record);
    }

    private void OnKickPost(Admin admin, CCSPlayerController player, string reason)
    {
        var record = new PunishmentRecord
        {
            EventType = "kick", PunishmentKind = "kick", AdminId = admin.Id,
            Player = player.PlayerName, Administrator = admin.CurrentName,
            AdministratorSteamId = admin.SteamId, Reason = reason,
            CreatedAt = AdminUtils.CurrentTimestamp()
        };
        RecordAndSend(record);
    }

    private HookResult OnDynamicEvent(EventData data)
    {
        try
        {
            switch (data.EventKey)
            {
                case "create_warn_post":
                    RecordWarning(data.Get<Warn>("warn"), removed: false, null);
                    break;
                case "delete_warn_post":
                    RecordWarning(data.Get<Warn>("warn"), removed: true, data.Get<Admin>("actor"));
                    break;
                case "chat_warning_created":
                    RecordChatWarning(data, removed: false);
                    break;
                case "chat_warning_removed":
                    RecordChatWarning(data, removed: true);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[{Module}] failed to record warning event {EventKey}", ModuleName, data.EventKey);
        }
        return HookResult.Continue;
    }

    private void RecordWarning(Warn warn, bool removed, Admin? actor)
    {
        if (!ShouldRecordWarning(removed, "administrator", warn.IsTest)) return;
        var issuer = warn.Admin;
        RecordAndSend(new PunishmentRecord
        {
            EventType = removed ? "warn_removed" : "warn_issued",
            WarningId = warn.Id, PunishmentId = warn.Id, PunishmentKind = "warn",
            AdminId = (removed ? actor : issuer)?.Id ?? 0,
            TargetAdminId = warn.TargetId > 0 ? warn.TargetId : null,
            Player = warn.TargetAdmin?.CurrentName ?? warn.TargetSteamId?.ToString() ?? warn.TargetId.ToString(),
            SteamId = warn.TargetAdmin?.SteamId ?? warn.TargetSteamId?.ToString() ?? "",
            Administrator = (removed ? actor : issuer)?.CurrentName ?? "CONSOLE",
            AdministratorSteamId = (removed ? actor : issuer)?.SteamId ?? "CONSOLE",
            OriginalIssuer = issuer?.CurrentName ?? "CONSOLE",
            Reason = warn.Reason,
            Source = "administrator",
            IsModeratorWarning = true,
            IsTest = warn.IsTest,
            CreatedAt = warn.CreatedAt,
            RemovedAt = removed ? warn.DeletedAt ?? AdminUtils.CurrentTimestamp() : null
        });
    }

    private void RecordChatWarning(EventData data, bool removed)
    {
        var reason = data.Get<string>("reason");
        var source = data.Get<string>("source");
        var isTest = reason.Trim().Equals("test", StringComparison.OrdinalIgnoreCase);
        if (!ShouldRecordWarning(removed, source, isTest)) return;
        var issuer = AdminUtils.Admin(data.Get<int>("issuer_id"));
        var actor = removed ? data.Get<Admin>("actor") : issuer;
        RecordAndSend(new PunishmentRecord
        {
            EventType = removed ? "warn_removed" : "warn_issued",
            WarningId = data.Get<long>("id"), PunishmentId = data.Get<long>("id"), PunishmentKind = "warn",
            AdminId = actor?.Id ?? 0,
            Player = data.Get<string>("player_name"),
            SteamId = data.Get<ulong>("steam_id").ToString(),
            Administrator = actor?.CurrentName ?? "CONSOLE",
            AdministratorSteamId = actor?.SteamId ?? "CONSOLE",
            OriginalIssuer = issuer?.CurrentName ?? "CONSOLE",
            Reason = reason,
            Source = source,
            Message = data.Get<string>("message"),
            IsTest = isTest,
            CreatedAt = checked((int)data.Get<long>("issued_at")),
            RemovedAt = removed ? AdminUtils.CurrentTimestamp() : null
        });
    }

    private bool ShouldRecordWarning(bool removed, string source, bool isTest) =>
        _warningConfig.Enabled && (removed ? _warningConfig.Removed : _warningConfig.Issued) &&
        (_warningConfig.Automatic || !source.Equals("automatic", StringComparison.OrdinalIgnoreCase)) &&
        (_warningConfig.Test || !isTest);

    private void RecordAndSend(PunishmentRecord record)
    {
        if (!_dispatchKeys.TryAdd(DispatchKey(record), 0))
        {
            Logger.LogWarning("[{Module}] duplicate {EventType} event ignored for {SteamId} at {CreatedAt}", ModuleName, record.EventType, record.SteamId, record.CreatedAt);
            return;
        }
        lock (_sync)
        {
            _records.Add(record);
            if (_records.Count > Config.MaxStoredRecords)
                _records.RemoveRange(0, _records.Count - Config.MaxStoredRecords);
            SaveRecords();
        }
        _ = SendAsync(record);
    }

    private static string DispatchKey(PunishmentRecord record) =>
        string.Join("|", record.EventType, record.SteamId, record.CreatedAt, record.EndAt,
            record.RemovedAt, record.Reason, record.WarningId, record.PunishmentId, record.Source);

    private static int EventTimestamp(PunishmentRecord record) => record.RemovedAt ?? record.CreatedAt;

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
            if (expired.Count > 0)
                SaveDispatchState();
        }
        foreach (var record in expired)
        {
            _ = SendAsync(new PunishmentRecord
            {
                EventType = "expired", PunishmentId = record.PunishmentId, PunishmentKind = record.PunishmentKind,
                AdminId = record.AdminId, TargetAdminId = record.TargetAdminId,
                PunishmentServerId = record.PunishmentServerId, BanType = record.BanType,
                Player = record.Player, SteamId = record.SteamId, Ip = record.Ip,
                Administrator = record.Administrator, AdministratorSteamId = record.AdministratorSteamId,
                Reason = record.Reason, Duration = record.Duration, CreatedAt = record.CreatedAt, EndAt = record.EndAt
            });
        }
    }

    private async Task SendScheduledReportAsync()
    {
        var from = AdminUtils.CurrentTimestamp() - Config.Reports.IntervalMinutes * 60;
        List<PunishmentRecord> records;
        lock (_sync) records = _records.Where(x => EventTimestamp(x) >= from).ToList();
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
        var fallback = new { username = "IksAdmin Logs", embeds = new[] { anomaly }, allowed_mentions = new { parse = Array.Empty<string>() } };
        await SendWebhookWithRetry(Config.Webhooks.Anomalies,
            BuildConfiguredMessage("anomaly", ReportValues("scheduler", records, "CONSOLE"), () => fallback), "anomaly");
    }

    private async Task SendAsync(PunishmentRecord record)
    {
        try
        {
            var warningEvent = record.EventType is "warn_issued" or "warn_removed";
            var url = warningEvent
                ? record.IsModeratorWarning ? _warningConfig.ModeratorWebhook : _warningConfig.PlayerWebhook
                : Config.Webhooks.Punishments;
            if (string.IsNullOrWhiteSpace(url))
            {
                Logger.LogWarning("[{Module}] webhook is empty; event {EventType} was recorded locally but not sent", ModuleName, record.EventType);
                return;
            }
            var payload = BuildMessage(record);
            await SendWebhookWithRetry(url, payload, record.EventType);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "[{Module}] failed to prepare Discord webhook for {EventType}", ModuleName, record.EventType);
        }
    }

    private async Task SendWebhookWithRetry(string url, object payload, string eventType = "generic")
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var webhook) ||
            (webhook.Scheme != Uri.UriSchemeHttps && webhook.Scheme != Uri.UriSchemeHttp))
        {
            Logger.LogError("[{Module}] invalid Discord webhook URL for {EventType}", ModuleName, eventType);
            return;
        }
        var separator = string.IsNullOrEmpty(webhook.Query) ? "?" : "&";
        var requestUri = new Uri(webhook + $"{separator}wait=true");
        await _sendLock.WaitAsync();
        try
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    var json = JsonSerializer.Serialize(payload, JsonOptions());
                    using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                    request.Headers.UserAgent.ParseAdd("IksAdmin-DiscordPunishments/1.0");
                    using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode)
                    {
                        Logger.LogInformation("[{Module}] Discord webhook delivered for {EventType}", ModuleName, eventType);
                        return;
                    }
                    var body = await response.Content.ReadAsStringAsync();
                    Logger.LogWarning("[{Module}] Discord webhook for {EventType} returned HTTP {Status}: {Body}", ModuleName, eventType, (int)response.StatusCode, Sanitize(body));
                    if (!IsRetryable(response.StatusCode))
                    {
                        await SaveFailedPayload(payload, eventType, $"HTTP {(int)response.StatusCode}");
                        return;
                    }
                    var delay = response.StatusCode == HttpStatusCode.TooManyRequests && response.Headers.RetryAfter?.Delta is { } retryAfter
                        ? retryAfter
                        : TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                    if (attempt < 3) await Task.Delay(delay);
                    continue;
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(exception, "[{Module}] Discord webhook for {EventType} attempt {Attempt} failed", ModuleName, eventType, attempt + 1);
                }
                if (attempt < 3) await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)));
            }
            await SaveFailedPayload(payload, eventType, "retries exhausted");
        }
        finally { _sendLock.Release(); }
    }

    private async Task SaveFailedPayload(object payload, string eventType, string error)
    {
        try
        {
            var entry = new { eventType, error, payload, timestamp = DateTimeOffset.UtcNow };
            await File.AppendAllTextAsync(_failedPath, JsonSerializer.Serialize(entry, JsonOptions()) + Environment.NewLine);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "[{Module}] failed to persist Discord webhook failure", ModuleName);
        }
    }

    private static bool IsRetryable(HttpStatusCode status) =>
        status == HttpStatusCode.RequestTimeout || status == HttpStatusCode.TooManyRequests || (int)status >= 500;

    private object BuildMessage(PunishmentRecord record)
    {
        var warningEvent = record.EventType is "warn_issued" or "warn_removed";
        var key = warningEvent
            ? record.IsModeratorWarning ? "moderator_" + record.EventType : "player_" + record.EventType
            : record.EventType;
        var values = Values(record);
        if (warningEvent)
        {
            if (_warningConfig.Messages.TryGetValue(key, out var specific) &&
                TryBuildConfiguredMessage(specific, values, key, out var warningMessage)) return warningMessage;
            var shared = record.EventType == "warn_issued" ? _warningConfig.IssuedTemplate : _warningConfig.RemovedTemplate;
            if (IsDiscordMessage(shared) && TryBuildConfiguredMessage(shared, values, record.EventType, out var sharedMessage))
                return sharedMessage;
        }
        return BuildConfiguredMessage(key, values,
            () => new { username = "IksAdmin Logs", embeds = new[] { BuildEmbed(record) }, allowed_mentions = new { parse = Array.Empty<string>() } });
    }

    private object BuildConfiguredMessage(string key, Dictionary<string, string> values, Func<object> fallback)
    {
        if (Config.Messages.TryGetValue(key, out var template) &&
            TryBuildConfiguredMessage(template, values, key, out var message)) return message;
        return fallback();
    }

    private static bool IsDiscordMessage(JsonElement template) =>
        template.ValueKind == JsonValueKind.Object &&
        (template.TryGetProperty("embeds", out _) || template.TryGetProperty("content", out _) ||
         template.TryGetProperty("components", out _));

    private bool TryBuildConfiguredMessage(JsonElement template, Dictionary<string, string> values, string key, out object payload)
    {
        payload = null!;
        if (template.ValueKind != JsonValueKind.Object) return false;
        try
        {
            var message = JsonNode.Parse(template.GetRawText())!.AsObject();
            ExpandNode(message, values);
            ValidateMessage(message);
            message["allowed_mentions"] = new JsonObject { ["parse"] = new JsonArray() };
            payload = message;
            return true;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "[{Module}] invalid Discord message template {Template}", ModuleName, key);
            return false;
        }
    }

    private void ExpandNode(JsonNode node, Dictionary<string, string> values)
    {
        if (node is JsonObject obj)
        {
            foreach (var item in obj.ToList())
            {
                if (item.Value is JsonValue value && value.TryGetValue<string>(out var text))
                    obj[item.Key] = Expand(text, values);
                else if (item.Value is not null) ExpandNode(item.Value, values);
            }
        }
        else if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                if (array[i] is JsonValue value && value.TryGetValue<string>(out var text))
                    array[i] = Expand(text, values);
                else if (array[i] is not null) ExpandNode(array[i]!, values);
            }
        }
    }

    private static void ValidateMessage(JsonObject message)
    {
        if (message["attachments"] is JsonArray { Count: > 0 })
            throw new ArgumentException("Discohook attachments require multipart upload and are not supported");
        if (message["username"] is JsonValue username && username.GetValue<string>().Length > 80)
            throw new ArgumentException("Discord webhook username exceeds 80 characters");
        if (message["content"] is JsonValue content && content.GetValue<string>().Length > 2000)
            throw new ArgumentException("Discord content exceeds 2000 characters");
        if (string.IsNullOrWhiteSpace(message["content"]?.GetValue<string>()) &&
            message["embeds"] is not JsonArray { Count: > 0 })
            throw new ArgumentException("Discord message needs content or an embed");
        if (message["embeds"] is JsonArray embeds)
        {
            if (embeds.Count > 10) throw new ArgumentException("Discord supports at most 10 embeds");
            var totalLength = 0;
            foreach (var embedNode in embeds)
            {
                var embed = embedNode?.AsObject() ?? throw new ArgumentException("Invalid embed");
                foreach (var (key, limit) in new[] { ("title", 256), ("description", 4096) })
                {
                    var length = embed[key]?.GetValue<string>().Length ?? 0;
                    if (length > limit) throw new ArgumentException($"Embed {key} exceeds {limit} characters");
                    totalLength += length;
                }
                foreach (var (key, limit) in new[] { ("footer", 2048), ("author", 256) })
                {
                    var property = key == "footer" ? "text" : "name";
                    var length = embed[key]?[property]?.GetValue<string>().Length ?? 0;
                    if (length > limit) throw new ArgumentException($"Embed {key} exceeds {limit} characters");
                    totalLength += length;
                }
                if (embed["fields"] is not JsonArray fields) continue;
                if (fields.Count > 25) throw new ArgumentException("Discord supports at most 25 fields per embed");
                foreach (var fieldNode in fields)
                {
                    var field = fieldNode?.AsObject() ?? throw new ArgumentException("Invalid embed field");
                    var nameLength = field["name"]?.GetValue<string>().Length ?? 0;
                    var valueLength = field["value"]?.GetValue<string>().Length ?? 0;
                    if (nameLength is < 1 or > 256 || valueLength is < 1 or > 1024)
                        throw new ArgumentException("Discord embed field name/value length is invalid");
                    totalLength += nameLength + valueLength;
                }
            }
            if (totalLength > 6000) throw new ArgumentException("Discord embeds exceed 6000 characters total");
        }
        if (message["components"] is not JsonArray rows) return;
        if (rows.Count > 5) throw new ArgumentException("Discord supports at most 5 action rows");
        foreach (var rowNode in rows)
        {
            var row = rowNode?.AsObject() ?? throw new ArgumentException("Invalid action row");
            if (row["type"]?.GetValue<int>() != 1 || row["components"] is not JsonArray buttons || buttons.Count > 5)
                throw new ArgumentException("Invalid Discord action row");
            foreach (var buttonNode in buttons)
            {
                var button = buttonNode?.AsObject() ?? throw new ArgumentException("Invalid button");
                var url = button["url"]?.GetValue<string>();
                if (button["type"]?.GetValue<int>() != 2 || button["style"]?.GetValue<int>() != 5 ||
                    string.IsNullOrWhiteSpace(button["label"]?.GetValue<string>()) ||
                    button["label"]!.GetValue<string>().Length > 80 ||
                    !Uri.TryCreate(url, UriKind.Absolute, out var link) || link.Scheme != Uri.UriSchemeHttps)
                    throw new ArgumentException("Webhook buttons must be HTTPS link buttons (style 5)");
            }
        }
    }

    private static TemplateConfig LegacyWarningTemplate(JsonElement source, bool issued)
    {
        var fallback = new TemplateConfig
        {
            Title = issued ? "Варн выдан #{warningid}" : "Варн снят #{warningid}",
            Description = issued ? "**{player}** получил варн от **{admin}**." : "**{admin}** снял варн с **{player}**.",
            Color = issued ? 15105570 : 5763719
        };
        if (source.ValueKind != JsonValueKind.Object || IsDiscordMessage(source)) return fallback;
        try { return JsonSerializer.Deserialize<TemplateConfig>(source.GetRawText(), JsonOptions()) ?? fallback; }
        catch { return fallback; }
    }

    private DiscordEmbed BuildEmbed(PunishmentRecord record)
    {
        var warningEvent = record.EventType is "warn_issued" or "warn_removed";
        var template = warningEvent
            ? LegacyWarningTemplate(record.EventType == "warn_issued" ? _warningConfig.IssuedTemplate : _warningConfig.RemovedTemplate, record.EventType == "warn_issued")
            : Config.Templates.TryGetValue(record.EventType, out var found) ? found : new TemplateConfig();
        var fields = _warningConfig.Fields;
        if (!warningEvent)
            fields = Config.Fields.TryGetValue(record.EventType, out var configuredFields)
                ? configuredFields : new FieldConfig();
        var values = Values(record);
        var embed = new DiscordEmbed
        {
            Title = Expand(template.Title, values), Description = Expand(template.Description, values), Color = template.Color,
            Footer = new DiscordFooter { Text = $"{Config.EmbedSettings.FooterName} | server:{Api.ThisServer?.Id}" }, Timestamp = DateTimeOffset.UtcNow
        };
        if (fields.Player) embed.Fields.Add(Field("Игрок", $"{Sanitize(record.Player)}\nSteamID64: `{Sanitize(record.SteamId)}`\nhttps://steamcommunity.com/profiles/{Sanitize(record.SteamId)}"));
        if (fields.Administrator) embed.Fields.Add(Field("Администратор", $"{Sanitize(record.Administrator)}\nSteamID64: `{Sanitize(record.AdministratorSteamId)}`"));
        if (!warningEvent && record.PunishmentId > 0)
        {
            var label = record.PunishmentKind switch
            {
                "ban" => "ID бана", "mute" => "ID мута", "gag" => "ID гага", "silence" => "ID сайленса",
                _ => "ID наказания"
            };
            embed.Fields.Add(Field(label, record.PunishmentId.ToString(CultureInfo.InvariantCulture)));
        }
        if (fields.Reason) embed.Fields.Add(Field("Причина", record.Reason));
        if (fields.Duration) embed.Fields.Add(Field("Длительность", FormatDuration(record.Duration)));
        if (fields.IssuedAt) embed.Fields.Add(Field("Дата выдачи", FormatDate(record.CreatedAt)));
        if (fields.ExpiresAt && record.EndAt != 0) embed.Fields.Add(Field("Дата истечения", FormatDate(record.EndAt)));
        if (fields.Type) embed.Fields.Add(Field("Тип наказания", record.EventType));
        if (fields.Ip && Config.EmbedSettings.IncludeIp) embed.Fields.Add(Field("IP-адрес", record.Ip));
        if (fields.Server) embed.Fields.Add(Field("Сервер", Api.ThisServer?.Name ?? "unknown"));
        if (fields.Online) embed.Fields.Add(Field("Онлайн", Utilities.GetPlayers().Count(x => x != null && x.IsValid).ToString(CultureInfo.InvariantCulture)));
        if (fields.PreviousPunishments)
        {
            int previous;
            lock (_sync) previous = _records.Count(x => x.SteamId == record.SteamId && x.CreatedAt < record.CreatedAt && x.EventType is "ban" or "mute" or "gag" or "silence" or "kick");
            embed.Fields.Add(Field("Предыдущие наказания", previous.ToString(CultureInfo.InvariantCulture)));
        }
        if (warningEvent)
        {
            if (_warningConfig.IncludeWarningId)
                embed.Fields.Add(Field("ID варна", record.WarningId.ToString(CultureInfo.InvariantCulture)));
            if (_warningConfig.IncludeSource) embed.Fields.Add(Field("Источник", record.Source));
            if (_warningConfig.IncludeMessage && !string.IsNullOrWhiteSpace(record.Message))
                embed.Fields.Add(Field("Сообщение", record.Message));
            if (_warningConfig.IncludeOriginalIssuer && record.RemovedAt.HasValue)
                embed.Fields.Add(Field("Выдал", record.OriginalIssuer));
            if (_warningConfig.IncludeRemovedAt && record.RemovedAt is { } removedAt)
                embed.Fields.Add(Field("Дата снятия", FormatDate(removedAt)));
            if (record.IsTest) embed.Fields.Add(Field("Тестовый", "Да"));
        }
        if (record.RemovedAt.HasValue && !string.IsNullOrWhiteSpace(record.RemoveReason))
            embed.Fields.Add(Field("Причина снятия", record.RemoveReason));
        return embed;
    }

    private Dictionary<string, string> Values(PunishmentRecord r)
    {
        var server = Api.ThisServer;
        var kind = string.IsNullOrEmpty(r.PunishmentKind) ? r.EventType : r.PunishmentKind;
        var punishmentId = r.PunishmentId > 0 ? r.PunishmentId.ToString(CultureInfo.InvariantCulture) : "";
        var commId = kind is "mute" or "gag" or "silence" ? punishmentId : "";
        var issuedAt = FormatDate(r.CreatedAt);
        var expiresAt = r.EndAt == 0 ? "Never" : FormatDate(r.EndAt);
        var removedAt = r.RemovedAt is { } removed ? FormatDate(removed) : "";
        var playerUrl = ulong.TryParse(r.SteamId, out var playerId)
            ? $"https://steamcommunity.com/profiles/{playerId}" : "";
        var adminUrl = ulong.TryParse(r.AdministratorSteamId, out var adminId)
            ? $"https://steamcommunity.com/profiles/{adminId}" : "";
        var now = DateTimeOffset.UtcNow;
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["player"] = r.Player, ["target"] = r.Player,
            ["playername"] = r.Player, ["player_name"] = r.Player,
            ["steamid"] = r.SteamId, ["steamid64"] = r.SteamId, ["playersteamid"] = r.SteamId,
            ["player_steamid"] = r.SteamId, ["playerurl"] = playerUrl, ["player_url"] = playerUrl,
            ["admin"] = r.Administrator, ["issuer"] = r.Administrator,
            ["adminname"] = r.Administrator, ["admin_name"] = r.Administrator,
            ["adminsteamid"] = r.AdministratorSteamId, ["admin_steamid"] = r.AdministratorSteamId,
            ["adminurl"] = adminUrl, ["admin_url"] = adminUrl,
            ["adminid"] = r.AdminId > 0 ? r.AdminId.ToString(CultureInfo.InvariantCulture) : "",
            ["admin_id"] = r.AdminId > 0 ? r.AdminId.ToString(CultureInfo.InvariantCulture) : "",
            ["targetadminid"] = r.TargetAdminId?.ToString(CultureInfo.InvariantCulture) ?? "",
            ["target_admin_id"] = r.TargetAdminId?.ToString(CultureInfo.InvariantCulture) ?? "",
            ["playerip"] = r.Ip, ["player_ip"] = r.Ip, ["ip"] = r.Ip,
            ["reason"] = r.Reason, ["removereason"] = r.RemoveReason, ["remove_reason"] = r.RemoveReason,
            ["duration"] = FormatDuration(r.Duration),
            ["durationseconds"] = r.Duration.ToString(CultureInfo.InvariantCulture),
            ["duration_seconds"] = r.Duration.ToString(CultureInfo.InvariantCulture),
            ["durationminutes"] = (r.Duration / 60).ToString(CultureInfo.InvariantCulture),
            ["duration_minutes"] = (r.Duration / 60).ToString(CultureInfo.InvariantCulture),
            ["issuedat"] = issuedAt, ["issued_at"] = issuedAt, ["createdat"] = issuedAt,
            ["issuediso"] = DateTimeOffset.FromUnixTimeSeconds(r.CreatedAt).ToString("O"),
            ["issued_iso"] = DateTimeOffset.FromUnixTimeSeconds(r.CreatedAt).ToString("O"),
            ["expiresat"] = expiresAt, ["expires_at"] = expiresAt,
            ["expiresiso"] = r.EndAt > 0 ? DateTimeOffset.FromUnixTimeSeconds(r.EndAt).ToString("O") : "",
            ["expires_iso"] = r.EndAt > 0 ? DateTimeOffset.FromUnixTimeSeconds(r.EndAt).ToString("O") : "",
            ["removedat"] = removedAt, ["removed_at"] = removedAt,
            ["removediso"] = r.RemovedAt is { } removedTime ? DateTimeOffset.FromUnixTimeSeconds(removedTime).ToString("O") : "",
            ["removed_iso"] = r.RemovedAt is { } removedTime2 ? DateTimeOffset.FromUnixTimeSeconds(removedTime2).ToString("O") : "",
            ["createdunix"] = r.CreatedAt.ToString(CultureInfo.InvariantCulture),
            ["created_unix"] = r.CreatedAt.ToString(CultureInfo.InvariantCulture),
            ["expiresunix"] = r.EndAt.ToString(CultureInfo.InvariantCulture),
            ["expires_unix"] = r.EndAt.ToString(CultureInfo.InvariantCulture),
            ["removedunix"] = r.RemovedAt?.ToString(CultureInfo.InvariantCulture) ?? "",
            ["removed_unix"] = r.RemovedAt?.ToString(CultureInfo.InvariantCulture) ?? "",
            ["server"] = server?.Name ?? "unknown", ["servername"] = server?.Name ?? "unknown",
            ["server_name"] = server?.Name ?? "unknown", ["serverid"] = server?.Id.ToString() ?? "",
            ["server_id"] = server?.Id.ToString() ?? "", ["serverip"] = server?.Ip ?? "",
            ["server_ip"] = server?.Ip ?? "",
            ["online"] = Utilities.GetPlayers().Count(x => x is { IsValid: true }).ToString(CultureInfo.InvariantCulture),
            ["now"] = now.ToOffset(GetTimeZone()).ToString(Config.EmbedSettings.DateFormat, CultureInfo.InvariantCulture),
            ["nowunix"] = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["nowiso"] = now.ToString("O"),
            ["type"] = r.EventType, ["event"] = r.EventType, ["emoji"] = "📋",
            ["punishmentid"] = punishmentId, ["punishment_id"] = punishmentId, ["id"] = punishmentId,
            ["punishmentkind"] = kind, ["punishment_kind"] = kind,
            ["banid"] = kind == "ban" ? punishmentId : "", ["ban_id"] = kind == "ban" ? punishmentId : "",
            ["bantype"] = r.BanType?.ToString(CultureInfo.InvariantCulture) ?? "",
            ["ban_type"] = r.BanType?.ToString(CultureInfo.InvariantCulture) ?? "",
            ["commid"] = commId, ["comm_id"] = commId,
            ["muteid"] = kind == "mute" ? punishmentId : "", ["mute_id"] = kind == "mute" ? punishmentId : "",
            ["gagid"] = kind == "gag" ? punishmentId : "", ["gag_id"] = kind == "gag" ? punishmentId : "",
            ["silenceid"] = kind == "silence" ? punishmentId : "", ["silence_id"] = kind == "silence" ? punishmentId : "",
            ["warningid"] = r.WarningId.ToString(CultureInfo.InvariantCulture),
            ["warning_id"] = r.WarningId.ToString(CultureInfo.InvariantCulture),
            ["punishmentserverid"] = r.PunishmentServerId?.ToString(CultureInfo.InvariantCulture) ?? "",
            ["punishment_server_id"] = r.PunishmentServerId?.ToString(CultureInfo.InvariantCulture) ?? "",
            ["source"] = r.Source, ["message"] = r.Message,
            ["originalissuer"] = r.OriginalIssuer, ["original_issuer"] = r.OriginalIssuer,
            ["test"] = r.IsTest ? "true" : "false",
            ["ismoderatorwarning"] = r.IsModeratorWarning ? "true" : "false"
        };
    }

    private DiscordField Field(string name, string value) => new() { Name = name, Value = Sanitize(value), Inline = false };
    private string Expand(string template, Dictionary<string, string> values)
    {
        return Placeholder.Replace(template, match => values.TryGetValue(match.Groups[1].Value, out var value)
            ? SanitizeText(value) : match.Value);
    }
    private static string SanitizeText(string? value) =>
        new string((value ?? "").Replace("@everyone", "@ everyone", StringComparison.OrdinalIgnoreCase)
            .Replace("@here", "@ here", StringComparison.OrdinalIgnoreCase)
            .Where(c => !char.IsControl(c) || c is '\n' or '\r' or '\t').ToArray());
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
        lock (_sync) records = _records.Where(x => EventTimestamp(x) >= from).ToList();
        var own = mode switch
        {
            "all" => records,
            "admin" => records.Where(x => x.Administrator.Contains(target, StringComparison.OrdinalIgnoreCase) || x.AdministratorSteamId == target).ToList(),
            "player" => records.Where(x => x.Player.Contains(target, StringComparison.OrdinalIgnoreCase) || x.SteamId == target).ToList(),
            _ => caller == null ? records : records.Where(x => x.AdministratorSteamId == caller.GetSteamId()).ToList()
        };
        var text = $"Отчёт ({mode}) за {period}: всего {own.Count}, банов {own.Count(x => x.EventType == "ban")}, киков {own.Count(x => x.EventType == "kick")}, наказаний чата {own.Count(x => x.EventType is "mute" or "gag" or "silence")}, снятий {own.Count(x => x.EventType is "unban" or "uncomm")}, варнов {own.Count(x => x.EventType == "warn_issued")}, снятых варнов {own.Count(x => x.EventType == "warn_removed")}.";
        caller?.Print(text);
        _ = SendReportAsync($"{mode}/{period}", own, caller?.PlayerName ?? "CONSOLE");
    }

    private void OnExport(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        var period = args[0].ToLowerInvariant();
        List<PunishmentRecord> records;
        lock (_sync) records = _records.Where(x => EventTimestamp(x) >= PeriodStart(period)).ToList();
        var directory = Path.Combine(AdminUtils.ConfigsDir, ModuleName, Config.Export.Directory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"report-{period}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        var sb = new StringBuilder("type,player,steamid,administrator,admin_steamid,reason,duration,created_at,end_at,removed_at,warning_id,source,message,original_issuer,is_test,punishment_id,punishment_kind,admin_id,target_admin_id,punishment_server_id,ban_type\n");
        foreach (var r in records)
            sb.AppendLine(string.Join(',', Csv(r.EventType), Csv(r.Player), Csv(r.SteamId), Csv(r.Administrator),
                Csv(r.AdministratorSteamId), Csv(r.Reason), r.Duration, r.CreatedAt, r.EndAt,
                r.RemovedAt?.ToString(CultureInfo.InvariantCulture) ?? "", r.WarningId, Csv(r.Source),
                Csv(r.Message), Csv(r.OriginalIssuer), r.IsTest,
                r.PunishmentId > 0 ? r.PunishmentId.ToString(CultureInfo.InvariantCulture) : "", Csv(r.PunishmentKind),
                r.AdminId > 0 ? r.AdminId.ToString(CultureInfo.InvariantCulture) : "", r.TargetAdminId?.ToString(CultureInfo.InvariantCulture) ?? "",
                r.PunishmentServerId?.ToString(CultureInfo.InvariantCulture) ?? "", r.BanType?.ToString(CultureInfo.InvariantCulture) ?? ""));
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
        embed.Fields.Add(Field("Варны выданы", records.Count(x => x.EventType == "warn_issued").ToString()));
        embed.Fields.Add(Field("Варны сняты", records.Count(x => x.EventType == "warn_removed").ToString()));
        var fallback = new { username = "IksAdmin Logs", embeds = new[] { embed }, allowed_mentions = new { parse = Array.Empty<string>() } };
        await SendWebhookWithRetry(Config.Webhooks.Reports,
            BuildConfiguredMessage("report", ReportValues(period, records, author), () => fallback), "report");
    }

    private Dictionary<string, string> ReportValues(string period, List<PunishmentRecord> records, string author)
    {
        var now = DateTimeOffset.UtcNow;
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["period"] = period, ["author"] = author,
            ["count"] = records.Count.ToString(CultureInfo.InvariantCulture),
            ["bans"] = records.Count(x => x.EventType == "ban").ToString(CultureInfo.InvariantCulture),
            ["kicks"] = records.Count(x => x.EventType == "kick").ToString(CultureInfo.InvariantCulture),
            ["comms"] = records.Count(x => x.EventType is "mute" or "gag" or "silence").ToString(CultureInfo.InvariantCulture),
            ["unbans"] = records.Count(x => x.EventType == "unban").ToString(CultureInfo.InvariantCulture),
            ["uncomms"] = records.Count(x => x.EventType == "uncomm").ToString(CultureInfo.InvariantCulture),
            ["warnings"] = records.Count(x => x.EventType == "warn_issued").ToString(CultureInfo.InvariantCulture),
            ["removedwarnings"] = records.Count(x => x.EventType == "warn_removed").ToString(CultureInfo.InvariantCulture),
            ["threshold"] = Config.Reports.AnomalyThresholdPerHour.ToString(CultureInfo.InvariantCulture),
            ["servername"] = Api.ThisServer?.Name ?? "unknown", ["server"] = Api.ThisServer?.Name ?? "unknown",
            ["serverid"] = Api.ThisServer?.Id.ToString() ?? "", ["serverip"] = Api.ThisServer?.Ip ?? "",
            ["now"] = now.ToOffset(GetTimeZone()).ToString(Config.EmbedSettings.DateFormat, CultureInfo.InvariantCulture),
            ["nowunix"] = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ["nowiso"] = now.ToString("O")
        };
    }

    private void OnReload(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        var path = Path.Combine(AdminUtils.ConfigsDir, ModuleName, ModuleName + ".json");
        if (!File.Exists(path)) { caller?.Print("Конфиг Discord-логирования не найден."); return; }
        try
        {
            var parsed = JsonSerializer.Deserialize<DiscordLogsConfig>(File.ReadAllText(path), JsonOptions());
            if (parsed == null) throw new InvalidDataException("empty config");
            if (string.IsNullOrWhiteSpace(parsed.Webhooks.Punishments))
                parsed.Webhooks.Punishments = parsed.Webhooks.Default;
            var warnings = ReadWarningConfig();
            Config = parsed;
            _warningConfig = warnings;
            caller?.Print("Конфиг Discord-логирования перечитан.");
        }
        catch (Exception e) { caller?.Print($"Ошибка конфига: {e.Message}"); }
    }

    private WarningLogConfig ReadWarningConfig()
    {
        if (!File.Exists(_warningConfigPath))
            File.WriteAllText(_warningConfigPath, JsonSerializer.Serialize(new WarningLogConfig(), JsonOptions()));
        return JsonSerializer.Deserialize<WarningLogConfig>(File.ReadAllText(_warningConfigPath), JsonOptions())
            ?? throw new InvalidDataException("empty warning config");
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
    private void LoadDispatchState()
    {
        try
        {
            if (!File.Exists(_dispatchStatePath)) return;
            var sent = JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(_dispatchStatePath), JsonOptions());
            if (sent != null) _expiredSent.UnionWith(sent);
        }
        catch (Exception e) { Logger.LogWarning(e, "Failed to load Discord webhook dispatch state"); }
    }
    private void SaveDispatchState()
    {
        try { File.WriteAllText(_dispatchStatePath, JsonSerializer.Serialize(_expiredSent, JsonOptions())); }
        catch (Exception e) { Logger.LogError(e, "Failed to save Discord webhook dispatch state"); }
    }
    private void SaveRecords()
    {
        try { File.WriteAllText(_dataPath, JsonSerializer.Serialize(_records, JsonOptions())); }
        catch (Exception e) { Logger.LogError(e, "Failed to save punishment records"); }
    }
    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
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
