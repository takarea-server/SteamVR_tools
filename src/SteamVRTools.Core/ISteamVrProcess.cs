namespace SteamVRTools.Core;

public interface ISteamVrProcess
{
    bool IsRunning();

    /// <summary>SteamVR を正常終了させ、timeout 以内に終了したら true。強制終了はしない。</summary>
    Task<bool> CloseAsync(TimeSpan timeout);

    void Start();
}
