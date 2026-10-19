using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ControllersOff;

public sealed class Loc : INotifyPropertyChanged
{
    private static readonly Dictionary<string, string> English = new()
    {
        ["AppTitle"] = "Controllers Off",
        ["Devices"] = "Devices",
        ["KeepKeyboardAndMouse"] = "Keyboard & mouse",
        ["RestoreInitialState"] = "Restore initial state",
        ["EnableAll"] = "Enable all",
        ["Settings"] = "Settings",
        ["Refresh"] = "Refresh",
        ["NoDevices"] = "No input devices found",
        ["GenericDevice"] = "HID device",
        ["SwitchOn"] = "On",
        ["SwitchOff"] = "Off",
        ["Open"] = "Open",
        ["Exit"] = "Exit",
        ["CategoryKeyboard"] = "Keyboards",
        ["CategoryMouse"] = "Mice",
        ["CategoryGamepad"] = "Gamepads",
        ["CategoryWheel"] = "Wheels",
        ["CategoryCustom"] = "Other HID devices",
        ["Theme"] = "Theme",
        ["ThemeSystem"] = "Follow system",
        ["ThemeLight"] = "Light",
        ["ThemeDark"] = "Dark",
        ["StartWithWindows"] = "Start with Windows",
        ["Language"] = "Language",
        ["LanguageSystem"] = "Follow system",
        ["LanguageRussian"] = "Русский",
        ["LanguageEnglish"] = "English",
        ["DesignatedKeyboard"] = "Main keyboard",
        ["DesignatedMouse"] = "Main mouse",
        ["AnyDevice"] = "Any",
        ["Done"] = "Done",
        ["ToggleFailed"] = "Could not change the device state.",
        ["ElevationFailed"] = "Administrator rights are required.",
        ["RestoreFailed"] = "Some devices could not be restored. Check Device Manager.",
        ["RestoredDevices"] = "Restored {0} device(s).",
        ["KeptKeyboardAndMouse"] = "{0} device(s) disabled, keyboard and mouse kept.",
        ["NothingToRestore"] = "Nothing to restore.",
        ["StatusOff"] = "{0} device(s) off",
        ["StatusClean"] = "All devices on"
    };

    private static readonly Dictionary<string, string> Russian = new()
    {
        ["AppTitle"] = "Controllers Off",
        ["Devices"] = "Устройства",
        ["KeepKeyboardAndMouse"] = "Клавиатура и мышь",
        ["RestoreInitialState"] = "Вернуть как было",
        ["EnableAll"] = "Включить всё",
        ["Settings"] = "Настройки",
        ["Refresh"] = "Обновить",
        ["NoDevices"] = "Устройства ввода не найдены",
        ["GenericDevice"] = "HID-устройство",
        ["SwitchOn"] = "Вкл",
        ["SwitchOff"] = "Выкл",
        ["Open"] = "Открыть",
        ["Exit"] = "Выход",
        ["CategoryKeyboard"] = "Клавиатуры",
        ["CategoryMouse"] = "Мыши",
        ["CategoryGamepad"] = "Геймпады",
        ["CategoryWheel"] = "Рули",
        ["CategoryCustom"] = "Прочие HID-устройства",
        ["Theme"] = "Тема",
        ["ThemeSystem"] = "Как в системе",
        ["ThemeLight"] = "Светлая",
        ["ThemeDark"] = "Тёмная",
        ["StartWithWindows"] = "Запускать при старте Windows",
        ["Language"] = "Язык",
        ["LanguageSystem"] = "Как в системе",
        ["LanguageRussian"] = "Русский",
        ["LanguageEnglish"] = "English",
        ["DesignatedKeyboard"] = "Основная клавиатура",
        ["DesignatedMouse"] = "Основная мышь",
        ["AnyDevice"] = "Любая",
        ["Done"] = "Готово",
        ["ToggleFailed"] = "Не удалось изменить состояние устройства.",
        ["ElevationFailed"] = "Требуются права администратора.",
        ["RestoreFailed"] = "Не все устройства удалось вернуть. Проверьте диспетчер устройств.",
        ["RestoredDevices"] = "Возвращено устройств: {0}.",
        ["KeptKeyboardAndMouse"] = "Отключено устройств: {0}. Клавиатура и мышь оставлены.",
        ["NothingToRestore"] = "Возвращать нечего.",
        ["StatusOff"] = "Выключено устройств: {0}",
        ["StatusClean"] = "Все устройства включены"
    };

    public static Loc Instance { get; } = new();

    private string _languageTag = "en";

    public string this[string key] => (IsRussian ? Russian : English).GetValueOrDefault(key, key);

    public string Format(string key, params object[] arguments) => string.Format(System.Globalization.CultureInfo.CurrentCulture, this[key], arguments);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UseLanguage(string? languageTag)
    {
        _languageTag = languageTag ?? System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        OnPropertyChanged("Item[]");
    }

    private bool IsRussian => _languageTag.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}