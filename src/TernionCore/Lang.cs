namespace TernionCore;

/// <summary>Простой словарь строк интерфейса на RU/EN.
/// Язык по умолчанию — English (EN), как и требует пользователь: первый запуск на английском.</summary>
public sealed class Lang
{
    public static string Current = "en";

    public static string T(string key) =>
        (Current == "ru" && Ru.TryGetValue(key, out var r) ? r : (En.TryGetValue(key, out var e) ? e : "[" + key + "]"));

    private static readonly Dictionary<string, string> En = new()
    {
        ["settings.title"] = "Overlay Settings",
        ["settings.port"] = "TeamSpeak Port:",
        ["settings.port.hint"] = "See «Remote Apps» in the client",
        ["settings.subtitle"] = "Display TeamSpeak participants in-game",
        ["overlay.wait.client"] = "Waiting for TeamSpeak on port",
        ["overlay.connection.error"] = "Cannot connect on port",
        ["overlay.wait.auth"] = "Waiting for Remote Apps permission…",
        ["overlay.no.server"] = "Connect to a TeamSpeak server",
        ["overlay.no.channel"] = "Waiting for channel data…",
        ["overlay.empty"] = "No participants in this channel",
        ["overlay.silent"] = "Nobody is speaking now",
        ["settings.show"] = "Show:",
        ["settings.show.all"] = "All channel participants",
        ["settings.show.talking"] = "Only speakers",
        ["settings.names"] = "Names:",
        ["settings.names.both"] = "Nick + real name from description",
        ["settings.names.nick"] = "Nickname only",
        ["settings.alignment"] = "Align capsules:",
        ["settings.alignment.left"] = "Left",
        ["settings.alignment.right"] = "Right",
        ["settings.avatar"] = "Avatar:",
        ["settings.avatar.right"] = "Right",
        ["settings.avatar.left"] = "Left",
        ["settings.avatar.none"] = "No avatar",
        ["settings.opacity"] = "List background opacity:",
        ["settings.background"] = "Semi-transparent background behind the list",
        ["settings.autostart.win"] = "Auto-start when Windows starts",
        ["settings.autostart.ts"] = "Wait for TeamSpeak connection",
        ["settings.clickthrough"] = "Clicks pass through to the game (click-through)",
        ["settings.hotkey"] = "Show/hide hotkey:",
        ["settings.fontsize"] = "Font size:",
        ["settings.width"] = "Panel width:",
        ["settings.language"] = "Language / Язык:",
        ["section.general"] = "General",
        ["section.connection"] = "Connection",
        ["section.display"] = "Display",
        ["section.behavior"] = "Behavior",
        ["section.hotkeys"] = "Hotkey & Appearance",
        ["settings.save"] = "Save",
        ["settings.cancel"] = "Cancel",
        ["tray.settings"] = "Settings…",
        ["tray.move"] = "Move overlay (unlock)",
        ["tray.lock"] = "Lock overlay (click-through)",
        ["tray.show"] = "Show / hide",
        ["tray.exit"] = "Exit",
        ["tray.tooltip"] = "Ternion Overlay — TeamSpeak 6",
    };

    private static readonly Dictionary<string, string> Ru = new()
    {
        ["settings.title"] = "Настройки оверлея",
        ["settings.port"] = "Порт TeamSpeak:",
        ["settings.port.hint"] = "См. «Удаленные приложения» в клиенте",
        ["settings.subtitle"] = "Показ участников TeamSpeak в игре",
        ["overlay.wait.client"] = "Ожидание TeamSpeak на порту",
        ["overlay.connection.error"] = "Нет связи на порту",
        ["overlay.wait.auth"] = "Ожидание разрешения Remote Apps…",
        ["overlay.no.server"] = "Подключись к серверу в TS6",
        ["overlay.no.channel"] = "Ожидание данных канала…",
        ["overlay.empty"] = "В этом канале нет участников",
        ["overlay.silent"] = "Сейчас никто не говорит",
        ["settings.show"] = "Показывать:",
        ["settings.show.all"] = "Всех участников канала",
        ["settings.show.talking"] = "Только говорящих",
        ["settings.names"] = "Имена:",
        ["settings.names.both"] = "Ник + настоящее имя из описания",
        ["settings.names.nick"] = "Только ник",
        ["settings.alignment"] = "Выравнивание капсул:",
        ["settings.alignment.left"] = "По левому краю",
        ["settings.alignment.right"] = "По правому краю",
        ["settings.avatar"] = "Аватар:",
        ["settings.avatar.right"] = "Справа",
        ["settings.avatar.left"] = "Слева",
        ["settings.avatar.none"] = "Без аватара",
        ["settings.opacity"] = "Прозрачность фона списка:",
        ["settings.background"] = "Полупрозрачный фон за списком",
        ["settings.autostart.win"] = "Автозапуск при входе в Windows",
        ["settings.autostart.ts"] = "Ожидать подключения TeamSpeak",
        ["settings.clickthrough"] = "Клики проходят в игру (окно насквозь)",
        ["settings.hotkey"] = "Показать/скрыть (Alt+T):",
        ["settings.fontsize"] = "Размер шрифта:",
        ["settings.width"] = "Ширина панели:",
        ["settings.language"] = "Язык / Language:",
        ["section.general"] = "Общее",
        ["section.connection"] = "Подключение",
        ["section.display"] = "Отображение",
        ["section.behavior"] = "Поведение",
        ["section.hotkeys"] = "Горячая клавиша и вид",
        ["settings.save"] = "Сохранить",
        ["settings.cancel"] = "Отмена",
        ["tray.settings"] = "Настройки…",
        ["tray.move"] = "Переместить оверлей (разблокировать)",
        ["tray.lock"] = "Заблокировать оверлей (клики насквозь)",
        ["tray.show"] = "Показать / скрыть",
        ["tray.exit"] = "Выход",
        ["tray.tooltip"] = "Ternion Overlay — TeamSpeak 6",
    };

    public static void Apply(string lang)
    {
        if (lang == "ru") Current = "ru";
        else if (lang == "en") Current = "en";
    }
}