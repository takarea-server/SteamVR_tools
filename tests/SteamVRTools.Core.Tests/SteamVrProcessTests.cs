using SteamVRTools.Core;

namespace SteamVRTools.Core.Tests;

public class SteamVrProcessTests
{
    [Theory]
    [InlineData("vrmonitor", true)]
    [InlineData("vrserver", true)]
    [InlineData("VRMonitor", true)]
    [InlineData("steam", false)]
    [InlineData("vrcompositor", false)]
    public void IsSteamVrProcessName_MatchesOnlyMonitorAndServer(string name, bool expected) =>
        Assert.Equal(expected, SteamVrProcess.IsSteamVrProcessName(name));
}
