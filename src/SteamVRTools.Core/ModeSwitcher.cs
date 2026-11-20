namespace SteamVRTools.Core;

public enum RunningChoice
{
    Restart,
    Manual,
    Cancel,
}

public enum SwitchOutcome
{
    AlreadyInMode,
    Switched,
    SwitchedAndRestarted,
    SwitchedManualRestartNeeded,
    Cancelled,
    Failed,
}

public record SwitchResult(SwitchOutcome Outcome, string? ErrorMessage = null);

/// <summary>バックアップ、SteamVR の終了、設定の書き換え、再起動を組み合わせてモードを切り替える。UI には依存しない。</summary>
public class ModeSwitcher
{
    private readonly VrSettingsFile _settings;
    private readonly BackupStore _backups;
    private readonly ISteamVrProcess _steamVr;
    private readonly TimeSpan _closeTimeout;

    public ModeSwitcher(VrSettingsFile settings, BackupStore backups, ISteamVrProcess steamVr, TimeSpan closeTimeout)
    {
        _settings = settings;
        _backups = backups;
        _steamVr = steamVr;
        _closeTimeout = closeTimeout;
    }

    public Mode CurrentMode() => _settings.ReadMode();

    public async Task<SwitchResult> SwitchAsync(Mode target, Func<Task<RunningChoice>> askWhenRunning)
    {
        try
        {
            if (_settings.ReadMode() == target)
            {
                return new SwitchResult(SwitchOutcome.AlreadyInMode);
            }

            var restart = false;
            var wasRunning = _steamVr.IsRunning();
            if (wasRunning)
            {
                switch (await askWhenRunning())
                {
                    case RunningChoice.Cancel:
                        return new SwitchResult(SwitchOutcome.Cancelled);
                    case RunningChoice.Restart:
                        restart = true;
                        if (!await _steamVr.CloseAsync(_closeTimeout))
                        {
                            return Fail("SteamVR が時間内に終了しませんでした。設定は変更していません。");
                        }
                        break;
                }
            }

            var writeError = WriteWithBackup(target);
            if (writeError is not null)
            {
                return Fail(writeError);
            }

            if (restart)
            {
                try
                {
                    _steamVr.Start();
                }
                catch (Exception e)
                {
                    return Fail($"設定は切り替えましたが、SteamVR を起動できませんでした: {e.Message}");
                }
                return new SwitchResult(SwitchOutcome.SwitchedAndRestarted);
            }

            return new SwitchResult(wasRunning ? SwitchOutcome.SwitchedManualRestartNeeded : SwitchOutcome.Switched);
        }
        catch (Exception e)
        {
            return Fail(e.Message);
        }
    }

    /// <summary>成功なら null、失敗ならエラーメッセージ(失敗時は元の内容に戻す)。</summary>
    private string? WriteWithBackup(Mode target)
    {
        string backup;
        try
        {
            backup = _backups.Create(_settings.FilePath);
        }
        catch (Exception e)
        {
            return $"バックアップを作れなかったため、設定は変更していません: {e.Message}";
        }

        try
        {
            _settings.WriteMode(target);
            return null;
        }
        catch (Exception e)
        {
            try
            {
                _backups.Restore(backup, _settings.FilePath);
                return $"設定を書き換えられませんでした(元の内容に戻しました): {e.Message}";
            }
            catch (Exception restoreError)
            {
                return $"設定を書き換えられず、元に戻すのにも失敗しました。バックアップ: {backup} / {e.Message} / {restoreError.Message}";
            }
        }
    }

    private static SwitchResult Fail(string message) => new(SwitchOutcome.Failed, message);
}
