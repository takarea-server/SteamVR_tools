namespace SteamVRTools.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        // 多重起動を防ぐ。2つ目は何もせず終了する。
        using var mutex = new Mutex(true, @"Local\SteamVRTools.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
