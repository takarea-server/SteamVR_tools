using System.Diagnostics;

namespace SteamVRTools.Core;

public class SteamVrProcess : ISteamVrProcess
{
    private const string MonitorName = "vrmonitor";
    private const string ServerName = "vrserver";
    private const string StartUrl = "steam://rungameid/250820";

    public static bool IsSteamVrProcessName(string name) =>
        name.Equals(MonitorName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(ServerName, StringComparison.OrdinalIgnoreCase);

    public bool IsRunning() =>
        Probe(MonitorName) || Probe(ServerName);

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

    public void Start() =>
        Process.Start(new ProcessStartInfo(StartUrl) { UseShellExecute = true })?.Dispose();

    private static bool Probe(string name)
    {
        var procs = Process.GetProcessesByName(name);
        foreach (var p in procs) p.Dispose();
        return procs.Length > 0;
    }
}
