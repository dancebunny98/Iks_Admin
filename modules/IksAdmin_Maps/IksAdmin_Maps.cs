using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using IksAdminApi;

namespace IksAdmin_Maps;

public class MapEntry
{
    public string Label { get; set; } = "";
    public string Map { get; set; } = "";
}

public class MapCategory
{
    public string Label { get; set; } = "";
    public List<MapEntry> Maps { get; set; } = new();
}

public class MapsConfig : BasePluginConfig
{
    public override int Version { get; set; } = 1;

    // Объявлять ли всем в чат о смене карты
    public bool AnnounceToAll { get; set; } = true;

    // Категории и карты в меню - можно менять/добавлять/удалять как угодно.
    // Если категория одна - подменю категорий всё равно покажется (для единообразия);
    // если хотите список карт без категорий - оставьте один элемент Categories
    // с пустым или общим Label.
    public List<MapCategory> Categories { get; set; } = new()
    {
        new MapCategory
        {
            Label = "Competitive",
            Maps = new List<MapEntry>
            {
                new() { Label = "Dust II", Map = "de_dust2" },
                new() { Label = "Mirage", Map = "de_mirage" },
                new() { Label = "Inferno", Map = "de_inferno" },
                new() { Label = "Nuke", Map = "de_nuke" },
                new() { Label = "Overpass", Map = "de_overpass" },
                new() { Label = "Vertigo", Map = "de_vertigo" },
                new() { Label = "Ancient", Map = "de_ancient" },
                new() { Label = "Anubis", Map = "de_anubis" },
            }
        },
        new MapCategory
        {
            Label = "Wingman",
            Maps = new List<MapEntry>
            {
                new() { Label = "Overpass (Wingman)", Map = "de_overpass" },
                new() { Label = "Inferno (Wingman)", Map = "de_inferno" },
            }
        }
    };
}

public class Main : AdminModule, IPluginConfig<MapsConfig>
{
    public override string ModuleName => "IksAdmin_Maps";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "iks__ modules";

    public MapsConfig Config { get; set; } = new();
    public void OnConfigParsed(MapsConfig config) => Config = config;

    private const string Permission = "maps_manage.change";

    public override void InitializeCommands()
    {
        Api.RegisterPermission(Permission, "m");

        Api.AddNewCommand(
            command: "maps",
            description: "Сменить карту через меню",
            permission: Permission,
            usage: "css_maps",
            onExecute: OnMapsCommand,
            whoCanExecute: CommandUsage.CLIENT_ONLY
        );

        Api.RegisterMainMenuOption(
            id: "maps_change",
            title: () => Localizer["MenuOption.ChangeMap"],
            onExecute: (caller, backMenu) => OpenCategoryMenu(caller, backMenu),
            viewFlags: AdminUtils.GetCurrentPermissionFlags(Permission)
        );
    }

    private void OnMapsCommand(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        if (caller == null || !caller.IsValid) return;
        OpenCategoryMenu(caller);
    }

    private void OpenCategoryMenu(CCSPlayerController caller, IDynamicMenu? backMenu = null)
    {
        if (Config.Categories.Count == 0)
        {
            caller.Print($" {ChatColors.Red}{Localizer["Chat.NoMapsConfigured"]}");
            return;
        }

        // Если категория ровно одна - сразу открываем список карт, не заставляя
        // администратора лишний раз тыкать в подменю с одним пунктом.
        if (Config.Categories.Count == 1)
        {
            OpenMapListMenu(caller, Config.Categories[0], backMenu);
            return;
        }

        var menu = Api.CreateMenu(
            id: "iksadmin_maps:menu:category",
            title: Localizer["MenuTitle.SelectCategory"],
            backMenu: backMenu
        );

        foreach (var category in Config.Categories)
        {
            menu.AddMenuOption(category.Label, category.Label, (_, _) =>
            {
                OpenMapListMenu(caller, category, menu);
            });
        }

        menu.Open(caller);
    }

    private void OpenMapListMenu(CCSPlayerController caller, MapCategory category, IDynamicMenu? backMenu)
    {
        var menu = Api.CreateMenu(
            id: "iksadmin_maps:menu:list:" + category.Label,
            title: string.IsNullOrEmpty(category.Label)
                ? Localizer["MenuTitle.SelectMap"]
                : $"{Localizer["MenuTitle.SelectMap"]}: {category.Label}",
            backMenu: backMenu
        );

        if (category.Maps.Count == 0)
        {
            menu.AddMenuOption("none", Localizer["Chat.NoMapsConfigured"], (_, _) => { }, disabled: true);
        }
        else
        {
            foreach (var map in category.Maps)
            {
                menu.AddMenuOption(map.Map, $"{map.Label} ({map.Map})", (_, _) =>
                {
                    ChangeMap(caller, map);
                });
            }
        }

        menu.Open(caller);
    }

    private void ChangeMap(CCSPlayerController caller, MapEntry map)
    {
        caller.Print($" {ChatColors.Green}{Localizer["Chat.ChangingMap"]} {ChatColors.Gold}{map.Label} ({map.Map})");

        if (Config.AnnounceToAll)
        {
            Server.PrintToChatAll(
                $" {ChatColors.Green}[Maps] {ChatColors.White}{caller.PlayerName} {Localizer["Chat.PlayerChangedMap"]} {ChatColors.Gold}{map.Label}");
        }

        Server.ExecuteCommand($"changelevel {map.Map}");
    }
}
