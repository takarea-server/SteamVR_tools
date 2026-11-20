namespace SteamVRTools.App;

/// <summary>現在のモード表示と、2つの切り替えボタンだけを持つウィンドウ。切り替え処理は持たない。</summary>
public class MainForm : Form
{
    private readonly Label _status = new() { AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold) };
    private readonly NoFocusButton _pico = new() { Text = "Pico モード", Width = 140, Height = 40 };
    private readonly NoFocusButton _beyond = new() { Text = "Beyond モード", Width = 140, Height = 40 };

    public event Action? PicoRequested;
    public event Action? BeyondRequested;

    public MainForm()
    {
        Text = "SteamVR Tools";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(340, 150);

        _status.Location = new Point(20, 20);
        _pico.Location = new Point(20, 80);
        _beyond.Location = new Point(180, 80);
        _pico.Click += (_, _) => PicoRequested?.Invoke();
        _beyond.Click += (_, _) => BeyondRequested?.Invoke();
        Controls.AddRange(new Control[] { _status, _pico, _beyond });
    }

    public void SetStatus(string text) => _status.Text = text;

    public void SetBusy(bool busy)
    {
        _pico.Enabled = !busy;
        _beyond.Enabled = !busy;
    }
}

/// <summary>キーボードフォーカスを受け取らないボタン。開いた直後の青い枠や、Enter での誤操作を防ぐ。マウス操作は通常どおり。</summary>
internal sealed class NoFocusButton : Button
{
    public NoFocusButton()
    {
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }
}
