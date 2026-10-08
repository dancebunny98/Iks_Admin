using System.Text.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using IksAdminApi;
using Microsoft.Extensions.Logging;

namespace IksAdmin_ChatModeration;

public sealed class ChatModeration : AdminModule, IPluginConfig<ChatModerationConfig>
{
    public override string ModuleName => "IksAdmin_ChatModeration";
    public override string ModuleVersion => $"v{typeof(ChatModeration).Assembly.GetName().Version?.ToString(3) ?? "4.0.0"}";
    public override string ModuleAuthor => "IksAdmin modules";

    private const string WarnPermission = "chat_moderation.warn";
    private const string ReviewPermission = "chat_moderation.review";
    private const string RevokePermission = "chat_moderation.revoke";
    private const string SelfPermission = "chat_moderation.self";
    private WarningStore? _store;
    private volatile bool _storeReady;
    private readonly Dictionary<string, long> _lastViolation = new();
    private readonly Dictionary<ulong, string> _lastMessage = new();
    private readonly Dictionary<(string Pattern, bool CaseSensitive), Regex> _regexRules = new();
    private static readonly Regex LinkRegex = new(@"(?:https?://|www\.)[^\s<>""']+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private Dictionary<string, string> _translations = new();

    public ChatModerationConfig Config { get; set; } = new();
    public void OnConfigParsed(ChatModerationConfig config) => Config = config;

    private string T(string key, params object[] args) =>
        string.Format(_translations.GetValueOrDefault(key, key), args);

    private string Reason(ChatRule rule) =>
        _translations.GetValueOrDefault(rule.Reason, rule.Reason);

    private string Source(PlayerWarning warning)
    {
        if (warning.Source == "automatic") return T("source_automatic");
        if (warning.AdminId == Api.ConsoleAdmin.Id) return T("source_console");
        return $"{T("source_moderator")} ({Api.AllAdmins.FirstOrDefault(x => x.Id == warning.AdminId)?.Name ?? warning.IssuedBy?.ToString() ?? "?"})";
    }

    public override void InitializeCommands()
    {
        LoadTranslations();
        Api.RegisterPermission(WarnPermission, "g");
        Api.RegisterPermission(ReviewPermission, "g");
        Api.RegisterPermission(RevokePermission, "z");
        Api.RegisterPermission(SelfPermission, "*");

        Api.AddNewCommand("chatwarn", T("command_warn"), SelfPermission,
            "css_chatwarn <player> <reason>", WarnCommand, CommandUsage.CLIENT_ONLY, minArgs: 2);
        Api.AddNewCommand("warn", T("command_warn"), SelfPermission,
            "css_warn <player> <reason>", WarnCommand, CommandUsage.CLIENT_ONLY, minArgs: 2);
        Api.AddNewCommand("chatwarns", T("command_review"), ReviewPermission,
            "css_chatwarns <player>", ReviewCommand, CommandUsage.CLIENT_ONLY, minArgs: 1);
        Api.AddNewCommand("chatunwarn", T("command_revoke"), SelfPermission,
            "css_chatunwarn <player> <warning_id>", RevokeCommand, CommandUsage.CLIENT_ONLY, minArgs: 2);
        AddCommand("css_mywarns", T("command_mine"), MyWarningsCommand);
        AddCommand("css_warns", T("command_mine"), MyWarningsCommand);

        Api.RegisterChatMenuOption("chat_moderation", () => T("menu_list"),
            (caller, back) => OpenModeratorMenu(caller, back),
            Api.GetCurrentPermissionFlags(ReviewPermission));
    }

    public override void Ready()
    {
        _store = new WarningStore(Api.DbConnectionString);
        _ = Task.Run(async () =>
        {
            try
            {
                await _store.InitializeAsync();
                _storeReady = true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not initialize player warning storage.");
            }
        });
        PrepareRules();
        AddCommandListener("say", OnSay);
        AddCommandListener("say_team", OnSay);
        RegisterListener<Listeners.OnClientDisconnect>(slot =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player?.AuthorizedSteamID is not null)
                _lastMessage.Remove(player.AuthorizedSteamID.SteamId64);
        });
    }

    public override void Unload(bool hotReload)
    {
        Api.UnregisterChatMenuOption("chat_moderation");
        base.Unload(hotReload);
    }

    private void LoadTranslations()
    {
        var coreLanguage = Api.Localizer["Locale.Code"].Value;
        var language = coreLanguage is "en" or "ru" or "ua"
            ? coreLanguage
            : string.Equals(Config.Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ru";
        var path = Path.Combine(ModuleDirectory, "lang", $"{language}.json");
        if (!File.Exists(path)) path = Path.Combine(ModuleDirectory, "lang", "en.json");
        _translations = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new()
            : new();
    }

    private void PrepareRules()
    {
        _regexRules.Clear();
        foreach (var rule in Config.Rules.Where(x => x.Enabled && x.MatchType.Equals("Regex", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var pattern in new[] { rule.Pattern }.Concat(rule.Patterns).Concat(rule.Allowlist)
                         .Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                try
                {
                    var options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
                    if (!rule.CaseSensitive) options |= RegexOptions.IgnoreCase;
                    _regexRules[(pattern, rule.CaseSensitive)] = new Regex(pattern, options,
                        TimeSpan.FromMilliseconds(Math.Clamp(Config.RegexTimeoutMilliseconds, 1, 1000)));
                }
                catch (ArgumentException ex)
                {
                    Logger.LogWarning(ex, "Invalid chat rule regex {RuleId}: {Pattern}.", rule.Id, pattern);
                }
            }
        }
    }

    private HookResult OnSay(CCSPlayerController? player, CommandInfo command)
    {
        if (!Config.Enabled || player is not { IsValid: true, IsBot: false } ||
            player.AuthorizedSteamID is null) return HookResult.Continue;
        var team = command.GetArg(0) == "say_team";
        if (team ? !Config.CheckTeamChat : !Config.CheckPublicChat) return HookResult.Continue;
        var steamId = player.AuthorizedSteamID.SteamId64;
        if (Config.ExemptSteamIds.Contains(steamId) || Config.IgnoreAdministrators && player.Admin() is not null)
            return HookResult.Continue;

        var message = command.GetCommandString;
        var prefixLength = team ? 9 : 4;
        if (message.Length <= prefixLength) return HookResult.Continue;
        message = message[prefixLength..].Trim();
        if (message.Length >= 2 && message[0] == '"' && message[^1] == '"')
            message = message[1..^1];
        if (Config.IgnoreChatCommands && (message.StartsWith('!') || message.StartsWith('/')))
            return HookResult.Continue;
        if (player.GetComms().HasGag() || player.GetComms().HasSilence()) return HookResult.Continue;

        foreach (var rule in Config.Rules)
        {
            if (!rule.Enabled || (team ? !rule.CheckTeamChat : !rule.CheckPublicChat) ||
                rule.Action.Equals("Ignore", StringComparison.OrdinalIgnoreCase)) continue;
            if (message.Length < rule.MinLength || !Matches(rule, message, steamId)) continue;
            var key = $"{steamId}:{rule.Id}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_lastViolation.Count > 10000)
            {
                foreach (var expired in _lastViolation.Where(x => now - x.Value > 3600).Select(x => x.Key).ToArray())
                    _lastViolation.Remove(expired);
            }
            if (_lastViolation.TryGetValue(key, out var previous) && now - previous < Math.Max(0, rule.CooldownSeconds))
                return rule.BlockMessage ? HookResult.Stop : HookResult.Continue;
            _lastViolation[key] = now;
            _lastMessage[steamId] = message;
            HandleAutomaticViolation(player, steamId, message, rule);
            return rule.BlockMessage ? HookResult.Stop : HookResult.Continue;
        }
        _lastMessage[steamId] = message;
        return HookResult.Continue;
    }

    private bool Matches(ChatRule rule, string message, ulong steamId)
    {
        var comparison = rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var matchType = rule.MatchType.ToLowerInvariant();
        if (matchType == "domain") return MatchesDomain(rule, message);
        if (matchType is "contains" or "exact" or "startswith" or "endswith" or "regex")
        {
            var allowed = rule.Allowlist.SelectMany(pattern => MatchRanges(matchType, pattern, message, comparison,
                rule.CaseSensitive)).ToArray();
            return new[] { rule.Pattern }.Concat(rule.Patterns)
                .SelectMany(pattern => MatchRanges(matchType, pattern, message, comparison, rule.CaseSensitive))
                .Any(blocked => !allowed.Any(x => x.Start <= blocked.Start && x.End >= blocked.End));
        }
        switch (matchType)
        {
            case "length": return rule.Threshold > 0 && message.Length >= rule.Threshold;
            case "duplicate": return _lastMessage.TryGetValue(steamId, out var previous) && message.Equals(previous, comparison);
            case "repeat":
                var count = 1;
                for (var i = 1; i < message.Length; i++)
                {
                    count = message[i] == message[i - 1] ? count + 1 : 1;
                    if (count >= Math.Max(2, rule.Threshold)) return true;
                }
                return false;
            case "caps":
                var letters = message.Count(char.IsLetter);
                return letters >= Math.Max(1, rule.MinLength) &&
                    letters > 0 && message.Count(char.IsUpper) * 100 / letters >= Math.Clamp(rule.Threshold, 1, 100);
            default: return false;
        }
    }

    private IEnumerable<(int Start, int End)> MatchRanges(string type, string pattern, string message,
        StringComparison comparison, bool caseSensitive)
    {
        if (string.IsNullOrWhiteSpace(pattern)) yield break;
        switch (type)
        {
            case "contains":
                for (var start = 0; start < message.Length;)
                {
                    var index = message.IndexOf(pattern, start, comparison);
                    if (index < 0) break;
                    yield return (index, index + pattern.Length);
                    start = index + 1;
                }
                break;
            case "exact":
                if (message.Equals(pattern, comparison)) yield return (0, message.Length);
                break;
            case "startswith":
                if (message.StartsWith(pattern, comparison)) yield return (0, pattern.Length);
                break;
            case "endswith":
                if (message.EndsWith(pattern, comparison)) yield return (message.Length - pattern.Length, message.Length);
                break;
            case "regex":
                if (_regexRules.TryGetValue((pattern, caseSensitive), out var regex))
                {
                    Match[] matches;
                    try { matches = regex.Matches(message).Cast<Match>().ToArray(); }
                    catch (RegexMatchTimeoutException) { break; }
                    for (var i = 0; i < matches.Length; i++)
                        yield return (matches[i].Index, matches[i].Index + matches[i].Length);
                }
                break;
        }
    }

    private static bool MatchesDomain(ChatRule rule, string message)
    {
        try
        {
            foreach (Match link in LinkRegex.Matches(message))
            {
                var url = link.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']');
                if (!Uri.TryCreate(url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + url : url,
                        UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) continue;
                var host = uri.IdnHost;
                if (rule.Allowlist.Any(domain => DomainMatches(host, domain)) ||
                    rule.AllowedUrls.Any(allowed => UrlMatches(uri, allowed))) continue;
                if (DomainMatches(host, rule.Pattern) || rule.Patterns.Any(domain => DomainMatches(host, domain)))
                    return true;
            }
        }
        catch (RegexMatchTimeoutException) { }
        return false;
    }

    private static bool DomainMatches(string host, string pattern)
    {
        pattern = pattern.Trim().TrimEnd('.');
        if (pattern == "*") return true;
        if (pattern.Length == 0 || pattern.Contains('/') || pattern.Contains(':')) return false;
        return host.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith("." + pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool UrlMatches(Uri link, string allowed)
    {
        if (!Uri.TryCreate(allowed.Contains("://", StringComparison.Ordinal) ? allowed : "https://" + allowed,
                UriKind.Absolute, out var expected) || expected.Scheme is not ("http" or "https")) return false;
        return link.IdnHost.Equals(expected.IdnHost, StringComparison.OrdinalIgnoreCase) &&
               link.AbsolutePath.Equals(expected.AbsolutePath, StringComparison.Ordinal);
    }

    private void HandleAutomaticViolation(CCSPlayerController player, ulong steamId, string message, ChatRule rule)
    {
        var reason = Reason(rule);
        player.PrintToChat(T("rule_violated", reason));
        var warning = new PlayerWarning
        {
            SteamId = steamId,
            PlayerName = player.PlayerName,
            Reason = rule.Reason,
            Source = "automatic",
            RuleId = rule.Id[..Math.Min(rule.Id.Length, 64)],
            Message = message[..Math.Min(message.Length, Math.Clamp(Config.MaxStoredMessageLength, 0, 500))],
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            IssuedImmunity = Api.ConsoleAdmin?.CurrentImmunity
        };
        if (rule.AddWarning && _storeReady &&
            !rule.Action.Equals("Mute", StringComparison.OrdinalIgnoreCase) &&
            !rule.Action.Equals("Ban", StringComparison.OrdinalIgnoreCase))
            _ = SaveWarningAsync(warning, null, null);
        if (Api.ThisServer is null) return;
        var action = rule.Action.ToLowerInvariant();
        if ((action == "gag" && rule.GagMinutes >= 0) || (action == "mute" && rule.MuteMinutes >= 0))
        {
            var comm = new PlayerComm(new PlayerInfo(player),
                action == "mute" ? PlayerComm.MuteTypes.MuteAll : PlayerComm.MuteTypes.MuteChat,
                reason, action == "mute" ? rule.MuteMinutes : rule.GagMinutes, Api.ThisServer.Id)
                { AdminId = Api.ConsoleAdmin.Id };
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await Api.AddComm(comm);
                    if (result.QueryStatus != 0)
                        Logger.LogWarning("Automatic chat mute failed for rule {RuleId}: {Status}.", rule.Id, result.QueryStatus);
                }
                catch (Exception ex) { Logger.LogError(ex, "Could not apply automatic chat mute."); }
            });
        }
        else if (action == "ban" && rule.BanMinutes >= 0)
        {
            var ban = new PlayerBan(new PlayerInfo(player), reason[..Math.Min(reason.Length, 250)],
                rule.BanMinutes, Api.ThisServer.Id) { AdminId = Api.ConsoleAdmin.Id };
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await Api.AddBan(ban);
                    if (result.QueryStatus != 0)
                        Logger.LogWarning("Automatic chat ban failed for rule {RuleId}: {Status}.", rule.Id, result.QueryStatus);
                }
                catch (Exception ex) { Logger.LogError(ex, "Could not apply automatic chat ban."); }
            });
        }
    }

    private async Task SaveWarningAsync(PlayerWarning warning, CCSPlayerController? moderator, PlayerInfo? target)
    {
        // Database operations may yield; player and menu updates must return to the game thread.
        try
        {
            warning.AdminId = moderator?.Admin()?.Id ?? Api.ConsoleAdmin?.Id ?? 0;
            warning.Id = await _store!.AddAsync(warning);
            Server.NextFrame(() => PublishWarningEvent("chat_warning_created", warning));
            var active = await _store.ActiveAsync(warning.SteamId);
            if (Api.AllAdmins.Any(x => x.USteamId == warning.SteamId))
                await Api.ReloadDataFromDb();
            var countable = active.Where(x => !x.Reason.Trim().Equals("test", StringComparison.OrdinalIgnoreCase)).ToList();
            Server.NextFrame(() =>
            {
                var recipient = PlayersUtils.GetControllerBySteamId(warning.SteamId);
                var displayedReason = _translations.GetValueOrDefault(warning.Reason, warning.Reason);
                if (recipient is { IsValid: true })
                    Api.Notify(recipient, T("menu_main"), Config.BanSuggestionThreshold > 0
                        ? T("warn_received", countable.Count, Config.BanSuggestionThreshold, displayedReason)
                        : T("warn_received_no_threshold", countable.Count, displayedReason), AdminNotice.Warning);
                if (moderator is { IsValid: true })
                {
                    Api.Notify(moderator, T("menu_main"), T("warn_saved", countable.Count));
                    if (Config.BanSuggestionThreshold > 0 && countable.Count >= Config.BanSuggestionThreshold && target is not null)
                        ShowBanPrompt(moderator, target, countable);
                }
                else if (Config.NotifyModeratorsAtThreshold && Config.BanSuggestionThreshold > 0 &&
                    countable.Count == Config.BanSuggestionThreshold)
                {
                    foreach (var admin in PlayersUtils.GetOnlinePlayers().Where(x => x.HasPermissions(ReviewPermission)))
                        Api.Notify(admin, T("menu_main"), T("threshold_notice", warning.PlayerName, countable.Count),
                            AdminNotice.Warning);
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not save player warning.");
            if (moderator is not null) Server.NextFrame(() => Api.Notify(moderator, T("menu_main"),
                T("storage_error"), AdminNotice.Error));
        }
    }

    private static CCSPlayerController? FindPlayer(string search)
    {
        var players = PlayersUtils.GetOnlinePlayers();
        if (search.StartsWith('#') && uint.TryParse(search[1..], out var userId))
            return players.FirstOrDefault(x => x.UserId == userId);
        return players.FirstOrDefault(x => x.AuthorizedSteamID!.SteamId64.ToString() == search) ??
               players.FirstOrDefault(x => x.PlayerName.Equals(search, StringComparison.OrdinalIgnoreCase)) ??
               players.FirstOrDefault(x => x.PlayerName.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private void WarnCommand(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        if (caller is null || !_storeReady) return;
        var target = FindPlayer(args[0]);
        if (target is null)
        {
            caller.PrintToChat(T("target_unavailable"));
            return;
        }
        IssueManualWarning(caller, target, string.Join(' ', args.Skip(1)));
    }

    private bool IssueManualWarning(CCSPlayerController caller, CCSPlayerController target, string reason)
    {
        if (!target.IsValid || target.AuthorizedSteamID is null ||
            !CanWarn(caller, target, reason))
        {
            caller.PrintToChat(T("target_unavailable"));
            return false;
        }
        reason = reason.Trim();
        if (reason.Length is < 3 or > 255)
        {
            caller.PrintToChat(T("invalid_reason"));
            return false;
        }
        var info = new PlayerInfo(target);
        var warning = new PlayerWarning
        {
            SteamId = target.AuthorizedSteamID!.SteamId64,
            PlayerName = target.PlayerName,
            Reason = reason,
            Source = "moderator",
            IssuedBy = caller.AuthorizedSteamID!.SteamId64,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            IssuedImmunity = caller.Admin()?.CurrentImmunity
        };
        _ = SaveWarningAsync(warning, caller, info);
        return true;
    }

    private void ReviewCommand(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        if (caller is null) return;
        var target = FindPlayer(args[0]);
        if (target is null)
        {
            caller.PrintToChat(T("target_unavailable"));
            return;
        }
        OpenWarnings(caller, target.AuthorizedSteamID!.SteamId64, target.PlayerName);
    }

    private void MyWarningsCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller?.AuthorizedSteamID is null) return;
        OpenWarnings(caller, caller.AuthorizedSteamID.SteamId64, caller.PlayerName, ownOnly: true);
    }

    private void RevokeCommand(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        if (caller is null || !_storeReady) return;
        var target = FindPlayer(args[0]);
        if (target is null || !long.TryParse(args[1], out var id))
        {
            caller.PrintToChat(T("target_unavailable"));
            return;
        }
        RevokeWarning(caller, target.AuthorizedSteamID!.SteamId64, target.PlayerName, id);
    }

    private bool CanWarn(CCSPlayerController caller, CCSPlayerController target, string reason = "")
    {
        var issuer = caller.Admin();
        var recipient = target.Admin();
        if (issuer is null) return false;
        if (issuer.Id == recipient?.Id)
            return reason.Trim().Equals("test", StringComparison.OrdinalIgnoreCase);
        return caller.HasPermissions(WarnPermission) &&
               (recipient is null || issuer.CurrentImmunity >= recipient.CurrentImmunity);
    }

    private bool CanRevoke(CCSPlayerController caller, PlayerWarning warning)
    {
        var moderator = caller.Admin();
        if (moderator is null) return false;
        if (warning.Reason.Trim().Equals("test", StringComparison.OrdinalIgnoreCase)) return true;
        if (warning.AdminId == moderator.Id) return true;
        if (!caller.HasPermissions(warning.IsAdminWarning ? "admins_manage.warn_delete" : RevokePermission))
            return false;
        if (!warning.IsAdminWarning && warning.AdminId == Api.ConsoleAdmin.Id &&
            !moderator.HasPermissions("blocks_manage.remove_console")) return false;
        var issuerImmunity = warning.IssuedImmunity ?? Api.AllAdmins.FirstOrDefault(x => x.Id == warning.AdminId)?.CurrentImmunity;
        return issuerImmunity is not null && moderator.CurrentImmunity >= issuerImmunity;
    }

    private void ApplyRevoke(CCSPlayerController caller, ulong steamId, string name, PlayerWarning warning,
        IDynamicMenu? historyBackMenu = null, bool ownOnly = false)
    {
        var actor = caller.Admin()!;
        var moderatorSteamId = caller.AuthorizedSteamID!.SteamId64;
        var moderatorId = actor.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                bool removed;
                if (warning.IsAdminWarning)
                {
                    var coreWarn = (await Api.GetAllWarns()).FirstOrDefault(x => x.Id == warning.Id && x.DeletedAt is null);
                    removed = coreWarn is not null &&
                        (await Api.DeleteWarn(actor, coreWarn)).QueryStatus == 0;
                }
                else
                    removed = await _store!.RevokeAsync(warning.Id, steamId, warning.AdminId,
                        moderatorId, moderatorSteamId);
                if (removed && !warning.IsAdminWarning)
                    Server.NextFrame(() => PublishWarningEvent("chat_warning_removed", warning, actor));
                if (removed && !warning.IsAdminWarning && Api.AllAdmins.Any(x => x.USteamId == steamId))
                    await Api.ReloadDataFromDb();
                Server.NextFrame(() =>
                {
                    if (!caller.IsValid) return;
                    Api.Notify(caller, T("menu_main"), T(removed ? "warn_revoked" : "warn_not_found"),
                        removed ? AdminNotice.Success : AdminNotice.Warning);
                    OpenWarnings(caller, steamId, name, ownOnly, historyBackMenu);
                });
            }
            catch (Exception ex) { Logger.LogError(ex, "Could not revoke player warning."); }
        });
    }

    private static void PublishWarningEvent(string key, PlayerWarning warning, Admin? actor = null)
    {
        var data = new EventData(key);
        data.Insert("id", warning.Id);
        data.Insert("steam_id", warning.SteamId);
        data.Insert("player_name", warning.PlayerName);
        data.Insert("reason", warning.Reason);
        data.Insert("source", warning.Source);
        data.Insert("issuer_id", warning.AdminId);
        data.Insert("issued_at", warning.CreatedAt);
        data.Insert("message", warning.Message);
        if (actor is not null) data.Insert("actor", actor);
        Api.InvokeDynamicEvent(data);
    }

    private void OpenWarnings(CCSPlayerController caller, ulong steamId, string name, bool ownOnly = false,
        IDynamicMenu? backMenu = null)
    {
        if (!_storeReady) { caller.PrintToChat(T("storage_unavailable")); return; }
        _ = Task.Run(async () =>
        {
            try
            {
                var warnings = ownOnly ? await _store!.ActiveAsync(steamId) :
                    await _store!.ListAsync(steamId, Config.HistoryLimit);
                Server.NextFrame(() =>
                {
                    if (!caller.IsValid) return;
                    var menu = Api.CreateMenu("chat_moderation:history", T("menu_history", name), backMenu: backMenu);
                    if (warnings.Count == 0)
                        menu.AddMenuOption("none", T("no_warnings"), (_, _) => { }, disabled: true);
                    foreach (var warning in warnings)
                    {
                        var source = Source(warning);
                        var status = warning.RevokedAt is null ? "" : T("revoked_suffix");
                        var reason = _translations.GetValueOrDefault(warning.Reason, warning.Reason);
                        menu.AddMenuOption(warning.Id.ToString(), $"#{warning.Id} {reason} ({source}){status}",
                            (_, _) => OpenWarningDetails(caller, steamId, name, warning, menu, backMenu, ownOnly));
                    }
                    menu.Open(caller);
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not load player warnings.");
                Server.NextFrame(() => caller.PrintToChat(T("storage_error")));
            }
        });
    }

    private void OpenWarningDetails(CCSPlayerController caller, ulong steamId, string name,
        PlayerWarning warning, IDynamicMenu historyMenu, IDynamicMenu? historyBackMenu, bool ownOnly)
    {
        var reason = _translations.GetValueOrDefault(warning.Reason, warning.Reason);
        var source = Source(warning);
        caller.PrintToChat(T("warning_details", warning.Id, reason, source,
            DateTimeOffset.FromUnixTimeSeconds(warning.CreatedAt).ToString("g")));
        if (!string.IsNullOrEmpty(warning.Message))
            caller.PrintToChat(T("warning_message", warning.Message));
        if (warning.RevokedAt is not null)
            caller.PrintToChat(T("warning_revoked_at",
                DateTimeOffset.FromUnixTimeSeconds(warning.RevokedAt.Value).ToString("g")));
        var menu = Api.CreateMenu("chat_moderation:warning_details", T("menu_warning", warning.Id),
            backMenu: historyMenu);
        if (warning.RevokedAt is null && CanRevoke(caller, warning))
            menu.AddMenuOption("revoke", T("menu_revoke", warning.Id), (_, _) =>
                RevokeWarning(caller, steamId, name, warning.Id, historyBackMenu, ownOnly));
        menu.AddMenuOption("reason", T("menu_reason", reason), (_, _) => { }, disabled: true);
        menu.AddMenuOption("source", T("menu_source", source), (_, _) => { }, disabled: true);
        menu.AddMenuOption("issued", T("menu_issued",
            DateTimeOffset.FromUnixTimeSeconds(warning.CreatedAt).ToString("g")), (_, _) => { }, disabled: true);
        if (warning.RevokedAt is not null)
            menu.AddMenuOption("revoked", T("warning_revoked_at",
                DateTimeOffset.FromUnixTimeSeconds(warning.RevokedAt.Value).ToString("g")), (_, _) => { }, disabled: true);
        menu.Open(caller);
    }

    private void RevokeWarning(CCSPlayerController caller, ulong steamId, string name, long warningId,
        IDynamicMenu? historyBackMenu = null, bool ownOnly = false)
    {
        if (caller.AuthorizedSteamID is null || caller.Admin() is null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                var warning = await _store!.GetAsync(warningId, steamId);
                Server.NextFrame(() =>
                {
                    if (!caller.IsValid) return;
                    if (warning is null || !CanRevoke(caller, warning))
                    {
                        Api.Notify(caller, T("menu_main"), T("warn_not_found"), AdminNotice.Warning);
                        return;
                    }
                    ApplyRevoke(caller, steamId, name, warning, historyBackMenu, ownOnly);
                });
            }
            catch (Exception ex) { Logger.LogError(ex, "Could not revoke player warning."); }
        });
    }

    private void OpenModeratorMenu(CCSPlayerController caller, IDynamicMenu? backMenu = null)
    {
        if (!caller.HasPermissions(ReviewPermission)) return;
        if (!_storeReady) { caller.PrintToChat(T("storage_unavailable")); return; }
        _ = Task.Run(async () =>
        {
            try
            {
                var targets = await _store!.ListTargetsAsync();
                Server.NextFrame(() =>
                {
                    if (!caller.IsValid || !caller.HasPermissions(ReviewPermission)) return;
                    var menu = Api.CreateMenu("chat_moderation:players", T("menu_list"), backMenu: backMenu);
                    menu.AddMenuOption("issue", T("menu_issue"), (_, _) => OpenIssueMenu(caller, menu),
                        viewFlags: Api.GetCurrentPermissionFlags(WarnPermission));
                    foreach (var target in targets)
                        menu.AddMenuOption(target.SteamId.ToString(),
                            $"{target.PlayerName} ({target.WarningCount})", (_, _) =>
                                OpenWarnings(caller, target.SteamId, target.PlayerName, backMenu: menu));
                    menu.Open(caller);
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not load warning list.");
                Server.NextFrame(() => { if (caller.IsValid) caller.PrintToChat(T("storage_error")); });
            }
        });
    }

    private void OpenIssueMenu(CCSPlayerController caller, IDynamicMenu? backMenu = null)
    {
        if (!caller.HasPermissions(WarnPermission)) return;
        var players = PlayersUtils.GetOnlinePlayers()
            .Where(player => player.AuthorizedSteamID is not null)
            .OrderByDescending(player => player.Admin() is not null)
            .ThenBy(player => player.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var steamIds = players.Select(player => player.AuthorizedSteamID!.SteamId64).ToArray();
        _ = Task.Run(async () =>
        {
            try
            {
                var counts = await _store!.ActiveCountsAsync(steamIds);
                Server.NextFrame(() =>
                {
                    if (!caller.IsValid) return;
                    var menu = Api.CreateMenu("chat_moderation:issue", T("menu_issue"), backMenu: backMenu);
                    foreach (var player in players.Where(p => p.IsValid))
                    {
                        var steamId = player.AuthorizedSteamID?.SteamId64;
                        if (steamId is null) continue;
                        var admin = player.Admin();
                        var group = admin?.Group?.Name ?? T("group_custom");
                        var label = player.PlayerName + (admin is null ? "" : $" [{group}]") +
                            $" ({counts.GetValueOrDefault(steamId.Value)})";
                        menu.AddMenuOption(player.GetSteamId(), label,
                            (_, _) => OpenTargetMenu(caller, player, menu));
                    }
                    menu.Open(caller);
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not load warning counts.");
                Server.NextFrame(() => { if (caller.IsValid) caller.PrintToChat(T("storage_error")); });
            }
        });
    }

    private void OpenTargetMenu(CCSPlayerController caller, CCSPlayerController target, IDynamicMenu backMenu)
    {
        if (!target.IsValid) return;
        var menu = Api.CreateMenu("chat_moderation:target", target.PlayerName, backMenu: backMenu);
        menu.AddMenuOption("history", T("menu_view"), (_, _) =>
            OpenWarnings(caller, target.AuthorizedSteamID!.SteamId64, target.PlayerName, backMenu: menu));
        menu.AddMenuOption("warn", T("menu_issue"), (_, _) =>
            OpenSelectWarningReasonMenu(caller, target, menu),
            disabled: !CanWarn(caller, target) && caller.Admin()?.Id != target.Admin()?.Id,
            viewFlags: Api.GetCurrentPermissionFlags(WarnPermission));
        menu.Open(caller);
    }

    private void OpenSelectWarningReasonMenu(CCSPlayerController caller, CCSPlayerController target,
        IDynamicMenu backMenu)
    {
        if (!target.IsValid || caller.Admin() is null) return;
        var menu = Api.CreateMenu("chat_moderation:reasons", T("menu_select_reason"), backMenu: backMenu);
        menu.AddMenuOption("own_reason", T("menu_own_reason"), (_, _) =>
        {
            caller.PrintToChat(T("enter_reason"));
            Api.HookNextPlayerMessage(caller, reason =>
            {
                if (IssueManualWarning(caller, target, reason)) Api.CloseMenu(caller);
            });
        });
        foreach (var reason in Config.WarningReasons.Where(x => !x.HideFromMenu &&
                     !string.IsNullOrWhiteSpace(x.Title) && !string.IsNullOrWhiteSpace(x.Text)))
        {
            if (!CanWarn(caller, target, reason.Text)) continue;
            menu.AddMenuOption(reason.Title, _translations.GetValueOrDefault(reason.Title, reason.Title),
                (_, _) =>
                {
                    if (IssueManualWarning(caller, target, reason.Text)) Api.CloseMenu(caller);
                });
        }
        menu.Open(caller);
    }

    private void ShowBanPrompt(CCSPlayerController caller, PlayerInfo target, List<PlayerWarning> warnings)
    {
        if (!caller.IsValid) return;
        var menu = Api.CreateMenu("chat_moderation:ban_offer", T("menu_ban_offer", warnings.Count),
            backAction: player => OpenWarnings(player, warnings[0].SteamId, target.PlayerName));
        foreach (var warning in warnings)
        {
            var source = Source(warning);
            var localizedReason = _translations.GetValueOrDefault(warning.Reason, warning.Reason);
            menu.AddMenuOption($"reason_{warning.Id}", $"{localizedReason} ({source})", (_, _) =>
                caller.PrintToChat(T("warning_details", warning.Id, localizedReason, source,
                    DateTimeOffset.FromUnixTimeSeconds(warning.CreatedAt).ToString("g"))));
        }
        if (caller.HasPermissions("blocks_manage.ban") && Api.ThisServer is not null &&
            Config.SuggestedBanMinutes >= 0)
            menu.AddMenuOption("confirm_ban", T("menu_confirm_ban", Config.SuggestedBanMinutes), (_, _) =>
            {
                if (Api.ThisServer is null || !Api.CanDoActionWithPlayer(caller.GetSteamId(), target.SteamId!)) return;
                var summary = string.Join("; ", warnings.Take(5).Select(x =>
                    _translations.GetValueOrDefault(x.Reason, x.Reason)));
                var reason = T("ban_reason_prefix") + summary;
                if (reason.Length > 250) reason = reason[..250];
                var ban = new PlayerBan(target, reason, Config.SuggestedBanMinutes, Api.ThisServer.Id)
                { AdminId = caller.Admin()!.Id };
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await Api.AddBan(ban);
                        Server.NextFrame(() => caller.PrintToChat(T(result.QueryStatus == 0 ? "ban_applied" : "ban_failed")));
                    }
                    catch (Exception ex) { Logger.LogError(ex, "Could not apply suggested ban."); }
                });
            });
        menu.AddMenuOption("keep_warnings", T("menu_keep_warnings"), (_, _) =>
            OpenWarnings(caller, warnings[0].SteamId, target.PlayerName));
        menu.Open(caller);
    }
}
