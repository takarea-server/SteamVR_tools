using SteamVRTools.Core;

namespace SteamVRTools.Core.Tests;

public class SteamVrProcessTests
{
    [Theory]
    [InlineData("vrmonitor", true)]
    [InlineData("vrserver", true)]
    [InlineData("VRMonitor", true)]
    [InlineData("vrstartup", true)] // 終了後も残り、Steam が「起動中」扱いを続ける
    [InlineData("steam", false)]
    [InlineData("vrcompositor", false)]
    public void IsSteamVrProcessName_MatchesOnlyMonitorAndServer(string name, bool expected) =>
        Assert.Equal(expected, SteamVrProcess.IsSteamVrProcessName(name));
}
