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

public class VrMonitorWindowTests
{
    [Theory]
    [InlineData("Qt5QWindowIcon", true, false, true)]   // 表示中のステータスウィンドウ
    [InlineData("Qt5QWindowIcon", false, false, true)]  // 非表示でもステータスウィンドウ(MainWindow は別の窓を指すことがある)
    [InlineData("Qt5QWindowToolSaveBits", false, false, false)] // 内部の非表示ウィンドウには送らない
    [InlineData("QTrayIconMessageWindowClass", false, false, false)]
    [InlineData("IME", false, true, false)]
    [InlineData("Qt5QWindowIcon", true, true, false)]   // 親(owner)付きの子ウィンドウは対象外
    public void ShouldClose_SelectsOnlyTheStatusWindow(string className, bool visible, bool hasOwner, bool expected) =>
        Assert.Equal(expected, SteamVrProcess.ShouldCloseWindow(className, visible, hasOwner));
}
