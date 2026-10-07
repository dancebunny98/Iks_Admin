# IksAdmin Chat Moderation

Moderates CS2 text chat using configurable rules and player warnings in the shared `iks_admins_warns` table. This module requires the IksAdmin core and its shared `IksAdminApi` assembly. Existing administrator warnings keep their IDs and continue to disable administrator privileges according to `MaxWarns`. Player warnings have a null `target_id`, so they do not change permissions or punish players on their own.

## Installation

Build `IksAdmin_ChatModeration.csproj` for .NET 10. Deploy `IksAdmin_ChatModeration.dll`, `Dapper.dll`, `MySqlConnector.dll`, the `lang` directory, and `README.md` to `addons/counterstrikesharp/plugins/IksAdmin_ChatModeration/`. Deploy `IksAdmin_ChatModeration.json` to `addons/counterstrikesharp/configs/plugins/IksAdmin_ChatModeration/`. The GitHub Actions `build.yml` matrix builds and packages these files. Start the updated IksAdmin core before this module. On first start, the module extends the existing `iks_admins_warns` table without deleting rows. The database account needs `ALTER`, `INDEX`, `SELECT`, `INSERT`, and `UPDATE` privileges on that table. The old `iks_chat_warnings` table is unused; when it is empty it can be removed separately.

## Permissions and commands

| Permission | Default flag | Command or menu |
| --- | --- | --- |
| `chat_moderation.warn` | `g` | `css_chatwarn <player> <reason>`; issue a warning from the admin menu. |
| `chat_moderation.review` | `g` | `css_chatwarns <player>`; view warning history in the admin menu. |
| `chat_moderation.review` | `g` | `css_warns` or `!warns`; list online players and active warning counts, with moderators first and their group after their name. `css_warns <player>` opens one history. |
| `chat_moderation.revoke` | `z` | `css_chatunwarn <player> <warning_id>`. |
| None | Everyone | `css_mywarns`; view your own warnings. |

Player lookup supports `#UserID`, SteamID64, an exact name, or a name fragment. Moderator actions honor IksAdmin immunity checks. The admin menu includes a Chat moderation entry with player selection, history, manual warning issuance, and warning removal for users with `chat_moderation.revoke`. A manual warning reason must be 3 to 255 characters.

## Warning and ban flow

Warnings store a reason, message excerpt, timestamp, issuer, and source. Automatic warnings are labeled **Automatic moderation**; manual warnings are labeled **Moderator**. `ActiveWarnDays` controls the rolling window used for the active count (`0` means all time). Revoked warnings never count. `HistoryLimit` controls how many records appear in the menu. After a manual warning reaches `BanSuggestionThreshold`, the moderator sees the active reasons and a suggested ban option if they have `blocks_manage.ban`. Set the threshold to `0` to disable ban offers. Choosing the ban option invokes IksAdmin's normal `AddBan` path, so its checks and announcements remain in force. Automatic warnings never ban a player; reaching the threshold can notify online moderators.

## Rule settings

Edit `IksAdmin_ChatModeration.json`, then reload the module. `Language` accepts `ru` or `en`, using the files in `lang/`. `Enabled`, `CheckPublicChat`, `CheckTeamChat`, `IgnoreChatCommands`, `IgnoreAdministrators`, and `ExemptSteamIds` define the overall scope. `ActiveWarnDays`, `HistoryLimit`, `BanSuggestionThreshold`, `SuggestedBanMinutes`, and `NotifyModeratorsAtThreshold` control warning review and ban offers. A suggested ban duration of `0` means permanent; a negative value hides the ban action. `MaxStoredMessageLength` caps message excerpts. `RegexTimeoutMilliseconds` bounds regex work per message.

Each `Rules` entry has an `Id`, `Enabled`, `Reason`, `MatchType`, `Pattern`, optional `MinLength` and `Threshold`, public/team switches, `Action`, `BlockMessage`, `AddWarning`, `GagMinutes`, and `CooldownSeconds`. `Reason` can be literal text or a key in `lang/*.json`; the bundled rules use translation keys. Supported match types are `Contains`, `Exact`, `StartsWith`, `EndsWith`, `Regex`, `Length`, `Duplicate`, `Repeat`, and `Caps`. `Caps` uses `Threshold` as the uppercase percentage; `Repeat` uses it as the number of consecutive equal characters; `Length` uses it as the minimum message length. `Action` may be `Warn`, `Gag`, `Block`, or `Ignore`. `BlockMessage` independently controls whether the matched chat message is suppressed. `AddWarning` controls persistence; `Gag` applies the configured gag duration via IksAdmin.

Rules run in array order. The first matching rule handles a message. Avoid overly broad patterns. Invalid regex patterns are logged and skipped, while valid patterns have a timeout. The built-in IksAdmin anti-flood feature still runs independently.
