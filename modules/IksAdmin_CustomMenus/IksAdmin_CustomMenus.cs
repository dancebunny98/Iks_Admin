using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using IksAdminApi;

namespace IksAdmin_CustomMenus;

public class CustomMenuItem
{
    // Уникален только в пределах своего родителя (в другом подменю можно повторить).
    public string Id { get; set; } = "";

    // Заголовок пункта в "обычном" состоянии (или единственный заголовок, если пункт
    // не переключатель).
    public string Title { get; set; } = "";

    // Права ИМЕННО на этот пункт (и, если это подменю, на всё что внутри него,
    // пока не переопределено ещё глубже). null/не задано - наследуется от родителя
    // (родительский пункт, а дальше вверх - от самого меню, ViewFlags в
    // CustomMenuDefinition). Формат - как везде в IksAdmin: "*" (все), "z"
    // (админ), можно комбинировать буквы, "not override" и т.д.
    public string? ViewFlags { get; set; }

    // --- Пункт-команда ---
    // Консольная команда сервера, выполняется как есть (Server.ExecuteCommand),
    // без "css_"-префикса: "mp_warmup_end", "mp_pause_match", любой ConVar и т.д.
    public string? Command { get; set; }

    // Если заданы ОБА ToggleTitle/ToggleCommand - пункт становится "переключателем":
    // после выполнения Command пункт меняет заголовок на ToggleTitle, и следующий
    // клик по ТОМУ ЖЕ пункту выполнит уже ToggleCommand и заголовок вернётся в Title.
    // Пример: Title="Pause Match"/Command="mp_pause_match",
    // ToggleTitle="Resume Match"/ToggleCommand="mp_unpause_match".
    public string? ToggleTitle { get; set; }
    public string? ToggleCommand { get; set; }

    // Сообщение в чат себе (и, если AnnounceToAll=true, всем) после выполнения.
    // Если не задано - используется заголовок выполненного действия.
    public string? FeedbackMessage { get; set; }

    // Опционально: помимо пункта меню, зарегистрировать ОБЫЧНУЮ чат-команду
    // (!Alias, /Alias, css_Alias) как быстрый ярлык к этому же действию.
    // По умолчанию null - НИКАКОЙ отдельной команды не создаётся, пункт доступен
    // ТОЛЬКО через дерево !admin. Право на алиас - то же ViewFlags, что и у пункта
    // (эффективное, с учётом наследования).
    // ВАЖНО: алиасы регистрируются только при полной загрузке/перезагрузке плагина
    // (css_plugins reload или рестарт сервера) - команда custommenus_reload (см.
    // ниже) их НЕ добавляет и НЕ убирает "на лету", только структуру самих меню.
    public string? Alias { get; set; }

    // --- Пункт-подменю ---
    // Если задано - клик открывает ЭТО вложенное меню вместо выполнения команды.
    // Command/ToggleTitle/ToggleCommand/Alias у такого пункта игнорируются.
    // Вкладывать можно сколь угодно глубоко (подменю внутри подменю внутри подменю...).
    public List<CustomMenuItem>? Submenu { get; set; }
}

public class CustomMenuDefinition
{
    // Уникальный id меню - используется как id пункта в главном меню !admin.
    public string Id { get; set; } = "";

    // Заголовок пункта в главном меню !admin И заголовок самого меню верхнего уровня.
    public string Title { get; set; } = "";

    // Права по умолчанию для ВСЕХ пунктов этого меню, у которых нет своего
    // ViewFlags (и это распространяется рекурсивно на все вложенные подменю,
    // пока где-то ниже не переопределено). "z" - обычный админский доступ.
    public string ViewFlags { get; set; } = "z";

    public List<CustomMenuItem> Items { get; set; } = new();
}

public class CustomMenusConfig : BasePluginConfig
{
    public override int Version { get; set; } = 1;

    public bool AnnounceToAll { get; set; } = true;

    // Пример-заготовка на ТРИ уровня вложенности сразу, чтобы наглядно показать,
    // что это не плоский список:
    //   Match Management (меню верхнего уровня)
    //     -> End Warmup / Pause⇄Resume Match (обычные пункты)
    //     -> Team Controls (подменю)
    //          -> CT Controls (под-подменю)
    //               -> Give CT Bonus (обычный пункт)
    //          -> T Controls (под-подменю)
    //               -> Give T Bonus (обычный пункт)
    // Смело редактируйте, добавляйте свои меню/пункты/подменю без пересборки плагина.
    public List<CustomMenuDefinition> Menus { get; set; } = new()
    {
        new CustomMenuDefinition
        {
            Id = "match_control",
            Title = "Match Management",
            ViewFlags = "z",
            Items = new List<CustomMenuItem>
            {
                new()
                {
                    Id = "end_warmup",
                    Title = "End Warmup",
                    Command = "mp_warmup_end",
                },
                new()
                {
                    Id = "pause",
                    Title = "Pause Match",
                    Command = "mp_pause_match",
                    ToggleTitle = "Resume Match",
                    ToggleCommand = "mp_unpause_match",
                    Alias = "pausematch",
                },
                new()
                {
                    Id = "team_controls",
                    Title = "Team Controls",
                    ViewFlags = "zt", // пример более узких прав, чем у родителя ("z")
                    Submenu = new List<CustomMenuItem>
                    {
                        new()
                        {
                            Id = "ct_controls",
                            Title = "CT Controls",
                            Submenu = new List<CustomMenuItem>
                            {
                                new()
                                {
                                    Id = "give_ct_bonus",
                                    Title = "Give CT Bonus Money",
                                    Command = "mp_startmoney 16000", // пример-заглушка
                                },
                            }
                        },
                        new()
                        {
                            Id = "t_controls",
                            Title = "T Controls",
                            Submenu = new List<CustomMenuItem>
                            {
                                new()
                                {
                                    Id = "give_t_bonus",
                                    Title = "Give T Bonus Money",
                                    Command = "mp_startmoney 16000", // пример-заглушка
                                },
                            }
                        },
                    }
                },
            }
        }
    };
}

public class Main : AdminModule, IPluginConfig<CustomMenusConfig>
{
    public override string ModuleName => "IksAdmin_CustomMenus";
    public override string ModuleVersion => "1.1.0";
    public override string ModuleAuthor => "iks__ modules";

    public CustomMenusConfig Config { get; set; } = new();
    public void OnConfigParsed(CustomMenusConfig config) => Config = config;

    private const string ReloadPermission = "custom_menu.reload";

    // Состояние переключателей - true значит "уже нажали, показываем ToggleTitle".
    // Ключ - полный путь до пункта (idМеню:idПункта:idПодпункта:...), поэтому
    // одинаковые Id в разных подменю никогда не путаются друг с другом.
    // Живёт в памяти, сбрасывается при перезагрузке плагина/карты - осознанно:
    // например "матч на паузе" не должно пережить рестарт сервера как факт.
    private readonly Dictionary<string, bool> _toggleState = new();

    // id всех пунктов главного меню, которые мы САМИ зарегистрировали (нужно, чтобы
    // при custommenus_reload корректно убрать те, что пропали из нового конфига).
    private readonly HashSet<string> _registeredMenuIds = new();

    public override void InitializeCommands()
    {
        RegisterMenus();

        // Алиасы регистрируются один раз при (пере)загрузке ПЛАГИНА - см. комментарий
        // у CustomMenuItem.Alias. custommenus_reload их не трогает.
        foreach (var definition in Config.Menus)
        {
            RegisterAliases(definition.Items, definition.ViewFlags, new List<string> { definition.Id });
        }

        Api.RegisterPermission(ReloadPermission, "z");
        Api.AddNewCommand(
            command: "custommenus_reload",
            description: "Перечитать конфиг IksAdmin_CustomMenus без перезагрузки плагина",
            permission: ReloadPermission,
            usage: "css_custommenus_reload",
            onExecute: OnReloadCommand,
            whoCanExecute: CommandUsage.CLIENT_AND_SERVER
        );
    }

    // Строит/перестраивает пункты в главном меню !admin по текущему Config.Menus.
    // Вызывается и при первой загрузке, и из custommenus_reload.
    private void RegisterMenus()
    {
        var newIds = new HashSet<string>();

        foreach (var definition in Config.Menus)
        {
            var def = definition; // локальная копия для замыкания
            var id = "custom_menu_" + def.Id;
            var rootPath = new List<string> { def.Id };

            Api.RegisterMainMenuOption(
                id: id,
                title: () => def.Title,
                onExecute: (caller, backMenu) =>
                    OpenMenuLevel(caller, def.Items, def.Title, backMenu, def.ViewFlags, rootPath),
                viewFlags: def.ViewFlags
            );
            newIds.Add(id);
        }

        // Меню, которые были в СТАРОМ конфиге, но пропали в новом - убираем из !admin.
        foreach (var staleId in _registeredMenuIds.Except(newIds).ToList())
        {
            Api.UnregisterMainMenuOption(staleId);
        }

        _registeredMenuIds.Clear();
        foreach (var id in newIds) _registeredMenuIds.Add(id);
    }

    private void OnReloadCommand(CCSPlayerController? caller, List<string> args, CommandInfo info)
    {
        var configPath = Path.Combine(AdminUtils.ConfigsDir, ModuleName, ModuleName + ".json");

        if (!File.Exists(configPath))
        {
            Reply(caller, $" {ChatColors.Red}Файл конфига не найден: {configPath}");
            return;
        }

        CustomMenusConfig? parsed;
        try
        {
            var json = File.ReadAllText(configPath);
            parsed = JsonSerializer.Deserialize<CustomMenusConfig>(json, new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (Exception e)
        {
            // Намеренно НЕ трогаем текущий Config, если новый файл битый - лучше
            // работать по старому конфигу, чем упасть/остаться без меню вообще.
            Reply(caller, $" {ChatColors.Red}Ошибка чтения конфига, конфиг НЕ применён: {e.Message}");
            return;
        }

        if (parsed == null)
        {
            Reply(caller, $" {ChatColors.Red}Конфиг пуст или не распознан, конфиг НЕ применён.");
            return;
        }

        Config = parsed;
        RegisterMenus();
        // Сбрасываем состояние переключателей - после ручной правки конфига
        // безопаснее показать все пункты в исходном ("не нажато") состоянии,
        // чем рисковать несостыковкой со старыми путями пунктов.
        _toggleState.Clear();

        Reply(caller,
            $" {ChatColors.Green}Конфиг IksAdmin_CustomMenus перечитан, меню обновлены. {ChatColors.White}(Alias-команды применятся только после css_plugins reload {ModuleName})");
    }

    private static void Reply(CCSPlayerController? caller, string message)
    {
        if (caller == null || !caller.IsValid)
        {
            Server.PrintToConsole(message);
        }
        else
        {
            caller.Print(message);
        }
    }

    // Рекурсивно проходит по дереву пунктов и регистрирует чат-команды для тех,
    // у кого задан Alias (в т.ч. внутри вложенных подменю).
    private void RegisterAliases(List<CustomMenuItem> items, string inheritedFlags, List<string> path)
    {
        foreach (var item in items)
        {
            var effectiveFlags = item.ViewFlags ?? inheritedFlags;
            var itemPath = new List<string>(path) { item.Id };

            if (item.Submenu != null)
            {
                RegisterAliases(item.Submenu, effectiveFlags, itemPath);
                continue;
            }

            if (string.IsNullOrEmpty(item.Alias)) continue;

            var permissionKey = "custom_menu.alias." + string.Join(".", itemPath);
            Api.RegisterPermission(permissionKey, effectiveFlags);

            var capturedItem = item;
            var capturedPath = itemPath;

            Api.AddNewCommand(
                command: item.Alias,
                description: $"Alias: {item.Title}",
                permission: permissionKey,
                usage: "css_" + item.Alias,
                onExecute: (caller, args, info) =>
                {
                    if (caller == null || !caller.IsValid) return;
                    ExecuteItem(caller, capturedItem, capturedPath, null);
                },
                whoCanExecute: CommandUsage.CLIENT_ONLY
            );
        }
    }

    private void OpenMenuLevel(CCSPlayerController caller, List<CustomMenuItem> items, string title,
        IDynamicMenu? backMenu, string inheritedFlags, List<string> path)
    {
        var menu = Api.CreateMenu(
            id: "iksadmin_custommenus:menu:" + string.Join(":", path),
            title: title,
            backMenu: backMenu
        );

        if (items.Count == 0)
        {
            menu.AddMenuOption("none", Localizer["Menu.NoItemsConfigured"], (_, _) => { }, disabled: true);
        }
        else
        {
            foreach (var item in items)
            {
                var effectiveFlags = item.ViewFlags ?? inheritedFlags;
                var itemPath = new List<string>(path) { item.Id };

                if (item.Submenu != null)
                {
                    var submenuItems = item.Submenu;
                    menu.AddMenuOption(item.Id, item.Title, (_, _) =>
                    {
                        OpenMenuLevel(caller, submenuItems, item.Title, menu, effectiveFlags, itemPath);
                    }, viewFlags: effectiveFlags);
                    continue;
                }

                var isToggled = item.ToggleTitle != null && GetToggleState(itemPath);
                var displayTitle = isToggled ? item.ToggleTitle! : item.Title;
                var capturedItem = item;

                menu.AddMenuOption(item.Id, displayTitle, (_, _) =>
                {
                    ExecuteItem(caller, capturedItem, itemPath,
                        () => OpenMenuLevel(caller, items, title, backMenu, inheritedFlags, path));
                }, viewFlags: effectiveFlags);
            }
        }

        menu.Open(caller);
    }

    private void ExecuteItem(CCSPlayerController caller, CustomMenuItem item, List<string> path, Action? reopen)
    {
        var isToggle = item.ToggleTitle != null && item.ToggleCommand != null;
        var wasToggled = isToggle && GetToggleState(path);

        var command = wasToggled ? item.ToggleCommand! : item.Command;
        var executedTitle = wasToggled ? item.ToggleTitle! : item.Title;

        if (!string.IsNullOrEmpty(command))
        {
            Server.ExecuteCommand(command);
        }

        if (isToggle)
        {
            SetToggleState(path, !wasToggled);
        }

        var feedback = item.FeedbackMessage ?? executedTitle;
        caller.Print($" {ChatColors.Green}{Localizer["Chat.Executed"]}: {ChatColors.Gold}{feedback}");

        if (Config.AnnounceToAll)
        {
            Server.PrintToChatAll(
                $" {ChatColors.Green}[CustomMenus] {ChatColors.White}{caller.PlayerName}: {ChatColors.Gold}{feedback}");
        }

        // Переоткрываем тот же уровень меню (если исполнено из меню, а не из
        // алиас-команды в чате) - для пунктов-переключателей админ сразу увидит
        // новый заголовок, а навигация "Назад" остаётся прежней.
        reopen?.Invoke();
    }

    private static string ToggleKey(List<string> path) => string.Join(":", path);
    private bool GetToggleState(List<string> path) => _toggleState.TryGetValue(ToggleKey(path), out var s) && s;
    private void SetToggleState(List<string> path, bool value) => _toggleState[ToggleKey(path)] = value;
}
