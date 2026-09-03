using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using IksAdminApi;
using Microsoft.Extensions.Logging;
using VipCoreApi;

namespace IksAdmin_VipGive;

public class VipDurationOption
{
    public string Label { get; set; } = "";

    // Длительность в ЧАСАХ - удобная единица для конфига, не зависящая от TimeMode ядра.
    // Модуль сам переводит её в единицу, которую ожидает VIPCore (см. CoreTimeMode ниже).
    public int Hours { get; set; } = 0;

    // Если true - Hours игнорируется, VIP выдаётся навсегда.
    public bool Permanent { get; set; } = false;
}

public class VipGiveConfig : BasePluginConfig
{
    public override int Version { get; set; } = 1;

    // ВАЖНО: должно совпадать с "TimeMode" в configs/plugins/VIPCore/vip_core.json
    // на этом же сервере, иначе выданное время будет посчитано неверно.
    // 0 = секунды (значение по умолчанию в самом VIPCore), 1 = минуты, 2 = часы, 3 = дни.
    public int CoreTimeMode { get; set; } = 0;

    // Объявлять ли всем в чат о выдаче VIP
    public bool AnnounceToAll { get; set; } = true;

    // Список вариантов длительности в меню. Можно менять/добавлять/удалять как угодно.
    public List<VipDurationOption> Durations { get; set; } = new()
    {
        new VipDurationOption { Label = "1 час", Hours = 1 },
        new VipDurationOption { Label = "1 день", Hours = 24 },
        new VipDurationOption { Label = "3 дня", Hours = 72 },
        new VipDurationOption { Label = "7 дней", Hours = 168 },
        new VipDurationOption { Label = "14 дней", Hours = 336 },
        new VipDurationOption { Label = "30 дней", Hours = 720 },
        new VipDurationOption { Label = "3 месяца", Hours = 2160 },
        new VipDurationOption { Label = "6 месяцев", Hours = 4320 },
        new VipDurationOption { Label = "Навсегда", Permanent = true },
    };
}

public class Main : AdminModule, IPluginConfig<VipGiveConfig>
{
    public override string ModuleName => "IksAdmin_VipGive";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "iks__ modules";

    public VipGiveConfig Config { get; set; } = new();
    public void OnConfigParsed(VipGiveConfig config) => Config = config;

    private const string Permission = "vip_manage.give";

    private readonly PluginCapability<IVipCoreApi?> _vipCoreCapability = new("vipcore:core");
    private IVipCoreApi? _vipApi;

    // Ready() вызывается после того, как загружены ВСЕ плагины (см. AdminModule.OnAllPluginsLoaded),
    // поэтому к этому моменту VIPCore уже успел зарегистрировать свою capability.
    public override void Ready()
    {
        _vipApi = _vipCoreCapability.Get();
        if (_vipApi == null)
        {
            Logger.LogWarning(
                "[IksAdmin_VipGive] Плагин VIPCore не найден. Команда !givevip не будет работать, пока VIPCore не загружен на этом сервере.");
        }
    }

    public override void InitializeCommands()
    {
        Api.RegisterPermission(Permission, "z");

        Api.AddNewCommand(
            command: "givevip",
            description: "Выдать/изменить VIP игроку через меню",
            permission: Permission,
            usage: "css_givevip",
            onExecute: OnGiveVipCommand,
            whoCanExecute: CommandUsage.CLIENT_ONLY
        );
    }

    private void OnGiveVipCommand(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        if (caller == null || !caller.IsValid) return;

        if (_vipApi == null)
        {
            caller.Print($" {ChatColors.Red}{Localizer["Chat.VipCoreNotFound"]}");
            return;
        }

        OpenSelectPlayerMenu(caller);
    }

    private void OpenSelectPlayerMenu(CCSPlayerController caller)
    {
        MenuUtils.OpenSelectPlayer(caller, "givevip", (target, playerMenu) =>
        {
            if (target.Controller == null)
            {
                caller.Print($" {ChatColors.Red}{Localizer["Chat.PlayerOffline"]}");
                return;
            }

            OpenSelectGroupMenu(caller, target, playerMenu);
        }, includeBots: false, customTitle: Localizer["MenuTitle.SelectPlayer"]);
    }

    private void OpenSelectGroupMenu(CCSPlayerController caller, PlayerInfo target, IDynamicMenu playerMenu)
    {
        var groups = _vipApi!.GetVipGroups();

        var menu = Api.CreateMenu(
            id: "iksadmin_vipgive:menu:group",
            title: $"{Localizer["MenuTitle.SelectGroup"]}: {target.PlayerName}",
            backMenu: playerMenu
        );

        if (groups.Length == 0)
        {
            menu.AddMenuOption("none", Localizer["Menu.NoGroups"], (_, _) => { }, disabled: true);
        }
        else
        {
            foreach (var group in groups)
            {
                menu.AddMenuOption(group, group, (_, _) =>
                {
                    OpenSelectDurationMenu(caller, target, group, menu);
                });
            }
        }

        menu.Open(caller);
    }

    private void OpenSelectDurationMenu(CCSPlayerController caller, PlayerInfo target, string group,
        IDynamicMenu groupMenu)
    {
        var menu = Api.CreateMenu(
            id: "iksadmin_vipgive:menu:duration",
            title: Localizer["MenuTitle.SelectDuration"],
            backMenu: groupMenu
        );

        if (Config.Durations.Count == 0)
        {
            menu.AddMenuOption("none", Localizer["Menu.NoDurations"], (_, _) => { }, disabled: true);
        }
        else
        {
            foreach (var duration in Config.Durations)
            {
                menu.AddMenuOption(duration.Label, duration.Label, (_, _) =>
                {
                    GiveVip(caller, target, group, duration);
                });
            }
        }

        menu.Open(caller);
    }

    private void GiveVip(CCSPlayerController caller, PlayerInfo target, string group, VipDurationOption duration)
    {
        var controller = target.Controller;
        if (controller == null || !controller.IsValid)
        {
            caller.Print($" {ChatColors.Red}{Localizer["Chat.PlayerOffline"]}");
            return;
        }

        var time = duration.Permanent ? 0 : ConvertHoursToCoreTime(duration.Hours);

        try
        {
            if (_vipApi!.IsClientVip(controller))
            {
                _vipApi.SetClientVip(controller, group, time);
            }
            else
            {
                _vipApi.GiveClientVip(controller, group, time);
            }
        }
        catch (Exception e)
        {
            caller.Print($" {ChatColors.Red}{Localizer["Chat.ErrorPrefix"]}: {e.Message}");
            return;
        }

        caller.Print(
            $" {ChatColors.Green}{Localizer["Chat.YouGaveVip"]} {ChatColors.LightPurple}{target.PlayerName} {ChatColors.White}- {ChatColors.Gold}{group} {ChatColors.White}({duration.Label})");
        controller.Print(
            $" {ChatColors.Green}{Localizer["Chat.YouGotVip"]} {ChatColors.Gold}{group} {ChatColors.White}({duration.Label})");

        if (Config.AnnounceToAll)
        {
            Server.PrintToChatAll(
                $" {ChatColors.Green}[VIP] {ChatColors.LightPurple}{target.PlayerName} {ChatColors.White}{Localizer["Chat.PlayerGotVip"]} {ChatColors.Gold}{group}");
        }
    }

    // Переводит часы (единица конфига) в единицу, которую ожидает VIPCore на этом сервере (Config.CoreTimeMode).
    private int ConvertHoursToCoreTime(int hours)
    {
        return Config.CoreTimeMode switch
        {
            1 => Math.Max(1, hours * 60), // минуты
            2 => Math.Max(1, hours), // часы
            3 => Math.Max(1, hours / 24), // дни (минимум 1, чтобы короткая выдача не сгорела мгновенно)
            _ => Math.Max(1, hours * 3600), // секунды (по умолчанию в VIPCore)
        };
    }
}
