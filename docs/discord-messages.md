# Discord-сообщения IksAdmin: настройка и шаблоны Discohook

Модуль `IksAdmin_DiscordPunishments` отправляет события наказаний и варнов в Discord через webhook. Сообщение каждого типа можно полностью задать в JSON, созданном на [Discohook](https://discohook.app/): текст, несколько embeds, поля, цвета, изображения по URL, подпись, автор и ссылочные кнопки. Подстановка `{...}` работает в любом **строковом значении** JSON, в том числе в URL и надписях кнопок. Числа, `true`/`false` и имена ключей JSON не изменяются.

## 1. Webhook и файлы

Рабочие файлы CounterStrikeSharp создаёт в `addons/counterstrikesharp/configs/plugins/IksAdmin_DiscordPunishments/`:

| Файл | Назначение |
| --- | --- |
| `IksAdmin_DiscordPunishments.json` | Webhook наказаний, шаблоны `Messages`, старые настройки embeds, отчёты. |
| `warnings.json` | Отдельные webhook варнов игроков и модераторов, фильтры варнов и стандартные шаблоны. |
| `records.json` | Локальная история событий. |
| `failed-webhooks.jsonl` | Запросы, которые не удалось доставить. |

В `warnings.json` задайте **два разных webhook URL**:

```json
{
  "Enabled": true,
  "PlayerWebhook": "https://discord.com/api/webhooks/PLAYER_ID/PLAYER_TOKEN",
  "ModeratorWebhook": "https://discord.com/api/webhooks/MODERATOR_ID/MODERATOR_TOKEN",
  "Issued": true,
  "Removed": true,
  "Automatic": true,
  "Test": true
}
```

Это пример фрагмента: при редактировании существующего файла сохраните остальные поля. `PlayerWebhook` получает ручные и автоматические варны игроков из ChatModeration. `ModeratorWebhook` получает варны модераторов из ядра IksAdmin. Если нужный URL пуст, событие остаётся в локальной истории, но **не отправляется в другой канал**. Старое поле `Webhook` не используется для маршрутизации; его можно удалить. Обычные бан, мут, gag, silence, kick и их снятие идут в `Webhooks.Punishments` главного файла. `Webhooks.Reports` и `Webhooks.Anomalies` независимы.

Webhook URL является секретом. Не публикуйте рабочий конфиг. После смены файлов выполните `css_discord_logs_reload` из консоли сервера или с правом `discord_logs.reload`.

## 2. Как перенести сообщение из Discohook

1. Создайте сообщение на Discohook и экспортируйте его JSON.
2. Откройте рабочий `IksAdmin_DiscordPunishments.json` и найдите объект `Messages`. Если его нет, добавьте его на верхнем уровне, рядом с `Templates` и `Fields`.
3. Добавьте ключ события из таблицы ниже. Его значением сделайте **весь объект сообщения** из Discohook: `content`, `embeds`, `components`, `username`, `avatar_url` и другие поля. Не вставляйте webhook URL в `Messages`.
4. Замените демонстрационные имена и ID на заполнители, например `{player}`, `{servername}`, `{warningid}`. Сохраните корректный JSON без комментариев и лишних запятых.
5. Выполните `css_discord_logs_reload`, затем создайте тестовый варн или используйте `css_discord_logs_test` для стандартного теста наказаний. Проверяйте лог сервера: неверный шаблон отмечается именем события.

Пример: один шаблон для выданного варна игроку. Данные в угловых скобках здесь не являются реальными webhook-секретами.

```json
{
  "Messages": {
    "player_warn_issued": {
      "content": "Варн на сервере **{servername}**",
      "embeds": [
        {
          "title": "Варн #{warningid}: {player}",
          "description": "Причина: {reason}\nВыдал: {admin}\nИсточник: {source}",
          "color": 5814783,
          "fields": [
            { "name": "SteamID64", "value": "{steamid}", "inline": true },
            { "name": "Дата", "value": "<t:{createdunix}:F>", "inline": true },
            { "name": "Сообщение", "value": "{message}", "inline": false }
          ],
          "footer": { "text": "{servername} | #{serverid}" },
          "timestamp": "{issuediso}"
        }
      ],
      "components": [
        { "type": 1, "components": [
          { "type": 2, "style": 5, "label": "Профиль {player}", "url": "{playerurl}" }
        ] }
      ]
    }
  }
}
```

Фрагмент выше показывает структуру. В реальном конфиге объект `Messages` вставляется в существующий корневой объект. `{message}` бывает пустым для ручного варна: Discord не принимает пустое `fields[].value`, поэтому удалите такое поле из шаблона ручных варнов или подставьте текст в другое поле с постоянным содержимым, например `Сообщение: {message}`. `{playerurl}` работает, когда у события известен числовой SteamID64. Для событий без SteamID не ставьте этот заполнитель в URL кнопки.

## 3. Ключи событий

| Ключ в `Messages` | Событие | Webhook |
| --- | --- | --- |
| `player_warn_issued` | Варн игроку выдан, ручной или автоматический. | `PlayerWebhook` |
| `player_warn_removed` | Варн игрока снят. | `PlayerWebhook` |
| `moderator_warn_issued` | Варн модератору выдан. | `ModeratorWebhook` |
| `moderator_warn_removed` | Варн модератора снят. | `ModeratorWebhook` |
| `ban` | Бан. | `Webhooks.Punishments` |
| `mute`, `gag`, `silence` | Ограничение общения. | `Webhooks.Punishments` |
| `kick` | Исключение игрока. | `Webhooks.Punishments` |
| `unban`, `uncomm` | Снятие бана или ограничения общения. | `Webhooks.Punishments` |
| `expired` | Срок наказания истёк. | `Webhooks.Punishments` |
| `report` | Ручной или плановый отчёт. | `Webhooks.Reports` |
| `anomaly` | Превышен порог активности наказаний. | `Webhooks.Anomalies` |

Ключи пишутся именно так, строчными буквами. Можно заполнить только нужные события. Без записи в `Messages` модуль использует прежний `Templates`/`Fields` и стандартный embed. Для варнов он использует `IssuedTemplate`, `RemovedTemplate` и `Fields` из `warnings.json`.

## 4. Полный список заполнителей

Регистр букв не важен: `{ServerName}` и `{servername}` эквивалентны. Варианты с подчёркиванием также поддерживаются там, где указаны ниже. Заполнитель, которого нет в таблице, остаётся без изменений, чтобы опечатка была заметна в отправленном сообщении. Пустое или неприменимое значение превращается в пустую строку. В имени игрока, причине и других значениях `@everyone` и `@here` обезвреживаются; в отправке дополнительно отключены Discord mentions.

| Значение | Заполнители |
| --- | --- |
| Имя цели | `{player}`, `{target}`, `{playername}`, `{player_name}` |
| SteamID64 цели | `{steamid}`, `{steamid64}`, `{playersteamid}`, `{player_steamid}` |
| Ссылка на Steam-профиль цели | `{playerurl}`, `{player_url}` |
| Имя выдавшего или снявшего наказание | `{admin}`, `{issuer}`, `{adminname}`, `{admin_name}` |
| SteamID64 действующего администратора | `{adminsteamid}`, `{admin_steamid}` |
| Ссылка на его Steam-профиль | `{adminurl}`, `{admin_url}` |
| Причина выдачи | `{reason}` |
| Причина снятия, если записана | `{removereason}`, `{remove_reason}` |
| Длительность в удобном виде | `{duration}` |
| Длительность числом в секундах | `{durationseconds}`, `{duration_seconds}` |
| Длительность числом в целых минутах | `{durationminutes}`, `{duration_minutes}` |
| Дата выдачи в формате `EmbedSettings.DateFormat` и зоне `TimeZone` | `{issuedat}`, `{issued_at}`, `{createdat}` |
| Дата окончания в том же формате; `Never`, если срока нет | `{expiresat}`, `{expires_at}` |
| Дата снятия, если событие снятия | `{removedat}`, `{removed_at}` |
| Время выдачи в Unix-секундах для Discord `<t:...:F>` | `{createdunix}`, `{created_unix}` |
| Время окончания в Unix-секундах (`0`, если бессрочно) | `{expiresunix}`, `{expires_unix}` |
| Время снятия в Unix-секундах | `{removedunix}`, `{removed_unix}` |
| Время выдачи в ISO 8601 для `embed.timestamp` | `{issuediso}`, `{issued_iso}` |
| Время окончания в ISO 8601 | `{expiresiso}`, `{expires_iso}` |
| Время снятия в ISO 8601 | `{removediso}`, `{removed_iso}` |
| Название сервера из IksAdmin | `{server}`, `{servername}`, `{server_name}` |
| ID сервера из IksAdmin | `{serverid}`, `{server_id}` |
| Адрес сервера из IksAdmin | `{serverip}`, `{server_ip}` |
| Число подключённых игроков на момент отправки | `{online}` |
| Текущее время в настроенном формате, Unix и ISO 8601 | `{now}`, `{nowunix}`, `{nowiso}` |
| Тип события: например `ban` или `warn_issued` | `{type}`, `{event}` |
| ID варна; для других событий `0` | `{warningid}`, `{warning_id}` |
| Источник варна: `automatic`, `moderator` или `administrator` | `{source}` |
| Сохранённый текст сообщения, вызвавшего варн | `{message}` |
| Имя изначально выдавшего варн | `{originalissuer}`, `{original_issuer}` |
| Причина `test`: `true` или `false` | `{test}` |
| Варн модератора: `true` или `false` | `{ismoderatorwarning}` |
| Значок стандартного шаблона | `{emoji}` |

`{admin}` при снятии означает того, кто снял наказание; `{originalissuer}` сохраняет того, кто выдал варн. Для автоматического варна администратор обычно `CONSOLE`. Некоторые события ядра не передают SteamID или причину снятия: соответствующее значение тогда пустое. `{duration}` для бессрочного наказания показывает «Навсегда» в стандартном шаблоне; числовые варианты равны `0`.

Для `report` и `anomaly` используются переменные сводки. Переменные отдельного игрока в этих событиях не доступны.

| Значение отчёта | Заполнитель |
| --- | --- |
| Период (`day`, `week`, `month`, `all` или значение планировщика) | `{period}` |
| Кто запросил отчёт | `{author}` |
| Всего событий | `{count}` |
| Число банов, исключений и ограничений общения | `{bans}`, `{kicks}`, `{comms}` |
| Число снятых банов и ограничений общения | `{unbans}`, `{uncomms}` |
| Выданные и снятые варны | `{warnings}`, `{removedwarnings}` |
| Порог аномалии из настроек | `{threshold}` |
| Сервер | `{servername}`, `{server}`, `{serverid}`, `{serverip}` |
| Текущее время | `{now}`, `{nowunix}`, `{nowiso}` |

## 5. Совместимость с Discohook и ограничения

- Поддерживаются `content`, до 10 `embeds` с обычными полями Discord, `username`, `avatar_url`, `tts` и до пяти строк `components` по пять кнопок в каждой. Кнопка должна иметь `type: 2`, `style: 5` и HTTPS `url`. Это **ссылочная** кнопка.
- Кнопки с `custom_id`, выпадающие меню и формы требуют Discord-бота, который обрабатывает взаимодействия. Обычный webhook IksAdmin не умеет принимать нажатия. Такой шаблон отклоняется; событие отправляется стандартным embed, а ошибка записывается в серверный лог.
- Загруженные в Discohook локальные файлы/attachments нельзя перенести одним JSON: Discord требует отдельную multipart-загрузку. Для изображения используйте публичный URL в `embeds[].image.url`, `thumbnail.url` или `avatar_url`.
- Длина `content` ограничена 2000 символами; имени webhook — 80; заголовка embed — 256; описания — 4096; имени поля — 256; значения поля — 1024; подписи footer — 2048; имени автора embed — 256; текста кнопки — 80. Сумма текста embeds ограничена 6000 символами. Пустое сообщение и слишком длинный шаблон отклоняются с записью ошибки и заменяются стандартным embed. Discord также может отклонить другие невалидные свойства; ответ API попадёт в лог и `failed-webhooks.jsonl`.
- Плейсхолдеры разворачиваются перед проверкой длины и отправкой. `allowed_mentions` всегда принудительно задаётся как `{"parse":[]}`, даже если Discohook экспортировал другие настройки. Это защищает от упоминаний по тексту игрока.
- Webhook URL не следует вставлять в объект сообщения. Webhook для каждого события выбирается по файлам конфигурации, а не по `Messages`.

## 6. Проверка

После перезагрузки выдайте тестовый варн игроку и модератору и проверьте **два разных канала**. Снимите оба варна и проверьте события снятия. Тестовая причина `test` видна в логах, если `warnings.json` содержит `"Test": true`. Посмотрите консоль сервера при отправке: модуль сообщает об успехе, невалидном шаблоне, пустом webhook или HTTP-ошибке. JSON-фрагменты можно проверить через стандартный JSON-валидатор до перезагрузки.

Варны сохраняются и показываются через Panorama-уведомления IksAdmin. Их количество не отключает права модератора и не запускает пороговый мут или автоматический бан. Отдельные правила ChatModeration с действием `Gag` или `Mute` продолжают работать как самостоятельные правила: они не являются следствием накопления варнов.
