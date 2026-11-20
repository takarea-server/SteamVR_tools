using SteamVRTools.Core;

namespace SteamVRTools.App;

/// <summary>トレイアイコンとウィンドウを持ち、操作を ModeSwitcher に渡す。</summary>
public class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _tray = new() { Icon = SystemIcons.Application, Visible = false };
    private readonly MainForm _form = new();
    private readonly ModeSwitcher? _switcher;
    private bool _busy;

    public TrayApp()
    {
        var vrSettingsPath = ResolveVrSettingsPath();
        if (vrSettingsPath is null)
        {
            // コンストラクターの時点では ExitThread が効かないため、メッセージループ開始後に終了する。
            Application.Idle += ExitOnFirstIdle;
            return;
        }

        _switcher = new ModeSwitcher(
            new VrSettingsFile(vrSettingsPath),
            new BackupStore(AppSettings.DefaultBackupDirectory),
            new SteamVrProcess(),
            TimeSpan.FromSeconds(30));

        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => Refresh();
        menu.Items.Add("Pico モード", null, async (_, _) => await SwitchAsync(Mode.Pico));
        menu.Items.Add("Beyond モード", null, async (_, _) => await SwitchAsync(Mode.Beyond));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("ウィンドウを開く", null, (_, _) => ShowWindow());
        menu.Items.Add("終了", null, (_, _) => Quit());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowWindow();
        _tray.Visible = true;

        _form.PicoRequested += async () => await SwitchAsync(Mode.Pico);
        _form.BeyondRequested += async () => await SwitchAsync(Mode.Beyond);
        _form.VisibleChanged += (_, _) => { if (_form.Visible) Refresh(); };
        // ×ボタンはトレイに格納するだけ。終了はトレイメニューの「終了」から。
        _form.FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                _form.Hide();
            }
        };

        Refresh();
        ShowWindow();
    }

    private void ExitOnFirstIdle(object? sender, EventArgs e)
    {
        Application.Idle -= ExitOnFirstIdle;
        ExitThread();
    }

    private static string? ResolveVrSettingsPath()
    {
        var settings = AppSettings.Load(AppSettings.DefaultFilePath);
        var steamPath = SteamLocator.Normalize(settings.SteamPathOverride) ?? SteamLocator.DetectSteamPath();

        while (steamPath is null || !File.Exists(SteamLocator.VrSettingsPath(steamPath)))
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Steam のインストール先フォルダ(config フォルダがある場所)を選んでください。",
                UseDescriptionForTitle = true,
            };
            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return null;
            }
            steamPath = SteamLocator.Normalize(dialog.SelectedPath);
            if (steamPath is not null && File.Exists(SteamLocator.VrSettingsPath(steamPath)))
            {
                settings.SteamPathOverride = steamPath;
                settings.Save(AppSettings.DefaultFilePath);
            }
            else
            {
                MessageBox.Show("steamvr.vrsettings が見つかりませんでした。別のフォルダを選んでください。", "SteamVR Tools");
            }
        }
        return SteamLocator.VrSettingsPath(steamPath);
    }

    private void Refresh()
    {
        string text;
        try
        {
            text = _switcher!.CurrentMode() == Mode.Pico ? "現在: Pico モード" : "現在: Beyond モード";
        }
        catch (VrSettingsException)
        {
            text = "現在: 不明(設定ファイルを読めません)";
        }
        _form.SetStatus(text);
        _tray.Text = text.Length > 63 ? text[..63] : text;
    }

    private void ShowWindow()
    {
        _form.Show();
        _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }

    private async Task SwitchAsync(Mode target)
    {
        if (_busy) return;
        _busy = true;
        _form.SetBusy(true);
        try
        {
            var result = await _switcher!.SwitchAsync(target, AskWhenRunning);
            Refresh();
            Report(result, target);
        }
        finally
        {
            _busy = false;
            _form.SetBusy(false);
        }
    }

    private Task<RunningChoice> AskWhenRunning()
    {
        var restart = new TaskDialogButton("再起動");
        var manual = new TaskDialogButton("手動でやる");
        var cancel = new TaskDialogButton("キャンセル");
        var page = new TaskDialogPage
        {
            Caption = "SteamVR Tools",
            Heading = "SteamVR が起動中です",
            Text = "再起動: SteamVR を終了して設定を書き換え、もう一度起動します。\n手動でやる: 設定だけ書き換えます。再起動はあなたが行います。",
            Buttons = { restart, manual, cancel },
            DefaultButton = cancel,
            AllowCancel = true,
        };
        var pressed = TaskDialog.ShowDialog(page);
        return Task.FromResult(
            pressed == restart ? RunningChoice.Restart
            : pressed == manual ? RunningChoice.Manual
            : RunningChoice.Cancel);
    }

    private void Report(SwitchResult result, Mode target)
    {
        var name = target == Mode.Pico ? "Pico" : "Beyond";
        switch (result.Outcome)
        {
            case SwitchOutcome.Failed:
                MessageBox.Show(result.ErrorMessage ?? "切り替えに失敗しました。", "SteamVR Tools", MessageBoxButtons.OK, MessageBoxIcon.Error);
                break;
            case SwitchOutcome.Cancelled:
                break;
            case SwitchOutcome.AlreadyInMode:
                _tray.ShowBalloonTip(2000, "SteamVR Tools", $"すでに {name} モードです。", ToolTipIcon.Info);
                break;
            case SwitchOutcome.SwitchedManualRestartNeeded:
                _tray.ShowBalloonTip(3000, "SteamVR Tools", $"{name} モードに切り替えました。SteamVR を再起動してください。", ToolTipIcon.Info);
                break;
            default:
                _tray.ShowBalloonTip(2000, "SteamVR Tools", $"{name} モードに切り替えました。", ToolTipIcon.Info);
                break;
        }
    }

    private void Quit()
    {
        // 切り替え中に破棄すると、完了後の更新が破棄済みの部品に触れて落ちるため、終わるまで終了しない。
        if (_busy)
        {
            _tray.ShowBalloonTip(2000, "SteamVR Tools", "切り替え中です。完了してから終了してください。", ToolTipIcon.Info);
            return;
        }

        _tray.Visible = false;
        _tray.Dispose();
        _form.Dispose();
        ExitThread();
    }
}
