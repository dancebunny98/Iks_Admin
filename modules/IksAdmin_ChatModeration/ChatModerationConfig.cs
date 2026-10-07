using CounterStrikeSharp.API.Core;

namespace IksAdmin_ChatModeration;

public sealed class ChatModerationConfig : BasePluginConfig
{
    public override int Version { get; set; } = 1;
    public string Language { get; set; } = "ru";
    public bool Enabled { get; set; } = true;
    public bool CheckPublicChat { get; set; } = true;
    public bool CheckTeamChat { get; set; } = true;
    public bool IgnoreChatCommands { get; set; } = true;
    public bool IgnoreAdministrators { get; set; } = false;
    public List<ulong> ExemptSteamIds { get; set; } = [];
    public int ActiveWarnDays { get; set; } = 30;
    public int HistoryLimit { get; set; } = 50;
    public int BanSuggestionThreshold { get; set; } = 3;
    public int SuggestedBanMinutes { get; set; } = 1440;
    public bool NotifyModeratorsAtThreshold { get; set; } = true;
    public int MaxStoredMessageLength { get; set; } = 300;
    public int RegexTimeoutMilliseconds { get; set; } = 50;
    public List<ChatRule> Rules { get; set; } =
    [
        new() { Id = "links", Reason = "reason_advertising", MatchType = "Regex", Pattern = @"(?i)\b(?:https?://|www\.)\S+", Action = "Warn", BlockMessage = true },
        new() { Id = "caps", Reason = "reason_caps", MatchType = "Caps", MinLength = 12, Threshold = 80, Action = "Warn", BlockMessage = false },
        new() { Id = "repeat", Reason = "reason_repeat", MatchType = "Repeat", Threshold = 8, Action = "Warn", BlockMessage = false }
    ];
}

public sealed class ChatRule
{
    public string Id { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string Reason { get; set; } = "Chat rule violation";
    public string MatchType { get; set; } = "Contains";
    public string Pattern { get; set; } = "";
    public bool CaseSensitive { get; set; } = false;
    public int MinLength { get; set; } = 0;
    public int Threshold { get; set; } = 0;
    public bool CheckPublicChat { get; set; } = true;
    public bool CheckTeamChat { get; set; } = true;
    public string Action { get; set; } = "Warn";
    public bool BlockMessage { get; set; } = true;
    public bool AddWarning { get; set; } = true;
    public int GagMinutes { get; set; } = 10;
    public int CooldownSeconds { get; set; } = 5;
}
