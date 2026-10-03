using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using IksAdminApi;
using Microsoft.Extensions.Logging;

namespace IksAdmin_DiscordPunishments;

public sealed class DiscordPunishmentsConfig : BasePluginConfig
{
    public override int Version { get; set; } = 1;
    public GeneralConfig General { get; set; } = new();
    // Не заполняются в конфиге модуля: берутся из IksAdmin core.json и ThisServer.
    // Оставлены как fallback для старых конфигов и автономного запуска.
    public string? ServerName { get; set; }
    public string? ServerId { get; set; }
    public WebhookConfig Webhooks { get; set; } = new();
    public EventConfig Events { get; set; } = new();
    public EmbedConfig Embed { get; set; } = new();
    public TemplateConfig Templates { get; set; } = new();
    public ThrottleConfig Throttling { get; set; } = new();
    public RetryConfig Retry { get; set; } = new();
    public LocalLogConfig LocalLog { get; set; } = new();
    public PrivacyConfig Privacy { get; set; } = new();
    public IntegrationConfig Integration { get; set; } = new();
    public ReactionConfig Reactions { get; set; } = new();
}
public sealed class GeneralConfig { public bool Enabled { get; set; } = true; public bool Debug { get; set; } public string TimeZone { get; set; } = "UTC"; public string Language { get; set; } = "en"; }
public sealed class WebhookConfig { public string Default { get; set; } = ""; public string Username { get; set; } = ""; public string AvatarUrl { get; set; } = ""; public string ThreadName { get; set; } = ""; public bool Tts { get; set; } public bool WaitForPrevious { get; set; } = true; public List<string> Ban { get; set; } = new(); public List<string> Unban { get; set; } = new(); public List<string> Kick { get; set; } = new(); public List<string> Mute { get; set; } = new(); public List<string> Unmute { get; set; } = new(); public List<string> Gag { get; set; } = new(); public List<string> Ungag { get; set; } = new(); public List<string> Silence { get; set; } = new(); public List<string> Unsilence { get; set; } = new(); }
public sealed class EventConfig { public bool Ban { get; set; } = true; public bool Unban { get; set; } = true; public bool Kick { get; set; } = true; public bool Mute { get; set; } = true; public bool Unmute { get; set; } = true; public bool Gag { get; set; } = true; public bool Ungag { get; set; } = true; public bool Silence { get; set; } = true; public bool Unsilence { get; set; } = true; public bool Expire { get; set; } }
public sealed class EmbedConfig { public bool ShowThumbnail { get; set; } = true; public bool ShowPlayer { get; set; } = true; public bool ShowAdmin { get; set; } = true; public bool ShowReason { get; set; } = true; public bool ShowDuration { get; set; } = true; public bool ShowDates { get; set; } = true; public bool ShowIp { get; set; } public bool ShowServer { get; set; } = true; public bool ShowType { get; set; } = true; public bool UseInlineFields { get; set; } = true; public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss 'UTC'"; public string FooterText { get; set; } = "IksAdmin punishment log | {server_id}"; public Dictionary<string, int> Colors { get; set; } = new() { ["Ban"] = 15158332, ["Unban"] = 5763719, ["Kick"] = 15844367, ["Mute"] = 15105570, ["Unmute"] = 5763719, ["Gag"] = 15105570, ["Ungag"] = 5763719, ["Silence"] = 15105570, ["Unsilence"] = 5763719 }; }
public sealed class TemplateConfig { public string BanTitle { get; set; } = "Игрок наказан"; public string UnbanTitle { get; set; } = "Наказание снято"; public string KickTitle { get; set; } = "Игрок исключён"; public string MuteTitle { get; set; } = "Игрок получил мут голоса"; public string UnmuteTitle { get; set; } = "Мут голоса снят"; public string GagTitle { get; set; } = "Игрок получил мут чата"; public string UngagTitle { get; set; } = "Мут чата снят"; public string SilenceTitle { get; set; } = "Игрок получил мут голоса и чата"; public string UnsilenceTitle { get; set; } = "Мут голоса и чата снят"; public string Description { get; set; } = "Игрок {player_name} получил наказание {type} от администратора {admin_name}"; public string ReasonWhenEmpty { get; set; } = "Причина не указана"; }
public sealed class ThrottleConfig { public int MessagesPerMinute { get; set; } = 30; public int MaxQueueSize { get; set; } = 500; public bool DropWhenFull { get; set; } = false; public bool PerWebhook { get; set; } = true; }
public sealed class RetryConfig { public int Attempts { get; set; } = 5; public int BaseDelaySeconds { get; set; } = 2; public int MaxDelaySeconds { get; set; } = 60; public bool Exponential { get; set; } = true; public int JitterSeconds { get; set; } = 1; public bool RetryOnRateLimit { get; set; } = true; }
public sealed class LocalLogConfig { public bool Enabled { get; set; } = true; public string Path { get; set; } = "logs/discord-punishments.jsonl"; public long MaxFileBytes { get; set; } = 10 * 1024 * 1024; public int RetentionDays { get; set; } = 30; public bool IncludePayload { get; set; } = true; public bool FlushImmediately { get; set; } = true; }
public sealed class PrivacyConfig { public bool LogIp { get; set; } public bool MaskIp { get; set; } = true; public bool MaskSteamId { get; set; } public bool AllowMentions { get; set; } public int MaxTextLength { get; set; } = 1000; }
public sealed class IntegrationConfig { public bool UseIksAdminApi { get; set; } = true; public bool ListenToCommandFallback { get; set; } public List<string> BanCommands { get; set; } = new() { "css_ban", "css_addban" }; public List<string> KickCommands { get; set; } = new() { "css_kick" }; public List<string> MuteCommands { get; set; } = new() { "css_mute", "css_gag", "css_silence" }; }
public sealed class ReactionConfig { public bool Enabled { get; set; } public bool WaitForMessage { get; set; } = true; public Dictionary<string, List<string>> ByType { get; set; } = new() { ["Ban"] = new() { "🔨" }, ["Unban"] = new() { "✅" }, ["Kick"] = new() { "👢" }, ["Mute"] = new() { "🔇" }, ["Unmute"] = new() { "🔊" } }; }

public sealed class Main : AdminModule, IPluginConfig<DiscordPunishmentsConfig>
{
    private readonly PluginCapability<IIksAdminApi> _capability = new("iksadmin:core");
    private readonly HttpClient _http = new();
    private readonly ConcurrentQueue<Queued> _queue = new();
    private CancellationTokenSource _stop = new();
    private Task? _worker;
    private DateTime _window = DateTime.UtcNow;
    private int _sentInWindow;
    public DiscordPunishmentsConfig Config { get; set; } = new();
    public override string ModuleName => "IksAdmin_DiscordPunishments";
    public override string ModuleVersion => $"v{typeof(Main).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";
    public override string ModuleAuthor => "iks__ modules";
    public void OnConfigParsed(DiscordPunishmentsConfig config) => Config = config;
    private string ServerName => !string.IsNullOrWhiteSpace(Api.ThisServer?.Name) ? Api.ThisServer.Name : (Config.ServerName ?? Api.Config.ServerName);
    private string ServerId => Api.ThisServer?.Id.ToString() ?? (Config.ServerId ?? Api.Config.ServerId.ToString());

    public override void Ready()
    {
        Api.OnBanPost += OnBan; Api.OnUnBanPost += OnUnban; Api.OnCommPost += OnComm; Api.OnUnCommPost += OnUncomm; Api.OnDynamicEvent += OnDynamic;
        Api.RegisterPermission("discord_logs.reload", "z");
        Api.AddNewCommand("discord_logs_reload", "Reload punishment Discord logger configuration", "discord_logs.reload", "css_discord_logs_reload", (_, _, _) => ReloadConfig());
        Api.AddNewCommand("discord_logs_test", "Send a test punishment Embed", "discord_logs.reload", "css_discord_logs_test", (_, _, _) => SendTest());
        var defaultConfigured = Uri.TryCreate(Config.Webhooks.Default, UriKind.Absolute, out _);
        Logger.LogInformation("Discord punishment logger loaded. Default webhook configured: {Configured}; server: {Server} ({Id})", defaultConfigured, ServerName, ServerId);
        if (!defaultConfigured && !Hooks("Ban").Any())
            Logger.LogWarning("No valid Discord webhook configured. Set Webhooks.Default or an event-specific webhook.");
        _worker = Task.Run(DispatchLoop);
    }
    public override void Unload(bool hotReload)
    {
        Api.OnBanPost -= OnBan; Api.OnUnBanPost -= OnUnban; Api.OnCommPost -= OnComm; Api.OnUnCommPost -= OnUncomm; Api.OnDynamicEvent -= OnDynamic;
        _stop.Cancel(); _http.Dispose();
        base.Unload(hotReload);
    }
    private void ReloadConfig() { try { var candidates = new[] { Path.Combine(ModuleDirectory, "config.json"), Path.Combine(ModuleDirectory, "..", "..", "configs", "plugins", ModuleName, ModuleName + ".json") }; var path = candidates.FirstOrDefault(File.Exists); if (path == null) { Logger.LogWarning("Discord logger config file was not found"); return; } Config = JsonSerializer.Deserialize<DiscordPunishmentsConfig>(File.ReadAllText(path), new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip }) ?? Config; Logger.LogInformation("Discord logger config reloaded from {Path}", path); } catch (Exception e) { Logger.LogError(e, "Unable to reload config"); } }
    private void SendTest() { if (Api.ConsoleAdmin == null) { Logger.LogWarning("Cannot send test Embed: IksAdmin console admin is unavailable"); return; } Enqueue("Ban", "Тестовый игрок", "76561198000000000", null, "Проверка Discord webhook", 60, (int)DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(), Api.ConsoleAdmin, null); Logger.LogInformation("Test Discord punishment Embed queued"); }
    private CounterStrikeSharp.API.Core.HookResult OnBan(PlayerBan ban, ref bool announce) { if (Config.Events.Ban && ban.Admin != null) Enqueue("Ban", ban.Name, ban.SteamId, ban.Ip, ban.Reason, ban.Duration, ban.EndAt, ban.Admin, null); return CounterStrikeSharp.API.Core.HookResult.Continue; }
    private CounterStrikeSharp.API.Core.HookResult OnUnban(Admin admin, ref string arg, ref string? reason, ref bool announce) { if (Config.Events.Unban) _ = CaptureUnban(admin, arg, reason); return CounterStrikeSharp.API.Core.HookResult.Continue; }
    private CounterStrikeSharp.API.Core.HookResult OnComm(PlayerComm comm, ref bool announce) { var type = TypeName(comm.MuteType); if (Enabled(type) && comm.Admin != null) Enqueue(type, comm.Name, comm.SteamId, comm.Ip, comm.Reason, comm.Duration, comm.EndAt, comm.Admin, null); return CounterStrikeSharp.API.Core.HookResult.Continue; }
    private CounterStrikeSharp.API.Core.HookResult OnUncomm(Admin admin, ref string steamId, ref string? reason, ref bool announce) { _ = CaptureUncomm(admin, steamId, reason); return CounterStrikeSharp.API.Core.HookResult.Continue; }
    private CounterStrikeSharp.API.Core.HookResult OnDynamic(EventData data) { if (data.EventKey != "kick_player_post" || !Config.Events.Kick) return CounterStrikeSharp.API.Core.HookResult.Continue; try { var p = data.Get<CCSPlayerController>("player"); Enqueue("Kick", p.PlayerName, p.SteamID.ToString(), null, data.Get<string>("reason"), 0, 0, data.Get<Admin>("admin"), null); } catch { } return CounterStrikeSharp.API.Core.HookResult.Continue; }
    private async Task CaptureUnban(Admin admin, string steam, string? reason) { var b = await Api.GetActiveBan(steam) ?? (await Api.GetAllBans(steam)).OrderByDescending(x => x.UpdatedAt).FirstOrDefault(); Enqueue("Unban", b?.Name, steam, b?.Ip, reason ?? b?.UnbanReason ?? "", 0, 0, admin, null); }
    private async Task CaptureUncomm(Admin admin, string steam, string? reason) { var c = (await Api.GetAllComms(steam)).OrderByDescending(x => x.UpdatedAt).FirstOrDefault(); var type = c == null ? "Unmute" : "Un" + TypeName(c.MuteType); if (Enabled(type)) Enqueue(type, c?.Name, steam, c?.Ip, reason ?? c?.UnbanReason ?? "", 0, 0, admin, null); }
    private void Enqueue(string type, string? player, string? steam, string? ip, string reason, int duration, int endAt, Admin admin, string? avatar) { var hooks = Hooks(type); if (!Config.General.Enabled || !Enabled(type) || hooks.Count == 0) return; if (Config.Throttling.DropWhenFull && _queue.Count >= Config.Throttling.MaxQueueSize) { Logger.LogWarning("Punishment notification queue is full; event {Type} was dropped", type); return; } var payload = Build(type, player, steam, ip, reason, duration, endAt, admin, avatar); foreach (var hook in hooks) _queue.Enqueue(new Queued(hook, payload, type, 0)); WriteLocal(type, payload); }
    private List<string> Hooks(string type) { var p = typeof(WebhookConfig).GetProperty(type); var values = (p?.GetValue(Config.Webhooks) as List<string> ?? new()).Where(x => Uri.TryCreate(x, UriKind.Absolute, out _)).ToList(); if (values.Count == 0 && Uri.TryCreate(Config.Webhooks.Default, UriKind.Absolute, out _)) values.Add(Config.Webhooks.Default); return values; }
    private bool Enabled(string type) => typeof(EventConfig).GetProperty(type)?.GetValue(Config.Events) as bool? == true;
    private static string TypeName(int t) => t switch { 1 => "Gag", 2 => "Silence", _ => "Mute" };
    private object Build(string type, string? player, string? steam, string? ip, string reason, int duration, int endAt, Admin admin, string? avatar) { var clean = (string s) => Sanitize(s); var p = clean(player ?? "[неизвестно]"); var playerId = clean(steam ?? "неизвестно"); var a = clean(admin.CurrentName); var adminId = clean(admin.SteamId); var safeReason = clean(string.IsNullOrWhiteSpace(reason) ? Config.Templates.ReasonWhenEmpty : reason); var inline = Config.Embed.UseInlineFields; var fields = new List<object>(); if (Config.Embed.ShowPlayer) fields.Add(new { name = "Игрок", value = $"{p}\n`{playerId}`", inline }); if (Config.Embed.ShowAdmin) fields.Add(new { name = "Администратор", value = $"{a}\n`{adminId}`", inline }); if (Config.Embed.ShowReason) fields.Add(new { name = "Причина", value = safeReason, inline = false }); if (Config.Embed.ShowDuration) fields.Add(new { name = "Длительность", value = duration == 0 ? "Навсегда" : TimeSpan.FromSeconds(duration).ToString(), inline }); if (Config.Embed.ShowDates) fields.Add(new { name = "Выдано (UTC)", value = DateTime.UtcNow.ToString(Config.Embed.DateFormat), inline }); if (endAt > 0) fields.Add(new { name = "Истекает", value = DateTimeOffset.FromUnixTimeSeconds(endAt).UtcDateTime.ToString(Config.Embed.DateFormat), inline }); if (Config.Embed.ShowIp && Config.Privacy.LogIp && !string.IsNullOrWhiteSpace(ip)) fields.Add(new { name = "IP-адрес", value = Config.Privacy.MaskIp ? MaskIp(ip) : clean(ip), inline }); if (Config.Embed.ShowType) fields.Add(new { name = "Тип наказания", value = TypeLabel(type), inline }); if (Config.Embed.ShowServer) fields.Add(new { name = "Сервер", value = clean(ServerName), inline }); var title = typeof(TemplateConfig).GetProperty(type + "Title")?.GetValue(Config.Templates)?.ToString() ?? type; var desc = Config.Templates.Description.Replace("{player_name}", p).Replace("{player_steamid}", playerId).Replace("{admin_name}", a).Replace("{admin_steamid}", adminId).Replace("{reason}", safeReason).Replace("{duration}", duration == 0 ? "Навсегда" : TimeSpan.FromSeconds(duration).ToString()).Replace("{date}", DateTime.UtcNow.ToString(Config.Embed.DateFormat)).Replace("{server_name}", clean(ServerName)).Replace("{server_id}", clean(ServerId)).Replace("{type}", TypeLabel(type)); var footer = Config.Embed.FooterText.Replace("{server_id}", clean(ServerId)).Replace("{server_name}", clean(ServerName)); var embed = new Dictionary<string, object?> { ["title"] = title, ["description"] = desc, ["color"] = Config.Embed.Colors.GetValueOrDefault(type, 9807270), ["fields"] = fields, ["timestamp"] = DateTime.UtcNow.ToString("O"), ["footer"] = new { text = footer } }; if (Config.Embed.ShowThumbnail && !string.IsNullOrWhiteSpace(avatar)) embed["thumbnail"] = new { url = avatar }; return new { embeds = new[] { embed }, username = Config.Webhooks.Username, avatar_url = Config.Webhooks.AvatarUrl, thread_name = Config.Webhooks.ThreadName, allowed_mentions = new { parse = Config.Privacy.AllowMentions ? new[] { "users" } : Array.Empty<string>() } }; }
    private static string TypeLabel(string type) => type switch { "Ban" => "Бан", "Unban" => "Разбан", "Kick" => "Кик", "Mute" => "Мут голоса", "Unmute" => "Снятие мута голоса", "Gag" => "Gag чата", "Ungag" => "Снятие gag", "Silence" => "Мут голоса и чата", "Unsilence" => "Снятие silence", _ => type };
    private string Sanitize(string value) { var s = new string(value.Where(c => !char.IsControl(c)).ToArray()); if (!Config.Privacy.AllowMentions) s = s.Replace("@everyone", "@ everyone", StringComparison.OrdinalIgnoreCase).Replace("@here", "@ here", StringComparison.OrdinalIgnoreCase); return s.Length <= Config.Privacy.MaxTextLength ? s : s[..Config.Privacy.MaxTextLength]; }
    private static string MaskIp(string ip) { var i = ip.LastIndexOf('.'); return i > 0 ? ip[..i] + ".***" : "***"; }
    private void WriteLocal(string type, object payload) { if (!Config.LocalLog.Enabled) return; try { var path = Path.Combine(ModuleDirectory, Config.LocalLog.Path); Directory.CreateDirectory(Path.GetDirectoryName(path)!); if (File.Exists(path) && new FileInfo(path).Length > Config.LocalLog.MaxFileBytes) File.Move(path, path + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true); File.AppendAllText(path, JsonSerializer.Serialize(new { type, sentAt = DateTime.UtcNow, payload }) + Environment.NewLine); } catch (Exception e) { Logger.LogError(e, "Unable to write local punishment log"); } }
    private async Task DispatchLoop() { while (!_stop.IsCancellationRequested) { if (!_queue.TryDequeue(out var item)) { await Task.Delay(250, _stop.Token).ContinueWith(_ => { }); continue; } if (DateTime.UtcNow - _window >= TimeSpan.FromMinutes(1)) { _window = DateTime.UtcNow; _sentInWindow = 0; } if (_sentInWindow >= Math.Max(1, Config.Throttling.MessagesPerMinute)) { await Task.Delay(TimeSpan.FromMinutes(1), _stop.Token).ContinueWith(_ => { }); _window = DateTime.UtcNow; _sentInWindow = 0; } try { var response = await _http.PostAsJsonAsync(AddWaitParameter(item.Url), item.Payload, _stop.Token); if (response.IsSuccessStatusCode) { _sentInWindow++; if (Config.Reactions.Enabled) await AddReactions(item, response); continue; } throw new HttpRequestException($"Discord returned {(int)response.StatusCode}"); } catch (Exception e) { if (item.Attempt + 1 < Config.Retry.Attempts) { var seconds = Math.Min(Config.Retry.MaxDelaySeconds, Config.Retry.BaseDelaySeconds * (Config.Retry.Exponential ? Math.Pow(2, item.Attempt) : item.Attempt + 1)); await Task.Delay(TimeSpan.FromSeconds(seconds), _stop.Token).ContinueWith(_ => { }); _queue.Enqueue(item with { Attempt = item.Attempt + 1 }); } else Logger.LogError(e, "Discord punishment notification failed after retries"); } } }
    private static string AddWaitParameter(string url) => url + (url.Contains('?') ? "&wait=true" : "?wait=true");
    private async Task AddReactions(Queued item, HttpResponseMessage response) { if (!Config.Reactions.ByType.TryGetValue(item.Type, out var emojis) || emojis.Count == 0) return; try { using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(_stop.Token), cancellationToken: _stop.Token); if (!doc.RootElement.TryGetProperty("id", out var id)) return; foreach (var emoji in emojis.Where(x => !string.IsNullOrWhiteSpace(x))) { var endpoint = $"{item.Url.TrimEnd('/')}/messages/{id.GetString()}/reactions/{Uri.EscapeDataString(emoji)}/@me"; await _http.PutAsync(endpoint, null, _stop.Token); } } catch (Exception e) { Logger.LogWarning(e, "Unable to add Discord reactions for {Type}", item.Type); } }
    private sealed record Queued(string Url, object Payload, string Type, int Attempt);
}
