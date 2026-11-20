using System.Text.Json;

namespace SteamVRTools.Core;

/// <summary>アプリ自身の設定(Steam のパスを手入力した場合の上書きなど)。</summary>
public class AppSettings
{
    public string? SteamPathOverride { get; set; }

    /// <summary>既定の保存場所は %APPDATA%\SteamVRTools\。</summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SteamVRTools");

    public static string DefaultFilePath => Path.Combine(DefaultDirectory, "settings.json");

    public static string DefaultBackupDirectory => Path.Combine(DefaultDirectory, "backups");

    /// <summary>読めない、壊れている場合は既定値を返す。</summary>
    public static AppSettings Load(string filePath)
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath)) ?? new AppSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
