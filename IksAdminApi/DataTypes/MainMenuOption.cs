using CounterStrikeSharp.API.Core;

namespace IksAdminApi;

/// <summary>
/// Дополнительный пункт главного меню администратора (!admin), зарегистрированный
/// сторонним модулем через <see cref="IIksAdminApi.RegisterMainMenuOption"/>.
/// </summary>
/// <param name="Id">Уникальный id пункта меню.</param>
/// <param name="Title">Отображаемый текст пункта. Func, а не string - вызывается заново
/// при каждом открытии меню, как и у встроенных пунктов (актуальная локализация,
/// а не застывшая на момент регистрации модулем строка).</param>
/// <param name="OnExecute">Вызывается при выборе пункта: (игрок, само главное меню - удобно как backMenu).</param>
/// <param name="ViewFlags">Права, при которых пункт виден игроку (как в AddMenuOption).</param>
public record MainMenuOption(string Id, Func<string> Title, Action<CCSPlayerController, IDynamicMenu> OnExecute, string ViewFlags);
