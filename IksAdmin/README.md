# IksAdmin

IksAdmin is the core administration plugin for CounterStrikeSharp. It provides
player punishments, administrator groups, permissions, menus, and the shared
`IksAdminApi` capability used by optional modules.

## Installation

Install the core artifact under `addons/counterstrikesharp/`. Keep
`IksAdminApi.dll` in `shared/IksAdminApi/` and `MenuManagerApi.dll` in
`shared/MenuManagerApi/`; neither belongs in the plugin folder. Configure the
database and server ID in `configs/plugins/IksAdmin/` before starting the server.
The core creates its tables on startup.

## Warnings

Administrator warnings remain in `iks_admins_warns` with a non-null `target_id`.
They retain the existing `MaxWarns` permission restriction. Player chat warnings
added by `IksAdmin_ChatModeration` use a null `target_id` and a SteamID in
`target_steam_id`; they do not affect administrator permissions. Install the
matching version of the Chat Moderation module after upgrading the core.

## Menus and notifications

The optional PanoramaMenuManagerCS2 capability controls the menu type. Panorama
players receive notification toasts for supported actions; other menu types
receive chat messages. Menu Manager may load after IksAdmin; capability lookup
is retried while the server runs.

## Build

```sh
dotnet build IksAdmin/IksAdmin.csproj -c Release
```

GitHub Actions builds the core and modules separately and publishes deployable
artifacts. The core artifact includes this README, English and Russian language
files, and the plugin configuration templates.
