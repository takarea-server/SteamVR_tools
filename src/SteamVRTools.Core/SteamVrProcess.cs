using System.Diagnostics;

namespace SteamVRTools.Core;

public class SteamVrProcess : ISteamVrProcess
{
    private const string MonitorName = "vrmonitor";
    private const string ServerName = "vrserver";
    // vrstartup は SteamVR 終了後もしばらく残り、その間 Steam はアプリを起動中とみなして起動要求を無視する。
    private const string StartupName = "vrstartup";
    private const string StartUrl = "steam://rungameid/250820";

    public static bool IsSteamVrProcessName(string name) =>
        name.Equals(MonitorName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(ServerName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(StartupName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// vrmonitor のステータスウィンドウ(Qt の Qt5QWindowIcon)だけを閉じる対象にする。
    /// Process.MainWindowHandle は、ステータスウィンドウが非表示のとき内部の非表示ウィンドウを指して、閉じる要求が無視されるため使わない。
    /// </summary>
    public static bool ShouldCloseWindow(string className, bool visible, bool hasOwner) =>
        !hasOwner && className == "Qt5QWindowIcon";

    public bool IsRunning() =>
        Probe(MonitorName) || Probe(ServerName) || Probe(StartupName);

    public async Task<bool> CloseAsync(TimeSpan timeout)
    {
        foreach (var p in Process.GetProcessesByName(MonitorName))
        {
            using (p)
            {
                try
                {
                    // 閉じる要求が 1 件も送れなかったときだけ、従来の方法を試す。
                    if (NativeWindows.PostCloseTo(p.Id, ShouldCloseWindow) == 0)
                    {
                        p.CloseMainWindow();
                    }
                }
                catch (InvalidOperationException) { /* すでに終了 */ }
            }
        }

        var deadline = DateTime.UtcNow + timeout;
        while (IsRunning())
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }
            await Task.Delay(250);
        }
        return true;
    }

    public async Task<bool> StartAsync(TimeSpan timeout)
    {
        // Steam 側の終了処理が遅れて最初の要求が無視されることがあるため、半分の時間待って立ち上がらなければ一度だけ再要求する。
        var half = TimeSpan.FromTicks(timeout.Ticks / 2);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Process.Start(new ProcessStartInfo(StartUrl) { UseShellExecute = true })?.Dispose();
            var deadline = DateTime.UtcNow + half;
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(500);
                if (IsRunning())
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool Probe(string name)
    {
        var procs = Process.GetProcessesByName(name);
        foreach (var p in procs) p.Dispose();
        return procs.Length > 0;
    }
}
