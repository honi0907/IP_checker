using System.Text.Json;
using IPChecker.Models;

namespace IPChecker.Helpers;

public static class SettingsHelper
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IPChecker");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    public static bool IsAlwaysOnTop
    {
        get => Load().IsAlwaysOnTop;
        set => Update(settings => settings.IsAlwaysOnTop = value);
    }

    public static bool StartInTrayOnly
    {
        get => Load().StartInTrayOnly;
        set => Update(settings => settings.StartInTrayOnly = value);
    }

    public static bool RunAtStartup
    {
        get => Load().RunAtStartup;
        set => Update(settings => settings.RunAtStartup = value);
    }

    public static int StartupDelaySeconds
    {
        get => Load().StartupDelaySeconds;
        set => Update(settings => settings.StartupDelaySeconds = Math.Clamp(value, 0, 120));
    }

    public static DisplayMode DisplayMode
    {
        get => Load().DisplayMode;
        set => Update(settings => settings.DisplayMode = value);
    }

    public static bool ShowVirtualAdapters
    {
        get => Load().ShowVirtualAdapters;
        set => Update(settings => settings.ShowVirtualAdapters = value);
    }

    public static WindowAnchor WindowAnchor
    {
        get => Load().WindowAnchor;
        set => Update(settings => settings.WindowAnchor = value);
    }

    public static bool EnableEfficiencyMode
    {
        get => Load().EnableEfficiencyMode;
        set => Update(settings => settings.EnableEfficiencyMode = value);
    }

    public static bool MiniRotateAdapters
    {
        get => Load().MiniRotateAdapters;
        set => Update(settings => settings.MiniRotateAdapters = value);
    }

    public static int MiniRotateIntervalSeconds
    {
        get => Load().MiniRotateIntervalSeconds;
        set => Update(settings => settings.MiniRotateIntervalSeconds = Math.Clamp(value, 2, 60));
    }

    public static DateTime? LastUpdateCheckUtc
    {
        get => Load().LastUpdateCheckUtc;
        set => Update(settings => settings.LastUpdateCheckUtc = value);
    }

    public static (int X, int Y)? GetWindowPosition()
    {
        var settings = Load();

        if (settings.WindowX is int x && settings.WindowY is int y)
        {
            return (x, y);
        }

        return null;
    }

    public static void SaveWindowPosition(int x, int y)
    {
        Update(settings =>
        {
            settings.WindowX = x;
            settings.WindowY = y;
        });
    }

    public static void SetCustomWindowPosition(int x, int y)
    {
        Update(settings =>
        {
            settings.WindowAnchor = WindowAnchor.Custom;
            settings.WindowX = x;
            settings.WindowY = y;
        });
    }

    private static void Update(Action<SettingsData> apply)
    {
        var settings = Load();
        apply(settings);
        Save(settings);
    }

    private static SettingsData Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
        }
        catch
        {
            // Ignore corrupt settings and fall back to defaults.
        }

        return new SettingsData();
    }

    private static void Save(SettingsData settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings);
        File.WriteAllText(SettingsFilePath, json);
    }

    private sealed class SettingsData
    {
        public int? WindowX { get; set; }

        public int? WindowY { get; set; }

        public bool IsAlwaysOnTop { get; set; } = true;

        public bool StartInTrayOnly { get; set; }

        public bool RunAtStartup { get; set; }

        public int StartupDelaySeconds { get; set; }

        public DisplayMode DisplayMode { get; set; } = DisplayMode.Mini;

        public bool ShowVirtualAdapters { get; set; }

        public WindowAnchor WindowAnchor { get; set; } = WindowAnchor.TopRight;

        public bool EnableEfficiencyMode { get; set; } = true;

        public bool MiniRotateAdapters { get; set; } = true;

        public int MiniRotateIntervalSeconds { get; set; } = 4;

        public DateTime? LastUpdateCheckUtc { get; set; }
    }
}
