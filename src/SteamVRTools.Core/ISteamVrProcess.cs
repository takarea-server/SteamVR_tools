namespace SteamVRTools.Core;

public interface ISteamVrProcess
{
    bool IsRunning();

    /// <summary>SteamVR を正常終了させ、timeout 以内に終了したら true。強制終了はしない。</summary>
    Task<bool> CloseAsync(TimeSpan timeout);

    /// <summary>SteamVR を起動し、実際に立ち上がったら true。立ち上がらなければ一度だけ起動し直し、それでもだめなら false。</summary>
    Task<bool> StartAsync(TimeSpan timeout);
}
