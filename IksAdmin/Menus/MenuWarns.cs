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
                    if (a is not null) OpenReasonMenu(caller, a, m);
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

    private static void OpenReasonMenu(CCSPlayerController caller, Admin target, IDynamicMenu backMenu)
    {
        var menu = _api.CreateMenu(Main.MenuId("warns.reasons"),
            _localizer["MenuTitle.Other.SelectReason"], backMenu: backMenu);
        menu.AddMenuOption("own_reason", _localizer["MenuOption.Other.OwnReason"], (_, _) =>
        {
            caller.Print(_localizer["Message.PrintOwnReason"]);
            _api.HookNextPlayerMessage(caller, reason => IssueWarn(caller, target, reason, backMenu));
        });
        foreach (var reason in _api.Config.WarnReasons.Where(x => !x.HideFromMenu &&
                     !string.IsNullOrWhiteSpace(x.Title) && !string.IsNullOrWhiteSpace(x.Text)))
        {
            var text = _localizer[reason.Text].Value;
            if (caller.Admin() is not { } issuer || !AdminUtils.CanIssueWarn(issuer, target, text))
                continue;
            menu.AddMenuOption(reason.Title, _localizer[reason.Title], (_, _) =>
                IssueWarn(caller, target, text, backMenu));
        }
        menu.Open(caller);
    }

    private static void IssueWarn(CCSPlayerController caller, Admin target, string reason, IDynamicMenu backMenu)
    {
        reason = reason.Trim();
        if (reason.Length is < 3 or > 255)
        {
            caller.Print(_localizer["ActionError.WarnReasonInvalid"]);
            return;
        }
        if (caller.Admin() is not { } issuer || !AdminUtils.CanIssueWarn(issuer, target, reason))
        {
            caller.Print(_localizer["ActionError.NotEnoughPermissionsForAction"]);
            return;
        }
        var warn = new Warn(issuer.Id, target.Id, 0, reason);
        Task.Run(async () =>
        {
            try
            {
                var result = await _api.CreateWarn(warn);
                Server.NextFrame(() =>
                {
                    if (!caller.IsValid) return;
                    caller.Print(_localizer[result.QueryStatus == 0 ?
                        "Message.Warns.Created" : "ActionError.WarnSaveFailed"]);
                    backMenu.Open(caller);
                });
            }
            catch (Exception ex)
            {
                AdminUtils.LogError(ex.Message);
                Server.NextFrame(() => { if (caller.IsValid) caller.Print(_localizer["ActionError.WarnSaveFailed"]); });
            }
        });
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
                caller.Print(MsgOther.SWarnTemplate(warn));
                var details = _api.CreateMenu(Main.MenuId("warns.details"),
                    _localizer["MenuTitle.Warns.Details"], backMenu: menu);
                if (caller.Admin() is { } actor && AdminUtils.CanRemoveWarn(actor, warn))
                {
                    details.AddMenuOption("remove", _localizer["MenuOption.Warns.Remove"], (_, _) =>
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                var result = await _api.DeleteWarn(actor, warn);
                                Server.NextFrame(() =>
                                {
                                    if (!caller.IsValid) return;
                                    caller.Print(_localizer[result.QueryStatus == 0 ?
                                        "Message.Warns.Removed" : "ActionError.WarnDeleteFailed"]);
                                    SelectWarnMenu(caller, admin, backMenu, mainBack);
                                });
                            }
                            catch (Exception ex)
                            {
                                AdminUtils.LogError(ex.Message);
                                Server.NextFrame(() => { if (caller.IsValid) caller.Print(_localizer["ActionError.WarnDeleteFailed"]); });
                            }
                            });
                    });
                }
                details.AddMenuOption("reason", _localizer["MenuOption.Warns.Reason", warn.Reason],
                    (_, _) => { }, disabled: true);
                details.Open(caller);
            });
        }

        menu.Open(caller);
    }
}
