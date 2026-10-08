using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using IksAdminApi;
using Microsoft.Extensions.Localization;

namespace IksAdmin.Menus;

public static class MenuWarns
{
    private static IIksAdminApi _api = Main.AdminApi;
    private static IStringLocalizer _localizer = _api.Localizer;

    public static void OpenMain(CCSPlayerController caller, IDynamicMenu? backMenu = null)
    {
        var menu = _api.CreateMenu(
            id: Main.MenuId("warns.main"),
            title: _localizer["MenuTitle.Warns.Main"],
            backMenu: backMenu
        );
        
        menu.AddMenuOption("add",  _localizer["MenuOption.Warns.Add"], (_, _) =>
        {
            MenuUtils.SelectItem<Admin?>(caller, "warn_add", "Name", 
                _api.AllAdmins.Where(x => caller.Admin() is { } issuer &&
                    issuer.CurrentImmunity >= x.CurrentImmunity).ToList()!,
                (a, m) =>
                {
                    caller.Print(_localizer["Message.GL.ReasonSet"]);
                    _api.HookNextPlayerMessage(caller, reason =>
                    {
                        if (a is null || caller.Admin() is not { } issuer ||
                            !AdminUtils.CanIssueWarn(issuer, a, reason)) return;
                        var warn = new Warn(caller.Admin()!.Id, a!.Id, 0, reason);
                        Task.Run(async () => await _api.CreateWarn(warn));
                        m.Open(caller);
                    });
                },
                backMenu: menu, nullOption: false
            );
        }, viewFlags: _api.GetCurrentPermissionFlags("admins_manage.warn_add"));
        menu.AddMenuOption("list",  _localizer["MenuOption.Warns.List"], (_, _) =>
        {
            MenuUtils.SelectItem<Admin?>(caller, "warn_list_admin", "Name", 
                _api.AllAdmins.Where(x => x.Warns.Count > 0).ToList()!,
                (a, m) =>
                {
                    SelectWarnMenu(caller, a!, m, backMenu);
                },
                backMenu: menu, nullOption: false
            );
        }, viewFlags: 
        _api.GetCurrentPermissionFlags("admins_manage.warn_delete") +
        _api.GetCurrentPermissionFlags("admins_manage.warn_list")
        );
        menu.Open(caller);
    }



    private static void SelectWarnMenu(CCSPlayerController caller, Admin admin, IDynamicMenu backMenu, IDynamicMenu? mainBack = null)
    {
        var menu = _api.CreateMenu(
            id: Main.MenuId("warns.list"),
            title: _localizer["MenuTitle.Warns.List"],
            backMenu: backMenu
        );
        var warns = admin.Warns;

        foreach (var warn in warns)
        {
            menu.AddMenuOption(warn.Id.ToString(), $"[{warn.Id}] {warn.Reason}", (_, _) => {
                _api.RemoveNextPlayerMessageHook(caller);
                caller.Print(MsgOther.SWarnTemplate(warn));
                if (caller.Admin() is { } actor && AdminUtils.CanRemoveWarn(actor, warn))
                {
                    caller.Print(_localizer["Message.Warns.DeleteWarn"]);
                    _api.HookNextPlayerMessage(caller, (s) => {
                        if (s == "delete")
                        {
                            Task.Run(async () => {
                                await _api.DeleteWarn(actor, warn);
                            });
                            OpenMain(caller, mainBack);
                        }
                    });
                }
            });
        }

        menu.Open(caller);
    }
}
