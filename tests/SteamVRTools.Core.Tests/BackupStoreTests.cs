using SteamVRTools.Core;

namespace SteamVRTools.Core.Tests;

public class BackupStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "svrt-" + Guid.NewGuid().ToString("N"));
    private readonly string _backups;

    public BackupStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _backups = Path.Combine(_dir, "backups");
    }

    public void Dispose() => Directory.Delete(_dir, true);

    private string Source(string content)
    {
        var p = Path.Combine(_dir, "steamvr.vrsettings");
        File.WriteAllText(p, content);
        return p;
    }

    [Fact]
    public void Create_CopiesFileContent()
    {
        var src = Source("hello");
        var backup = new BackupStore(_backups).Create(src);
        Assert.Equal("hello", File.ReadAllText(backup));
        Assert.NotEqual(src, backup);
    }

    [Fact]
    public void Create_BeyondGenerations_DeletesOldest()
    {
        var src = Source("v1");
        var store = new BackupStore(_backups, generations: 2);
        var first = store.Create(src);
        File.WriteAllText(src, "v2");
        var second = store.Create(src);
        File.WriteAllText(src, "v3");
        var third = store.Create(src);

        Assert.False(File.Exists(first));
        Assert.True(File.Exists(second));
        Assert.True(File.Exists(third));
        Assert.Equal(2, Directory.GetFiles(_backups).Length);
    }

    [Fact]
    public void Restore_OverwritesTargetWithBackup()
    {
        var src = Source("original");
        var store = new BackupStore(_backups);
        var backup = store.Create(src);
        File.WriteAllText(src, "changed");
        store.Restore(backup, src);
        Assert.Equal("original", File.ReadAllText(src));
    }

    [Fact]
    public void Create_MissingSource_Throws()
    {
        var store = new BackupStore(_backups);
        Assert.Throws<FileNotFoundException>(() => store.Create(Path.Combine(_dir, "nope")));
    }
}
