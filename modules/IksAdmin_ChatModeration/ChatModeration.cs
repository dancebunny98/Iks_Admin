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
    private WarningStore? _store;
    private volatile bool _storeReady;
    private readonly Dictionary<string, long> _lastViolation = new();
    private readonly Dictionary<ulong, string> _lastMessage = new();
    private readonly Dictionary<string, Regex> _regexRules = new();
    private Dictionary<string, string> _translations = new();

    public ChatModerationConfig Config { get; set; } = new();
    public void OnConfigParsed(ChatModerationConfig config) => Config = config;

    private string T(string key, params object[] args) =>
        string.Format(_translations.GetValueOrDefault(key, key), args);

    private string Reason(ChatRule rule) =>
        _translations.GetValueOrDefault(rule.Reason, rule.Reason);

    private string Source(PlayerWarning warning) => warning.Source == "automatic"
        ? T("source_automatic")
        : $"{T("source_moderator")} ({warning.IssuedBy})";

    public override void InitializeCommands()
    {
        LoadTranslations();
        Api.RegisterPermission(WarnPermission, "g");
        Api.RegisterPermission(ReviewPermission, "g");
        Api.RegisterPermission(RevokePermission, "z");

        Api.AddNewCommand("chatwarn", T("command_warn"), WarnPermission,
            "css_chatwarn <player> <reason>", WarnCommand, CommandUsage.CLIENT_ONLY, minArgs: 2);
        Api.AddNewCommand("chatwarns", T("command_review"), ReviewPermission,
            "css_chatwarns <player>", ReviewCommand, CommandUsage.CLIENT_ONLY, minArgs: 1);
        Api.AddNewCommand("warns", T("command_review"), ReviewPermission,
            "css_warns [player]", WarnsCommand, CommandUsage.CLIENT_ONLY, minArgs: 0);
        Api.AddNewCommand("chatunwarn", T("command_revoke"), RevokePermission,
            "css_chatunwarn <player> <warning_id>", RevokeCommand, CommandUsage.CLIENT_ONLY, minArgs: 2);
        AddCommand("css_mywarns", T("command_mine"), MyWarningsCommand);

        Api.RegisterMainMenuOption("chat_moderation", () => T("menu_main"),
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
        Api.UnregisterMainMenuOption("chat_moderation");
        base.Unload(hotReload);
    }

    private void LoadTranslations()
    {
        var language = string.Equals(Config.Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ru";
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
            if (string.IsNullOrWhiteSpace(rule.Id) || string.IsNullOrWhiteSpace(rule.Pattern)) continue;
            try
            {
                var options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
                if (!rule.CaseSensitive) options |= RegexOptions.IgnoreCase;
                _regexRules[rule.Id] = new Regex(rule.Pattern, options,
                    TimeSpan.FromMilliseconds(Math.Clamp(Config.RegexTimeoutMilliseconds, 1, 1000)));
            }
            catch (ArgumentException ex)
            {
                Logger.LogWarning(ex, "Invalid chat rule regex {RuleId}.", rule.Id);
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
        switch (rule.MatchType.ToLowerInvariant())
        {
            case "contains": return rule.Pattern.Length > 0 && message.Contains(rule.Pattern, comparison);
            case "exact": return rule.Pattern.Length > 0 && message.Equals(rule.Pattern, comparison);
            case "startswith": return rule.Pattern.Length > 0 && message.StartsWith(rule.Pattern, comparison);
            case "endswith": return rule.Pattern.Length > 0 && message.EndsWith(rule.Pattern, comparison);
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
            case "regex":
                try { return _regexRules.TryGetValue(rule.Id, out var regex) && regex.IsMatch(message); }
                catch (RegexMatchTimeoutException) { return false; }
            default: return false;
        }
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
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        if (rule.AddWarning && _storeReady)
            _ = SaveWarningAsync(warning, null, null);
        if (rule.Action.Equals("Gag", StringComparison.OrdinalIgnoreCase) && rule.GagMinutes >= 0 &&
            Api.ThisServer is not null)
        {
            var comm = new PlayerComm(new PlayerInfo(player), PlayerComm.MuteTypes.MuteChat,
                reason, rule.GagMinutes, Api.ThisServer.Id) { AdminId = Api.ConsoleAdmin.Id };
            _ = Task.Run(async () =>
            {
                try { await Api.AddComm(comm); }
                catch (Exception ex) { Logger.LogError(ex, "Could not apply automatic gag."); }
            });
        }
    }

    private async Task SaveWarningAsync(PlayerWarning warning, CCSPlayerController? moderator, PlayerInfo? target)
    {
        // Database operations may yield; player and menu updates must return to the game thread.
        try
        {
            warning.AdminId = moderator?.Admin()?.Id ?? Api.ConsoleAdmin?.Id ?? 0;
            await _store!.AddAsync(warning);
            var active = await _store.ActiveAsync(warning.SteamId, Config.ActiveWarnDays);
            Server.NextFrame(() =>
            {
                var recipient = PlayersUtils.GetControllerBySteamId(warning.SteamId);
                var displayedReason = _translations.GetValueOrDefault(warning.Reason, warning.Reason);
                if (recipient is { IsValid: true })
                    Api.Notify(recipient, T("menu_main"), Config.BanSuggestionThreshold > 0
                        ? T("warn_received", active.Count, Config.BanSuggestionThreshold, displayedReason)
                        : T("warn_received_no_threshold", active.Count, displayedReason), AdminNotice.Warning);
                if (moderator is { IsValid: true })
                {
                    Api.Notify(moderator, T("menu_main"), T("warn_saved", active.Count));
                    if (Config.BanSuggestionThreshold > 0 && active.Count >= Config.BanSuggestionThreshold && target is not null)
                        ShowBanPrompt(moderator, target, active);
                }
                else if (Config.NotifyModeratorsAtThreshold && Config.BanSuggestionThreshold > 0 &&
                    active.Count == Config.BanSuggestionThreshold)
                {
                    foreach (var admin in PlayersUtils.GetOnlinePlayers().Where(x => x.HasPermissions(ReviewPermission)))
                        Api.Notify(admin, T("menu_main"), T("threshold_notice", warning.PlayerName, active.Count),
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
        if (target is null || !Api.CanDoActionWithPlayer(caller.GetSteamId(), target.GetSteamId()))
        {
            caller.PrintToChat(T("target_unavailable"));
            return;
        }
        IssueManualWarning(caller, target, string.Join(' ', args.Skip(1)));
    }

    private void IssueManualWarning(CCSPlayerController caller, CCSPlayerController target, string reason)
    {
        if (!target.IsValid || target.AuthorizedSteamID is null ||
            !Api.CanDoActionWithPlayer(caller.GetSteamId(), target.GetSteamId()))
        {
            caller.PrintToChat(T("target_unavailable"));
            return;
        }
        reason = reason.Trim();
        if (reason.Length is < 3 or > 255)
        {
            caller.PrintToChat(T("invalid_reason"));
            return;
        }
        var info = new PlayerInfo(target);
        var warning = new PlayerWarning
        {
            SteamId = target.AuthorizedSteamID!.SteamId64,
            PlayerName = target.PlayerName,
            Reason = reason,
            Source = "moderator",
            IssuedBy = caller.AuthorizedSteamID!.SteamId64,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        _ = SaveWarningAsync(warning, caller, info);
    }

    private void ReviewCommand(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        if (caller is null) return;
        var target = FindPlayer(args[0]);
        if (target is null || !Api.CanDoActionWithPlayer(caller.GetSteamId(), target.GetSteamId()))
        {
            caller.PrintToChat(T("target_unavailable"));
            return;
        }
        OpenWarnings(caller, target.AuthorizedSteamID!.SteamId64, target.PlayerName);
    }

    private void WarnsCommand(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        if (caller is null) return;
        if (args.Count == 0)
        {
            OpenModeratorMenu(caller);
            return;
        }
        ReviewCommand(caller, args, info);
    }

    private void MyWarningsCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller?.AuthorizedSteamID is null) return;
        OpenWarnings(caller, caller.AuthorizedSteamID.SteamId64, caller.PlayerName);
    }

    private void RevokeCommand(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        if (caller is null || !_storeReady) return;
        var target = FindPlayer(args[0]);
        if (target is null || !Api.CanDoActionWithPlayer(caller.GetSteamId(), target.GetSteamId()) ||
            !long.TryParse(args[1], out var id))
        {
            caller.PrintToChat(T("target_unavailable"));
            return;
        }
        var targetSteamId = target.AuthorizedSteamID!.SteamId64;
        var moderatorSteamId = caller.AuthorizedSteamID!.SteamId64;
        var moderatorId = caller.Admin()!.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                var removed = await _store!.RevokeAsync(id, targetSteamId, moderatorId, moderatorSteamId);
                Server.NextFrame(() => { if (caller.IsValid) Api.Notify(caller, T("menu_main"),
                    T(removed ? "warn_revoked" : "warn_not_found"),
                    removed ? AdminNotice.Success : AdminNotice.Warning); });
            }
            catch (Exception ex) { Logger.LogError(ex, "Could not revoke player warning."); }
        });
    }

    private void OpenWarnings(CCSPlayerController caller, ulong steamId, string name)
    {
        if (!_storeReady) { caller.PrintToChat(T("storage_unavailable")); return; }
        _ = Task.Run(async () =>
        {
            try
            {
                var warnings = await _store!.ListAsync(steamId, Config.HistoryLimit);
                Server.NextFrame(() =>
                {
                    if (!caller.IsValid) return;
                    var menu = Api.CreateMenu("chat_moderation:history", T("menu_history", name));
                    if (warnings.Count == 0)
                        menu.AddMenuOption("none", T("no_warnings"), (_, _) => { }, disabled: true);
                    foreach (var warning in warnings)
                    {
                        var source = Source(warning);
                        var status = warning.RevokedAt is null ? "" : T("revoked_suffix");
                        var reason = _translations.GetValueOrDefault(warning.Reason, warning.Reason);
                        menu.AddMenuOption(warning.Id.ToString(), $"#{warning.Id} {reason} ({source}){status}",
                            (_, _) => caller.PrintToChat(T("warning_details", warning.Id, reason, source,
                                DateTimeOffset.FromUnixTimeSeconds(warning.CreatedAt).ToString("g"))));
                        if (warning.RevokedAt is null && steamId != caller.AuthorizedSteamID?.SteamId64 &&
                            caller.HasPermissions(RevokePermission) &&
                            Api.CanDoActionWithPlayer(caller.GetSteamId(), steamId.ToString()))
                            menu.AddMenuOption($"revoke_{warning.Id}", T("menu_revoke", warning.Id), (_, _) =>
                                RevokeWarning(caller, steamId, name, warning.Id));
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

    private void RevokeWarning(CCSPlayerController caller, ulong steamId, string name, long warningId)
    {
        if (caller.AuthorizedSteamID is null || caller.Admin() is null ||
            !Api.CanDoActionWithPlayer(caller.GetSteamId(), steamId.ToString())) return;
        var issuerId = caller.Admin()!.Id;
        var issuerSteamId = caller.AuthorizedSteamID.SteamId64;
        _ = Task.Run(async () =>
        {
            try
            {
                var removed = await _store!.RevokeAsync(warningId, steamId, issuerId, issuerSteamId);
                Server.NextFrame(() =>
                {
                    if (!caller.IsValid) return;
                    Api.Notify(caller, T("menu_main"), T(removed ? "warn_revoked" : "warn_not_found"),
                        removed ? AdminNotice.Success : AdminNotice.Warning);
                    OpenWarnings(caller, steamId, name);
                });
            }
            catch (Exception ex) { Logger.LogError(ex, "Could not revoke player warning."); }
        });
    }

    private void OpenModeratorMenu(CCSPlayerController caller, IDynamicMenu? backMenu = null)
    {
        if (!_storeReady) { caller.PrintToChat(T("storage_unavailable")); return; }
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
                var counts = await _store!.ActiveCountsAsync(steamIds, Config.ActiveWarnDays);
                Server.NextFrame(() =>
                {
                    if (!caller.IsValid) return;
                    var menu = Api.CreateMenu("chat_moderation:players", T("menu_main"), backMenu: backMenu);
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
            OpenWarnings(caller, target.AuthorizedSteamID!.SteamId64, target.PlayerName));
        menu.AddMenuOption("warn", T("menu_issue"), (_, _) =>
        {
            caller.PrintToChat(T("enter_reason"));
            Api.HookNextPlayerMessage(caller, reason => IssueManualWarning(caller, target, reason));
        }, disabled: !Api.CanDoActionWithPlayer(caller.GetSteamId(), target.GetSteamId()),
            viewFlags: Api.GetCurrentPermissionFlags(WarnPermission));
        menu.Open(caller);
    }

    private void ShowBanPrompt(CCSPlayerController caller, PlayerInfo target, List<PlayerWarning> warnings)
    {
        if (!caller.IsValid) return;
        var menu = Api.CreateMenu("chat_moderation:ban_offer", T("menu_ban_offer", warnings.Count));
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
        menu.AddMenuOption("keep_warnings", T("menu_keep_warnings"), (_, _) => Api.CloseMenu(caller));
        menu.Open(caller);
    }
}
