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
    public bool IgnoreAdministrators { get; set; } = true;
    public List<ulong> ExemptSteamIds { get; set; } = [];
    public int HistoryLimit { get; set; } = 50;
    public WarningEscalationConfig WarningEscalation { get; set; } = new();
    public bool NotifyModeratorsAtThreshold { get; set; } = true;
    public int MaxStoredMessageLength { get; set; } = 300;
    public int RegexTimeoutMilliseconds { get; set; } = 50;
    public List<WarningReason> WarningReasons { get; set; } =
    [
        new() { Title = "reason_advertising", Text = "reason_advertising", Severity = 3, Advertising = true },
        new() { Title = "reason_project_insult", Text = "reason_project_insult", Severity = 2 },
        new() { Title = "reason_profanity", Text = "reason_profanity", Severity = 2 },
        new() { Title = "reason_caps", Text = "reason_caps", Severity = 1 },
        new() { Title = "reason_repeat", Text = "reason_repeat", Severity = 1 }
    ];
    public List<ChatRule> Rules { get; set; } =
    [
        new()
        {
            Id = "external-addresses", Reason = "reason_advertising", MatchType = "Regex",
            Pattern = @"(?i)(?<![\p{L}\p{N}@._-])(?:[\p{L}0-9-]+\.)+[\p{L}]{2,63}(?::\d{1,5})?(?![\p{L}\p{N}._-])",
            Patterns = [@"(?<![\d.])(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)(?::\d{1,5})?(?![\d.])"],
            Allowlist =
            [
                @"(?i)(?<![\p{L}\p{N}@._-])(?:[\p{L}0-9-]+\.)*quickfirecorp\.ru(?::\d{1,5})?(?![\p{L}\p{N}._-])",
                @"(?<![\d.])213\.21\.10\.140(?::\d{1,5})?(?![\d.])",
                @"(?i)discord\.gg/MfKUp4F(?![\p{L}\p{N}._/@-])",
                @"(?i)discord\.com/invite/MfKUp4F(?![\p{L}\p{N}._/@-])"
            ],
            Action = "Warn", BlockMessage = true, Severity = 3, Advertising = true
        },
        new()
        {
            Id = "listed-project-names", Reason = "reason_advertising", MatchType = "Regex",
            Patterns = ProjectPatterns(), Action = "Warn", BlockMessage = true, Severity = 3, Advertising = true
        },
        new()
        {
            Id = "links", Reason = "reason_advertising", MatchType = "Domain", Pattern = "*",
            Allowlist = ["quickfirecorp.ru", "213.21.10.140"],
            AllowedUrls = ["discord.gg/MfKUp4F", "discord.com/invite/MfKUp4F"],
            Action = "Warn", BlockMessage = true, Severity = 3, Advertising = true
        },
        new()
        {
            Id = "project-insult", Reason = "reason_project_insult", MatchType = "Regex",
            Patterns =
            [
                @"(?i)(?<![\p{L}\p{N}])(?:quick[\W_]*fire|квик[\W_]*фа[йе]р)(?![\p{L}\p{N}]).{0,40}(?<!не\s)(?<![\p{L}\p{N}])(?:говн[оа]|помойк[а-яё]*|скам|дн[оа]|отсто[йя]|мусор[а-яё]*|дерьм[оа]|херн[яи])(?![\p{L}\p{N}])",
                @"(?i)(?<!не\s)(?<![\p{L}\p{N}])(?:говн[оа]|помойк[а-яё]*|скам|дн[оа]|отсто[йя]|мусор[а-яё]*|дерьм[оа]|херн[яи])(?![\p{L}\p{N}]).{0,40}(?<![\p{L}\p{N}])(?:quick[\W_]*fire|квик[\W_]*фа[йе]р)(?![\p{L}\p{N}])"
            ],
            Action = "Warn", BlockMessage = true, Severity = 2
        },
        new()
        {
            Id = "profanity", Reason = "reason_profanity", MatchType = "Regex",
            Pattern = @"(?i)(?<![\p{L}\p{N}])(?:х[уy][йеёяюи][а-яё]*|п[иы][з3]д[а-яё]*|[её]б[а-яё]*|бл[яеё](?:д[а-яё]*)?|сук[аиу][а-яё]*|мудак[а-яё]*|fuck(?:ing|ed|er|s)?|shit(?:ty|s)?|bitch(?:es|y)?)(?![\p{L}\p{N}])",
            Action = "Warn", BlockMessage = true, Severity = 2
        },
        new() { Id = "caps", Reason = "reason_caps", MatchType = "Caps", MinLength = 12, Threshold = 80, Action = "Warn", BlockMessage = false, Severity = 1 },
        new() { Id = "repeat", Reason = "reason_repeat", MatchType = "Repeat", Threshold = 8, Action = "Warn", BlockMessage = false, Severity = 1 }
    ];

    private static List<string> ProjectPatterns() =>
    [
        .. new[]
        {
            "rehvh", "cs2hvhservers", "nixware", "monkeyhvh", "mcdonaldshvh", "poderosahvh",
            "frague", "rastahvh", "santahvh", "sikintilihvh", "fluxhvh", "hvhlegions",
            "novahvh", "enhancehvh", "darkprojecthvh", "unmatchedgg", "operahvh",
            "wallersnightmare", "matchclubxyz", "evolutionofhvh", "xdgameshvh", "dedsechvh",
            "hvhcat", "hvhclub", "primeareapl", "dreamhvh", "foxhvh", "hvhil", "kittywtf",
            "livehvhnet", "rascalhvh", "goofygang", "kocolo", "fakesmile", "persianstrike",
            "xfamily", "cs2hvhserves", "aslithegoat", "griffinfamily", "liprasafaceit",
            "oskolabad", "pclanduser", "teryakis", "vacemployees", "demigodshvh",
            "faceitclubevip", "hvhgg"
        }.Select(name => @"(?i)(?<![\p{L}\p{N}])" + string.Join(@"[\W_]*", name.ToCharArray()) + @"(?![\p{L}\p{N}])")
    ];
}

public sealed class WarningEscalationConfig
{
    public bool Enabled { get; set; } = true;
    public int WarningThreshold { get; set; } = 3;
    public int DefaultSeverity { get; set; } = 2;
    public int LowMuteMinutes { get; set; } = 30;
    public int MediumMuteMinutes { get; set; } = 120;
    public int HighMuteMinutes { get; set; } = 1440;
    public bool AdvertisingPermanent { get; set; } = true;
    public bool CountManualWarnings { get; set; } = true;
    public bool CountAutomaticWarnings { get; set; } = true;
}

public sealed class WarningReason
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public bool HideFromMenu { get; set; }
    public int Severity { get; set; } = 2;
    public bool Advertising { get; set; }
}

public sealed class ChatRule
{
    public string Id { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string Reason { get; set; } = "Chat rule violation";
    public string MatchType { get; set; } = "Contains";
    public string Pattern { get; set; } = "";
    public List<string> Patterns { get; set; } = [];
    public List<string> Allowlist { get; set; } = [];
    public List<string> AllowedUrls { get; set; } = [];
    public bool CaseSensitive { get; set; } = false;
    public int MinLength { get; set; } = 0;
    public int Threshold { get; set; } = 0;
    public bool CheckPublicChat { get; set; } = true;
    public bool CheckTeamChat { get; set; } = true;
    public string Action { get; set; } = "Warn";
    public bool BlockMessage { get; set; } = true;
    public bool AddWarning { get; set; } = true;
    public int GagMinutes { get; set; } = 10;
    public int MuteMinutes { get; set; } = 10;
    public int CooldownSeconds { get; set; } = 5;
    public int WarningAfterConsecutive { get; set; } = 3;
    public int WarningAfterWindowCount { get; set; } = 6;
    public int WarningWindowSeconds { get; set; } = 600;
    public int ConsecutiveGapSeconds { get; set; } = 120;
    public int Severity { get; set; } = 2;
    public bool Advertising { get; set; }
}
