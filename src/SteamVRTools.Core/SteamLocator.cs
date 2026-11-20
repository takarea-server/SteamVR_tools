using Microsoft.Win32;

namespace SteamVRTools.Core;

public static class SteamLocator
{
    /// <summary>レジストリが返す "c:/program files (x86)/steam" のような値を Windows 形式に直す。</summary>
    public static string? Normalize(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }
        var full = Path.GetFullPath(rawPath.Trim().Replace('/', '\\'));
        return Path.TrimEndingDirectorySeparator(full);
    }

    /// <summary>HKCU\Software\Valve\Steam の SteamPath から Steam のインストール先を取る。無ければ null。</summary>
    public static string? DetectSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return Normalize(key?.GetValue("SteamPath") as string);
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public static string VrSettingsPath(string steamPath) =>
        Path.Combine(steamPath, "config", "steamvr.vrsettings");
}
