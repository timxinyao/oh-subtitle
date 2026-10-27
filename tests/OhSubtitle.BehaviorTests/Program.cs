using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using OhSubtitle;

internal static class Program
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private static int _failed;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            if (args.Length != 1 || args[0] != "--regions-only")
            {
                Console.Error.WriteLine("支持的参数：--regions-only");
                return 2;
            }

            Run("屏幕像素区域边界", CheckRegionBoundaries);
            return _failed == 0 ? 0 : 1;
        }

        if (!GetCursorPos(out var originalCursor))
        {
            Console.Error.WriteLine("无法保存鼠标位置。");
            return 1;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 50,
            Top = 50,
            Width = 400,
            Height = 120,
            ShowInTaskbar = false
        };

        try
        {
            var handle = new WindowInteropHelper(window).EnsureHandle();
            Field("_windowHandle").SetValue(window, handle);
            if (!SetWindowPos(handle, IntPtr.Zero, 50, 50, 400, 120, SwpNoZOrder | SwpNoActivate)
                || !GetWindowRect(handle, out var rect))
            {
                throw new InvalidOperationException("无法设置测试窗口的屏幕像素位置。");
            }

            Run("进入学习模式时左侧保留遮罩", () => CheckInitialState(window, rect, 20, false));
            Run("进入学习模式时中央揭示并穿透", () => CheckInitialState(window, rect, 200, true));
            Run("进入学习模式时右侧保留遮罩", () => CheckInitialState(window, rect, 350, false));
            Run("轮询在 20% 边界揭示，在 80% 边界遮挡", () =>
            {
                MoveCursor(rect.Left + 80, rect.Top + 40);
                Invoke(window, "StudyModeTimer_Tick", null, EventArgs.Empty);
                AssertReveal(window, true);
                MoveCursor(rect.Left + 320, rect.Top + 40);
                Invoke(window, "StudyModeTimer_Tick", null, EventArgs.Empty);
                AssertReveal(window, false);
            });
            Run("侧边保留所选主题和不透明度", () =>
            {
                Invoke(window, "SetWindowColor", Brushes.DimGray, Brushes.White);
                Invoke(window, "SetNormalOpacity", 0.37d);
                CheckInitialState(window, rect, 20, false);
                Assert(Equals(window.Background, Brushes.DimGray), "侧边背景没有保持主题色。");
                Assert(window.Opacity == 0.37d, $"侧边不透明度应为 0.37，实际为 {window.Opacity}。");
            });
            Run("学习模式侧边和右侧按钮显示指针", () =>
            {
                MoveCursor(rect.Left + 20, rect.Top + 40);
                Invoke(window, "SetStudyMode", true);
                Assert(window.Cursor == Cursors.Arrow, $"窗口指针应为 Arrow，实际为 {window.Cursor}。");
                foreach (var name in new[] { "gridMinimize", "gridEye", "gridStudyMode" })
                {
                    var control = (Grid)Field(name).GetValue(window)!;
                    Assert(control.Cursor == Cursors.Hand, $"{name} 的指针不是 Hand。");
                }
            });
            Run("学习模式允许最小化", () =>
                Assert(window.ResizeMode == ResizeMode.CanMinimize, $"实际 ResizeMode: {window.ResizeMode}"));
            Run("屏幕像素区域边界", CheckRegionBoundaries);
            Run("拖动期间轮询不能揭示，结束后按位置恢复", () =>
            {
                CheckInitialState(window, rect, 20, false);
                Field("_isDraggingInStudyMode").SetValue(window, true);
                try
                {
                    MoveCursor(rect.Left + 200, rect.Top + 40);
                    Invoke(window, "StudyModeTimer_Tick", null, EventArgs.Empty);
                    AssertReveal(window, false);
                }
                finally
                {
                    Field("_isDraggingInStudyMode").SetValue(window, false);
                }
                Invoke(window, "StudyModeTimer_Tick", null, EventArgs.Empty);
                AssertReveal(window, true);
            });
            Run("最小化时遮挡，恢复后继续检测", () =>
            {
                CheckInitialState(window, rect, 200, true);
                Invoke(window, "SetMinimizedState", true);
                AssertReveal(window, false);
                Invoke(window, "SetMinimizedState", false);
                Invoke(window, "StudyModeTimer_Tick", null, EventArgs.Empty);
                AssertReveal(window, true);
            });
            Run("退出学习模式恢复遮罩及原尺寸模式", () =>
            {
                CheckInitialState(window, rect, 200, true);
                Invoke(window, "SetStudyMode", false);
                AssertReveal(window, false);
                Assert(window.ResizeMode == ResizeMode.CanResizeWithGrip, $"实际 ResizeMode: {window.ResizeMode}");
                Assert(Equals(window.Background, Brushes.DimGray), "退出学习模式后没有恢复主题色。");
                Assert(window.Opacity == 0.37d, $"退出学习模式后不透明度为 {window.Opacity}。");
            });
            Run("学习模式按钮事件不冒泡到窗口拖动", () =>
            {
                MoveCursor(rect.Left + 350, rect.Top + 40);
                Invoke(window, "SetStudyMode", true);
                var control = (Grid)Field("gridStudyMode").GetValue(window)!;
                var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = Mouse.MouseDownEvent
                };
                control.RaiseEvent(args);
                Assert(!(bool)Field("_isStudyMode").GetValue(window)!, "按钮未切出学习模式。");
                Assert(args.Handled, "按钮鼠标事件没有标记为已处理。");
            });
        }
        finally
        {
            SetCursorPos(originalCursor.X, originalCursor.Y);
            // 不触发应用的关闭处理，避免测试改写用户设置。
            var handler = (System.ComponentModel.CancelEventHandler)Delegate.CreateDelegate(
                typeof(System.ComponentModel.CancelEventHandler), window, Method("Window_Closing"));
            window.Closing -= handler;
            window.Close();
            app.Shutdown();
        }

        Console.WriteLine(_failed == 0 ? "所有行为测试通过。" : $"{_failed} 项行为测试失败。");
        return _failed == 0 ? 0 : 1;
    }

    private static void CheckInitialState(MainWindow window, NativeRect rect, int xOffset, bool revealed)
    {
        Invoke(window, "SetStudyMode", false);
        MoveCursor(rect.Left + xOffset, rect.Top + 40);
        Invoke(window, "SetStudyMode", true);
        AssertReveal(window, revealed);
    }

    private static void CheckRegionBoundaries()
    {
        var type = typeof(MainWindow).Assembly.GetType("OhSubtitle.Helpers.StudyModeHitTest")
            ?? throw new InvalidOperationException("缺少学习模式区域判定。");
        var classify = type.GetMethod("Classify", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("缺少学习模式区域分类方法。");
        var cases = new (int left, int top, int right, int bottom, int x, int y, string expected)[]
        {
            (-400, -100, 0, 20, -400, -100, "Drag"),
            (-400, -100, 0, 20, -321, -1, "Drag"),
            (-400, -100, 0, 20, -320, -1, "Reveal"),
            (-400, -100, 0, 20, -81, -1, "Reveal"),
            (-400, -100, 0, 20, -80, -1, "Drag"),
            (-400, -100, 0, 20, -1, 19, "Drag"),
            (-400, -100, 0, 20, -401, -1, "Outside"),
            (-400, -100, 0, 20, 0, -1, "Outside"),
            (-400, -100, 0, 20, -300, -101, "Outside"),
            (-400, -100, 0, 20, -300, 20, "Outside"),
            (-300, -50, 101, 50, -220, 0, "Drag"),
            (-300, -50, 101, 50, -219, 0, "Reveal"),
            (-300, -50, 101, 50, 20, 0, "Reveal"),
            (-300, -50, 101, 50, 21, 0, "Drag"),
            (-300, -50, 103, 50, -220, 0, "Drag"),
            (-300, -50, 103, 50, -219, 0, "Reveal"),
            (-300, -50, 103, 50, 22, 0, "Reveal"),
            (-300, -50, 103, 50, 23, 0, "Drag"),
            (10, 10, 10, 20, 10, 10, "Outside"),
            (int.MinValue, -1, int.MaxValue, 1, -1288490190, 0, "Drag"),
            (int.MinValue, -1, int.MaxValue, 1, -1288490189, 0, "Reveal"),
            (int.MinValue, -1, int.MaxValue, 1, 1288490187, 0, "Reveal"),
            (int.MinValue, -1, int.MaxValue, 1, 1288490188, 0, "Drag")
        };

        var failures = new List<string>();
        foreach (var c in cases)
        {
            var actual = classify.Invoke(null, new object[] { c.left, c.top, c.right, c.bottom, c.x, c.y })?.ToString();
            if (actual != c.expected)
            {
                failures.Add($"窗口 ({c.left},{c.top},{c.right},{c.bottom}) 点 ({c.x},{c.y}) 预期 {c.expected}，实际 {actual}");
            }
        }
        Assert(failures.Count == 0, string.Join("；", failures));
    }

    private static void AssertReveal(MainWindow window, bool expected)
    {
        var revealed = (bool)Field("_isRevealed").GetValue(window)!;
        var clickThrough = (bool)Field("_isClickThrough").GetValue(window)!;
        Assert(revealed == expected && clickThrough == expected,
            $"预期揭示/穿透为 {expected}，实际揭示={revealed}、穿透={clickThrough}。");
    }

    private static void MoveCursor(int x, int y)
    {
        if (!SetCursorPos(x, y) || !GetCursorPos(out var actual) || actual.X != x || actual.Y != y)
        {
            throw new InvalidOperationException($"无法将鼠标移动到测试坐标 ({x}, {y})。");
        }
    }

    private static FieldInfo Field(string name) => typeof(MainWindow).GetField(name, InstancePrivate)
        ?? throw new MissingFieldException(typeof(MainWindow).FullName, name);

    private static MethodInfo Method(string name) => typeof(MainWindow).GetMethod(name, InstancePrivate)
        ?? throw new MissingMethodException(typeof(MainWindow).FullName, name);

    private static void Invoke(MainWindow window, string name, params object?[] arguments) =>
        Method(name).Invoke(window, arguments);

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"FAIL {name}: {ex.GetBaseException().Message}");
        }
    }
}
