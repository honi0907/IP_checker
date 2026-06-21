namespace IPChecker.Helpers;

public static class StartupHelper
{
    private const string ShortcutName = "IP Checker.lnk";

    public static bool IsRegistered()
    {
        return File.Exists(GetShortcutPath());
    }

    public static void SetEnabled(bool enabled)
    {
        var shortcutPath = GetShortcutPath();

        if (enabled)
        {
            CreateShortcut(shortcutPath);
            return;
        }

        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }

    public static void SyncWithSetting(bool shouldRunAtStartup)
    {
        var isRegistered = IsRegistered();

        if (shouldRunAtStartup && !isRegistered)
        {
            SetEnabled(true);
        }
        else if (!shouldRunAtStartup && isRegistered)
        {
            SetEnabled(false);
        }
    }

    private static string GetShortcutPath()
    {
        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        return Path.Combine(startupFolder, ShortcutName);
    }

    private static void CreateShortcut(string shortcutPath)
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("実行ファイルのパスを取得できません。");

        var workingDirectory = Path.GetDirectoryName(exePath)
            ?? throw new InvalidOperationException("作業ディレクトリを取得できません。");

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell を利用できません。");

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = exePath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.Description = "IP Checker";
        shortcut.Save();
    }
}
