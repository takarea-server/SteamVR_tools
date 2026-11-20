using System.Runtime.InteropServices;
using System.Text;

namespace SteamVRTools.Core;

/// <summary>指定プロセスのトップレベルウィンドウを列挙し、条件に合うものへ WM_CLOSE を送る。</summary>
internal static class NativeWindows
{
    private const uint WM_CLOSE = 0x0010;
    private const uint GW_OWNER = 4;

    private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc proc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>条件に合うウィンドウへ WM_CLOSE を送り、送った数を返す。</summary>
    public static int PostCloseTo(int processId, Func<string, bool, bool, bool> shouldClose)
    {
        var sent = 0;
        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid != (uint)processId)
            {
                return true;
            }
            var cls = new StringBuilder(256);
            GetClassName(hWnd, cls, cls.Capacity);
            var hasOwner = GetWindow(hWnd, GW_OWNER) != IntPtr.Zero;
            if (shouldClose(cls.ToString(), IsWindowVisible(hWnd), hasOwner) && PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero))
            {
                sent++;
            }
            return true;
        }, IntPtr.Zero);
        return sent;
    }
}
