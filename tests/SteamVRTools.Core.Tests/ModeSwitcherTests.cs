using SteamVRTools.Core;

namespace SteamVRTools.Core.Tests;

public class ModeSwitcherTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "svrt-" + Guid.NewGuid().ToString("N"));
    private readonly string _settingsPath;
    private readonly string _backupDir;
    private readonly FakeSteamVr _vr = new();

    public ModeSwitcherTests()
    {
        Directory.CreateDirectory(_dir);
        _settingsPath = Path.Combine(_dir, "steamvr.vrsettings");
        _backupDir = Path.Combine(_dir, "backups");
    }

    public void Dispose() => Directory.Delete(_dir, true);

    private const string PicoJson = "{\"steamvr\":{\"a\":1}}";

    private ModeSwitcher Create(string json = PicoJson)
    {
        File.WriteAllText(_settingsPath, json);
        return new ModeSwitcher(new VrSettingsFile(_settingsPath), new BackupStore(_backupDir), _vr, TimeSpan.FromSeconds(1));
    }

    private static Func<Task<RunningChoice>> Ask(RunningChoice c, Action? onCall = null) =>
        () => { onCall?.Invoke(); return Task.FromResult(c); };

    [Fact]
    public async Task Switch_AlreadyInTarget_NoDialogNoWrite()
    {
        var s = Create();
        _vr.Running = true;
        var asked = false;
        var before = File.ReadAllBytes(_settingsPath);

        var r = await s.SwitchAsync(Mode.Pico, Ask(RunningChoice.Restart, () => asked = true));

        Assert.Equal(SwitchOutcome.AlreadyInMode, r.Outcome);
        Assert.False(asked);
        Assert.Equal(before, File.ReadAllBytes(_settingsPath));
    }

    [Fact]
    public async Task Switch_NotRunning_WritesWithoutAsking_ReturnsSwitched()
    {
        var s = Create();
        var asked = false;
        var r = await s.SwitchAsync(Mode.Beyond, Ask(RunningChoice.Cancel, () => asked = true));

        Assert.Equal(SwitchOutcome.Switched, r.Outcome);
        Assert.False(asked);
        Assert.Equal(Mode.Beyond, s.CurrentMode());
    }

    [Fact]
    public async Task Switch_Running_Cancel_NoWrite()
    {
        var s = Create();
        _vr.Running = true;
        var r = await s.SwitchAsync(Mode.Beyond, Ask(RunningChoice.Cancel));

        Assert.Equal(SwitchOutcome.Cancelled, r.Outcome);
        Assert.Equal(Mode.Pico, s.CurrentMode());
        Assert.Empty(_vr.Calls);
    }

    [Fact]
    public async Task Switch_Running_Manual_WritesAndDoesNotClose()
    {
        var s = Create();
        _vr.Running = true;
        var r = await s.SwitchAsync(Mode.Beyond, Ask(RunningChoice.Manual));

        Assert.Equal(SwitchOutcome.SwitchedManualRestartNeeded, r.Outcome);
        Assert.Equal(Mode.Beyond, s.CurrentMode());
        Assert.Empty(_vr.Calls);
    }

    [Fact]
    public async Task Switch_Running_Restart_ClosesWritesStarts_InThatOrder()
    {
        var s = Create();
        _vr.Running = true;
        _vr.OnClose = () => Assert.Equal(Mode.Pico, s.CurrentMode()); // close 時点ではまだ書き換わっていない
        _vr.OnStart = () => Assert.Equal(Mode.Beyond, s.CurrentMode()); // start 時点では書き換え済み

        var r = await s.SwitchAsync(Mode.Beyond, Ask(RunningChoice.Restart));

        Assert.Equal(SwitchOutcome.SwitchedAndRestarted, r.Outcome);
        Assert.Equal(new[] { "close", "start" }, _vr.Calls);
    }

    [Fact]
    public async Task Switch_Running_Restart_CloseTimesOut_NoWriteNoStart_ReturnsFailed()
    {
        var s = Create();
        _vr.Running = true;
        _vr.CloseResult = false;
        var before = File.ReadAllBytes(_settingsPath);

        var r = await s.SwitchAsync(Mode.Beyond, Ask(RunningChoice.Restart));

        Assert.Equal(SwitchOutcome.Failed, r.Outcome);
        Assert.False(string.IsNullOrEmpty(r.ErrorMessage));
        Assert.Equal(before, File.ReadAllBytes(_settingsPath));
        Assert.Equal(new[] { "close" }, _vr.Calls);
    }

    [Fact]
    public async Task Switch_WriteFails_RestoresBackup_ReturnsFailed()
    {
        var s = Create();
        var before = File.ReadAllBytes(_settingsPath);

        // 読み取りは許すが置き換え(書き込み)は許さない状態にして、書き込みだけ失敗させる。
        using (new FileStream(_settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var r = await s.SwitchAsync(Mode.Beyond, Ask(RunningChoice.Cancel));
            Assert.Equal(SwitchOutcome.Failed, r.Outcome);
            Assert.False(string.IsNullOrEmpty(r.ErrorMessage));
        }

        Assert.Equal(before, File.ReadAllBytes(_settingsPath));
        Assert.Empty(_vr.Calls);
    }

    [Fact]
    public async Task Switch_InvalidJson_ReturnsFailedWithMessage_FileUnchanged()
    {
        var s = Create("{ broken");
        var r = await s.SwitchAsync(Mode.Beyond, Ask(RunningChoice.Cancel));

        Assert.Equal(SwitchOutcome.Failed, r.Outcome);
        Assert.False(string.IsNullOrEmpty(r.ErrorMessage));
        Assert.Equal("{ broken", File.ReadAllText(_settingsPath));
    }

    [Fact]
    public async Task Switch_CreatesBackupBeforeWrite()
    {
        var s = Create();
        await s.SwitchAsync(Mode.Beyond, Ask(RunningChoice.Cancel));

        var backup = Assert.Single(Directory.GetFiles(_backupDir));
        Assert.Equal(PicoJson, File.ReadAllText(backup));
    }

    private sealed class FakeSteamVr : ISteamVrProcess
    {
        public bool Running;
        public bool CloseResult = true;
        public List<string> Calls { get; } = new();
        public Action? OnClose;
        public Action? OnStart;

        public bool IsRunning() => Running;

        public Task<bool> CloseAsync(TimeSpan timeout)
        {
            Calls.Add("close");
            OnClose?.Invoke();
            if (CloseResult) Running = false;
            return Task.FromResult(CloseResult);
        }

        public void Start()
        {
            Calls.Add("start");
            OnStart?.Invoke();
        }
    }
}
