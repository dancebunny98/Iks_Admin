# IksAdmin Chat Moderation

Moderates CS2 text chat using configurable rules and player warnings in the shared `iks_admins_warns` table. This module requires the IksAdmin core and its shared `IksAdminApi` assembly. Existing administrator warnings keep their IDs. Player warnings have a null `target_id` and count toward `MaxWarns` when the target has administrator permissions. They also count toward the separate player chat mute threshold.

## Installation

Build `IksAdmin_ChatModeration.csproj` for .NET 10. Deploy `IksAdmin_ChatModeration.dll`, `Dapper.dll`, `MySqlConnector.dll`, the `lang` directory, and `README.md` to `addons/counterstrikesharp/plugins/IksAdmin_ChatModeration/`. Deploy `IksAdmin_ChatModeration.json` to `addons/counterstrikesharp/configs/plugins/IksAdmin_ChatModeration/`. The GitHub Actions `build.yml` matrix builds and packages these files. Start the updated IksAdmin core before this module. On first start, the module extends the existing `iks_admins_warns` table without deleting rows. The database account needs `ALTER`, `INDEX`, `SELECT`, `INSERT`, and `UPDATE` privileges on that table. The old `iks_chat_warnings` table is unused; when it is empty it can be removed separately.

## Permissions and commands

| Permission | Default flag | Command or menu |
| --- | --- | --- |
| `chat_moderation.warn` | `g` | `css_warn <player> <reason>` or `css_chatwarn <player> <reason>`; issue a warning from the admin menu. |
| `chat_moderation.review` | `g` | `css_chatwarns <player>`; view warning history in the admin menu. |
| None | Everyone | `css_warns` or `!warns`; list your own active warnings, including administrator warnings. |
| `chat_moderation.revoke` | `z` | `css_chatunwarn <player> <warning_id>`. |
| None | Everyone | `css_mywarns`; view your own warnings. |

Player lookup supports `#UserID`, SteamID64, an exact name, or a name fragment. A moderator can warn targets with equal or lower immunity. The full warning list, including offline players, and manual issuance are under Chat Management in the admin menu. The original issuer can revoke a warning; another moderator needs equal or higher immunity and the permission for that warning type. A warning whose reason is exactly `test` can be issued to oneself and revoked by any administrator. A manual warning reason must be 3 to 255 characters.

In the menu, choose a player, then select a configured warning reason or enter a custom reason in chat. Open a warning from the history list to see its details and revoke it when permitted. Administrators can also revoke eligible warnings from their own `!warns` list. Administrator warnings use the core `admins_manage.warn_delete` permission; player chat warnings use `chat_moderation.revoke`.

## Warning and mute flow

Warnings store a reason, message excerpt, timestamp, issuer, and source. Automatic warnings are labeled **Automatic moderation**; manual warnings are labeled **Moderator**. Warnings never expire; only revoked warnings stop counting. `test` warnings remain visible but do not count toward moderator group removal or player mutes. `HistoryLimit` controls how many records appear in the moderator history menu. When active player warnings reach `WarningEscalation.WarningThreshold` (three by default), the module blocks text chat through IksAdmin. The highest warning severity sets the duration; any advertising warning makes it permanent when `AdvertisingPermanent` is enabled. Player warnings do not cause a ban. Automatic `Ban` rules are disabled and logged during startup.

## Rule settings

Edit `IksAdmin_ChatModeration.json`, then reload the module. Menu text follows the IksAdmin core language (`ru`, `en`, or `ua`); `Language` is used only with an older core that does not expose its locale. `Enabled`, `CheckPublicChat`, `CheckTeamChat`, `IgnoreChatCommands`, `IgnoreAdministrators`, and `ExemptSteamIds` define the overall scope. `HistoryLimit` controls review; `NotifyModeratorsAtThreshold` sends threshold notices. `MaxStoredMessageLength` caps message excerpts. `RegexTimeoutMilliseconds` bounds regex work per message. Older `BanSuggestionThreshold` and `SuggestedBanMinutes` settings are obsolete; replace them with `WarningEscalation`.

`WarningEscalation` has `Enabled`, `WarningThreshold` (0 disables automatic mutes), `DefaultSeverity`, `LowMuteMinutes`, `MediumMuteMinutes`, `HighMuteMinutes`, `AdvertisingPermanent`, `CountManualWarnings`, and `CountAutomaticWarnings`. Severity is 1, 2, or 3. A duration of `0` is permanent; a negative duration disables that severity's mute. Administrator-targeted core warnings and `test` warnings are excluded from this player threshold. The threshold applies when the count is first reached; existing warnings are not retroactively muted after configuration changes.

Each `Rules` entry has an `Id`, `Enabled`, `Reason`, `MatchType`, `Pattern`, optional `Patterns` and `Allowlist` arrays, optional `MinLength` and `Threshold`, public/team switches, `Action`, `BlockMessage`, `AddWarning`, `GagMinutes`, `MuteMinutes`, `CooldownSeconds`, `Severity`, and `Advertising`. `Reason` can be literal text or a key in `lang/*.json`; the bundled rules use translation keys. Supported match types are `Contains`, `Exact`, `StartsWith`, `EndsWith`, `Regex`, `Domain`, `Length`, `Duplicate`, `Repeat`, and `Caps`. `Caps` uses `Threshold` as the uppercase percentage; `Repeat` uses it as the number of consecutive equal characters; `Length` uses it as the minimum message length. `Pattern` and `Patterns` form a blocklist for text and domain rules; `Allowlist` excludes matching text spans or domains from that rule. List entries use the rule's `MatchType` and `CaseSensitive` setting. `Action` may be `Warn`, `Gag`, `Mute`, `Block`, or `Ignore`. `BlockMessage` independently controls whether the matched chat message is suppressed. `Gag` blocks text chat, while `Mute` blocks both text and voice chat. `AddWarning` works with any enabled action. Durations are in minutes; `0` means permanent and a negative value disables the punishment.

`WarningAfterConsecutive` (default 3) issues a warning after that many messages matching the same rule in a row. A normal message or a different rule breaks the streak. `ConsecutiveGapSeconds` limits the gap between matching messages. `WarningAfterWindowCount` (default 6) also issues a warning when the rule repeatedly matches within `WarningWindowSeconds` (default 600), even when normal messages interrupt the streak. Set either count to `0` to disable that trigger. Counters reset after a warning and are held in memory until disconnect or module restart. `CooldownSeconds` throttles notifications and immediate `Gag`/`Mute` actions; warning counts continue across the cooldown.

`WarningReasons` defines the manual warning menu. Each entry has `Title` (menu label), `Text` (stored reason), `Severity`, `Advertising`, and optional `HideFromMenu`. A title or text matching a key in `lang/*.json` is displayed in the selected language. Literal values are shown as configured. Custom reasons use `DefaultSeverity`.

`Domain` checks HTTP(S) and `www.` links by hostname, including subdomains. `Pattern: "*"` matches any linked domain. An allowlisted domain such as `example.com` also allows `www.example.com` and `sub.example.com`, but not `example.com.evil.net`. `AllowedUrls` exempts one URL path on a shared host, such as `discord.gg/YourInvite`, without allowing all of `discord.gg`. If one message contains both allowed and blocked links, the blocked link still triggers the rule. Add your domains to the `links` rule's `Allowlist` in the JSON config. Existing configs using the old `Regex` links rule should change its `MatchType` to `Domain` and its `Pattern` to `*` to use domain exceptions. For other blocklists, use separate rules when different terms need different actions or durations. Example:

```json
{
  "Id": "blocked-links",
  "Enabled": true,
  "Reason": "Forbidden link",
  "MatchType": "Domain",
  "Pattern": "blocked.example",
  "Patterns": ["ads.example"],
  "Allowlist": ["trusted.blocked.example"],
  "Action": "Warn",
  "Severity": 3,
  "Advertising": true,
  "WarningAfterConsecutive": 3,
  "WarningAfterWindowCount": 6,
  "BlockMessage": true,
  "CooldownSeconds": 5
}
```

Place a specific blocked-domain rule before a general `links` rule because the first matching rule wins. Add trusted domains to the general `links` rule's `Allowlist` as well if they should pass that rule. Set `Action` to `Gag` and `GagMinutes` to `0` for an immediate permanent text-chat block on a specific rule.

Rules run in array order. The first matching rule handles a message. Avoid overly broad patterns. Invalid regex patterns are logged and skipped, while valid patterns have a timeout. The built-in IksAdmin anti-flood feature still runs independently.
