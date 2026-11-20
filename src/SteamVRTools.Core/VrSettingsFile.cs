using System.Text.Json;
using System.Text.Json.Nodes;

namespace SteamVRTools.Core;

public class VrSettingsException : Exception
{
    public VrSettingsException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>steamvr.vrsettings の driver_pico セクションを読み書きする。</summary>
public class VrSettingsFile
{
    private const string Section = "driver_pico";
    private const string EnableKey = "enable";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        IndentCharacter = '\t',
        IndentSize = 1,
    };

    private readonly string _path;

    public VrSettingsFile(string path) => _path = path;

    public string FilePath => _path;

    public Mode ReadMode()
    {
        var root = LoadRoot();
        if (root[Section] is JsonObject section
            && section[EnableKey] is JsonValue v
            && v.TryGetValue<bool>(out var enabled)
            && !enabled)
        {
            return Mode.Beyond;
        }
        return Mode.Pico;
    }

    public void WriteMode(Mode mode)
    {
        var root = LoadRoot();

        if (mode == Mode.Beyond)
        {
            if (root[Section] is not JsonObject section)
            {
                section = new JsonObject();
                root[Section] = section;
            }
            section[EnableKey] = false;
        }
        else if (root[Section] is JsonObject section)
        {
            section.Remove(EnableKey);
            if (section.Count == 0)
            {
                root.Remove(Section);
            }
        }

        // 一時ファイルに書いてから置き換え、書き込み途中の状態を残さない。
        var tmp = _path + ".tmp";
        try
        {
            File.WriteAllText(tmp, root.ToJsonString(WriteOptions));
            File.Move(tmp, _path, overwrite: true);
        }
        catch
        {
            try { File.Delete(tmp); } catch { /* 後始末の失敗は無視 */ }
            throw;
        }
    }

    private JsonObject LoadRoot()
    {
        string text;
        try
        {
            text = File.ReadAllText(_path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new VrSettingsException($"設定ファイルを読めません: {e.Message}", e);
        }

        try
        {
            return JsonNode.Parse(text) as JsonObject
                ?? throw new VrSettingsException("設定ファイルの形式が不正です(JSON オブジェクトではありません)。");
        }
        catch (JsonException e)
        {
            throw new VrSettingsException("設定ファイルの JSON が不正です。", e);
        }
    }
}
