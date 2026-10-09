# IksAdmin Discord Punishments

Подробная инструкция по Discohook JSON, раздельным webhook для игроков и модераторов, всем событиям и заполнителям: `DISCORD_MESSAGES.md` в архиве модуля или [docs/discord-messages.md](../../docs/discord-messages.md) в репозитории.

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

Варны настраиваются отдельно в `addons/counterstrikesharp/configs/plugins/IksAdmin_DiscordPunishments/warnings.json`. `PlayerWebhook` принимает варны игроков, `ModeratorWebhook` — варны модераторов. Пустой URL означает отсутствие отправки этого типа варнов. Переключатели `Issued`, `Removed`, `Automatic`, `Test` остаются доступными. `IssuedTemplate` и `RemovedTemplate` принимают полный JSON сообщения из Discohook; для разных сообщений игрокам и модераторам используйте `Messages` в `warnings.json`. Старые короткие шаблоны и записи варнов в `Messages` основного конфига продолжают работать. Отчёты отдельно считают выданные и снятые варны; CSV содержит ID, источник и дату снятия. Изменения обоих конфигов применяет `css_discord_logs_reload`.

Для наказаний доступны `{punishmentid}` (любой сохранённый ID), `{banid}`, `{commid}`, `{muteid}`, `{gagid}` и `{silenceid}`. При снятии и истечении используется ID исходной записи. Кик не имеет ID в БД. Дополнительные поля и применимость каждого заполнителя перечислены в `DISCORD_MESSAGES.md`.

Команды:

```text
css_report [day|week|month|all]
css_report_export <day|week|month|all>
css_discord_logs_reload
```

Дополнительные режимы отчёта: `css_report admin <имя или SteamID> [period]`, `css_report player <имя или SteamID> [period]` и `css_report all [period]`.

Webhook URL является секретом. Не добавляйте его в репозиторий или публичные логи; после публикации URL его следует перевыпустить в Discord.
