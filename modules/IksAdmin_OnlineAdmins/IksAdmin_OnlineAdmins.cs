using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using IksAdminApi;

namespace IksAdmin_OnlineAdmins;

public class Main : AdminModule
{
    public override string ModuleName => "IksAdmin_OnlineAdmins";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "iks__ modules";

    // "info.online_admins" - право по умолчанию "*", то есть команда доступна
    // ВСЕМ игрокам, а не только админам: список онлайн-админов - справочная
    // информация для всех. Если нужно ограничить только админами - поменяйте
    // "*" на нужный флаг ниже, либо переопределите право через
    // PermissionReplacement в конфиге ядра (configs/plugins/IksAdmin/config.json),
    // не трогая код модуля.
    private const string Permission = "info.online_admins";

    public override void InitializeCommands()
    {
        Api.RegisterPermission(Permission, "*");

        Api.AddNewCommand(
            command: "admins",
            description: "Показать список админов онлайн",
            permission: Permission,
            usage: "css_admins",
            onExecute: OnAdminsCommand,
            whoCanExecute: CommandUsage.CLIENT_ONLY
        );
    }

    private void OnAdminsCommand(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        if (caller == null || !caller.IsValid) return;
        ShowOnlineAdmins(caller);
    }

    private void ShowOnlineAdmins(CCSPlayerController caller)
    {
        // ServerAdmins - админы, привязанные к этому серверу.
        // Admin.Online уже учитывает, подключен ли игрок с этим SteamID сейчас.
        // IsDisabled - исключает деактивированных/выключенных по варнам админов.
        // HidenAdmins - исключает админов, включивших /hide (они не должны светиться как админы).
        var admins = Api.ServerAdmins.Values
            .Where(admin => admin.Online && !admin.IsDisabled && !Api.HidenAdmins.Contains(admin))
            .OrderByDescending(admin => admin.CurrentImmunity)
            .ToList();

        var menu = Api.CreateMenu(
            id: "iksadmin_onlineadmins:menu:list",
            title: Localizer["MenuTitle.OnlineAdmins"]
        );

        if (admins.Count == 0)
        {
            menu.AddMenuOption(
                id: "none",
                title: Localizer["Menu.NoAdminsOnline"],
                (_, _) => { },
                disabled: true
            );
        }
        else
        {
            foreach (var admin in admins)
            {
                var groupName = admin.Group?.Name ?? Localizer["Menu.NoGroup"];
                menu.AddMenuOption(
                    id: admin.SteamId,
                    title: $"{admin.CurrentName}  ({groupName})",
                    (_, _) => { },
                    disabled: true
                );
            }
        }

        menu.Open(caller);
    }
}
