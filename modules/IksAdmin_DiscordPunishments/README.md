# IksAdmin Discord Punishments

Модуль отправляет события наказаний из `IksAdmin` в Discord webhook Embed-сообщениями.

Рабочий конфиг создаётся CounterStrikeSharp после первого запуска:

```text
addons/counterstrikesharp/configs/plugins/IksAdmin_DiscordPunishments/IksAdmin_DiscordPunishments.json
```

Минимальная настройка:

```json
{
  "Webhooks": {
    "Punishments": "https://discord.com/api/webhooks/.../...",
    "Reports": "",
    "Anomalies": "",
    "Errors": ""
  },
  "EmbedSettings": {
    "IncludeAvatar": false,
    "IncludeIp": false,
    "TimeZone": "UTC",
    "DateFormat": "yyyy-MM-dd HH:mm:ss zzz",
    "MaxFieldLength": 1024
  },
  "Reports": {
    "Enabled": true,
    "IntervalMinutes": 60,
    "SendScheduledReports": false,
    "AnomalyThresholdPerHour": 10
  }
}
```

Поддерживаются события банов, mute/gag/silence, снятия наказаний, kick-команд, выдачи и снятия варнов. Записи сохраняются в `records.json`, неотправленные webhook-запросы попадают в `failed-webhooks.jsonl`.

Варны настраиваются отдельно в `addons/counterstrikesharp/configs/plugins/IksAdmin_DiscordPunishments/warnings.json`. Файл содержит свой `Webhook` (пустое значение использует `Webhooks.Punishments`), переключатели `Issued`, `Removed`, `Automatic`, `Test`, шаблоны `IssuedTemplate`/`RemovedTemplate` и настройки полей Embed, включая `IncludeRemovedAt`. Поддерживаются варны администраторов из ядра и автоматические/ручные варны игроков из ChatModeration. В шаблонах доступны `{warningid}`, `{player}`, `{steamid}`, `{admin}`, `{reason}`, `{source}`, `{message}`, `{originalissuer}`, `{test}` и `{server}`. Отчёты отдельно считают выданные и снятые варны; CSV содержит ID, источник и дату снятия. Изменения обоих конфигов применяет `css_discord_logs_reload`.

Команды:

```text
css_report [day|week|month|all]
css_report_export <day|week|month|all>
css_discord_logs_reload
```

Дополнительные режимы отчёта: `css_report admin <имя или SteamID> [period]`, `css_report player <имя или SteamID> [period]` и `css_report all [period]`.

Webhook URL является секретом. Не добавляйте его в репозиторий или публичные логи; после публикации URL его следует перевыпустить в Discord.
