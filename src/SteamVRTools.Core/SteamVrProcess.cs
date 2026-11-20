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

    public bool IsRunning() =>
        Probe(MonitorName) || Probe(ServerName) || Probe(StartupName);

    public async Task<bool> CloseAsync(TimeSpan timeout)
    {
        foreach (var p in Process.GetProcessesByName(MonitorName))
        {
            using (p)
            {
                try { p.CloseMainWindow(); } catch (InvalidOperationException) { /* すでに終了 */ }
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
