using OhSubtitle.Enums;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace OhSubtitle.Helpers;

/// <summary>
/// 系统快捷键注册助手
/// https://www.cnblogs.com/TianFang/p/7668753.html
/// https://www.cnblogs.com/leolion/p/4693514.html
/// </summary>
public static class HotKeyHelper
{
    const int WmHotKey = 0x312;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, HotKeyModifiers fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private static int _keyId = 10;

    private static readonly Dictionary<int, HotKeyRegistration> _keyMap = new();
    private static readonly Dictionary<IntPtr, HwndSource> _hookedWindows = new();

    private sealed record HotKeyRegistration(IntPtr Hwnd, HotKeyCallBackHanlder Callback);

    public delegate void HotKeyCallBackHanlder();

    /// <summary>
    /// 注册快捷键
    /// </summary>
    /// <param name="hwnd">持有快捷键窗口的句柄</param> 
    /// <param name="fsModifiers">组合键</param>
    /// <param name="key">快捷键</param>
    /// <param name="callBack">回调函数</param>
    public static bool TryRegist(IntPtr hwnd, HotKeyModifiers fsModifiers, Key key, HotKeyCallBackHanlder callBack)
    {
        var hwndSource = HwndSource.FromHwnd(hwnd);
        if (hwndSource is null)
        {
            return false;
        }

        var hookAdded = false;
        if (!_hookedWindows.ContainsKey(hwnd))
        {
            hwndSource.AddHook(WndProc);
            _hookedWindows[hwnd] = hwndSource;
            hookAdded = true;
        }

        int id = _keyId++;

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (RegisterHotKey(hwnd, id, fsModifiers, (uint)vk))
        {
            _keyMap[id] = new HotKeyRegistration(hwnd, callBack);
            return true;
        }

        if (hookAdded)
        {
            hwndSource.RemoveHook(WndProc);
            _hookedWindows.Remove(hwnd);
        }

        return false;
    }

    /// <summary> 
    /// 快捷键消息处理 
    /// </summary> 
    static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey)
        {
            int id = wParam.ToInt32();
            if (_keyMap.TryGetValue(id, out var registration))
            {
                registration.Callback();
            }
        }
        return IntPtr.Zero;
    }

    /// <summary> 
    /// 注销快捷键 
    /// </summary> 
    /// <param name="hwnd">持有快捷键窗口的句柄</param> 
    /// <param name="callBack">回调函数</param> 
    public static void UnRegist(IntPtr hwnd, HotKeyCallBackHanlder callBack)
    {
        var registrations = _keyMap
            .Where(item => item.Value.Hwnd == hwnd && item.Value.Callback == callBack)
            .ToList();

        foreach (var registration in registrations)
        {
            UnregisterHotKey(hwnd, registration.Key);
            _keyMap.Remove(registration.Key);
        }

        if (!_keyMap.Values.Any(item => item.Hwnd == hwnd)
            && _hookedWindows.Remove(hwnd, out var hwndSource))
        {
            hwndSource.RemoveHook(WndProc);
        }
    }
}
