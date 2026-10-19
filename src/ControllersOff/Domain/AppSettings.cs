namespace ControllersOff.Domain;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    public bool StartWithWindows { get; set; }

    public string? DesignatedKeyboardInstanceId { get; set; }

    public string? DesignatedMouseInstanceId { get; set; }

    public string? Language { get; set; }
}