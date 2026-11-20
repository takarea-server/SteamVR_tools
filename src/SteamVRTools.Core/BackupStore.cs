namespace SteamVRTools.Core;

/// <summary>設定ファイルのバックアップを作り、世代数を超えた古いものを消す。</summary>
public class BackupStore
{
    private readonly string _dir;
    private readonly int _generations;

    public BackupStore(string backupDirectory, int generations = 5)
    {
        _dir = backupDirectory;
        _generations = Math.Max(1, generations);
    }

    /// <summary>バックアップを作り、そのパスを返す。</summary>
    public string Create(string sourceFile)
    {
        if (!File.Exists(sourceFile))
        {
            throw new FileNotFoundException("バックアップ元のファイルがありません。", sourceFile);
        }

        Directory.CreateDirectory(_dir);
        var name = Path.GetFileName(sourceFile);

        // Ticks は桁数が固定なので、名前順 = 作成順になる。同じ値が重なったら進める。
        var ticks = DateTime.UtcNow.Ticks;
        string path;
        do
        {
            path = Path.Combine(_dir, $"{name}.{ticks:D19}.bak");
            ticks++;
        } while (File.Exists(path));

        File.Copy(sourceFile, path);
        Prune(name);
        return path;
    }

    public void Restore(string backupPath, string targetFile) =>
        File.Copy(backupPath, targetFile, overwrite: true);

    private void Prune(string name)
    {
        var files = Directory.GetFiles(_dir, $"{name}.*.bak");
        Array.Sort(files, StringComparer.Ordinal);
        for (var i = 0; i < files.Length - _generations; i++)
        {
            File.Delete(files[i]);
        }
    }
}
