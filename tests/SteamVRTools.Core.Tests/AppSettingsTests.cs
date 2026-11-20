using SteamVRTools.Core;

namespace SteamVRTools.Core.Tests;

public class AppSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "svrt-" + Guid.NewGuid().ToString("N"));

    public AppSettingsTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var path = Path.Combine(_dir, "sub", "settings.json");
        new AppSettings { SteamPathOverride = @"D:\Steam" }.Save(path);
        Assert.Equal(@"D:\Steam", AppSettings.Load(path).SteamPathOverride);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefault() =>
        Assert.Null(AppSettings.Load(Path.Combine(_dir, "nope.json")).SteamPathOverride);

    [Fact]
    public void Load_InvalidJson_ReturnsDefault()
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "{ broken");
        Assert.Null(AppSettings.Load(path).SteamPathOverride);
    }
}
