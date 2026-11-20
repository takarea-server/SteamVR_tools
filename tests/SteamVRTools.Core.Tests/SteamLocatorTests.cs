using SteamVRTools.Core;

namespace SteamVRTools.Core.Tests;

public class SteamLocatorTests
{
    [Fact]
    public void Normalize_ForwardSlashes_BecomesBackslashes() =>
        Assert.Equal(@"c:\program files (x86)\steam", SteamLocator.Normalize("c:/program files (x86)/steam"));

    [Fact]
    public void Normalize_TrailingSeparator_Removed() =>
        Assert.Equal(@"c:\steam", SteamLocator.Normalize("c:/steam/"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_NullOrWhitespace_ReturnsNull(string? raw) =>
        Assert.Null(SteamLocator.Normalize(raw));

    [Fact]
    public void VrSettingsPath_CombinesConfigSubfolder() =>
        Assert.Equal(@"c:\steam\config\steamvr.vrsettings", SteamLocator.VrSettingsPath(@"c:\steam"));
}
