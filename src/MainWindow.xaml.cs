using OhSubtitle.Enums;
using OhSubtitle.Helpers;
using OhSubtitle.Services;
using OhSubtitle.Services.Implements;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OhSubtitle;

public partial class MainWindow : Window
{
    /// <summary>
    /// 窗体透明时的不透明度
    /// </summary>
    const double WINDOW_MINIUMU_OPACITY = 0.15d;

    /// <summary>
    /// 窗体默认宽度
    /// </summary>
    const double WINDOW_DEFAULT_WIDTH = 860;

    /// <summary>
    /// 窗体默认高度
    /// </summary>
    const double WINDOW_DEFAULT_HEIGHT = 65;

    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private static readonly IntPtr _hwndTopMost = new(-1);

    /// <summary>
    /// 窗体句柄
    /// </summary>
    private IntPtr _windowHandle;

    /// <summary>
    /// 学习模式下检测鼠标屏幕坐标的定时器
    /// </summary>
    private readonly DispatcherTimer _studyModeTimer;

    /// <summary>
    /// 当前是否处于学习模式
    /// </summary>
    private bool _isStudyMode;

    /// <summary>
    /// 当前遮罩是否因鼠标位于窗口内而揭示
    /// </summary>
    private bool _isRevealed;

    /// <summary>
    /// 当前窗口是否开启了 Win32 鼠标穿透
    /// </summary>
    private bool _isClickThrough;

    /// <summary>
    /// 鼠标是否位于右侧常驻控制区；该区域用于在学习模式下保留退出入口。
    /// </summary>
    private bool _isControlAreaHovered;

    /// <summary>
    /// 编辑模式下应恢复的遮罩透明度
    /// </summary>
    private double _normalOpacity = 1d;

    private readonly HotKeyHelper.HotKeyCallBackHanlder _switchOpacityHotKey;
    private readonly HotKeyHelper.HotKeyCallBackHanlder _switchStudyModeHotKey;

    /// <summary>
    /// 该线程负责周期性地将窗体设为置顶（与视频播放器争夺 TopMost）
    /// </summary>
    private Thread? _setTopMostThread;

    /// <summary>    
    /// 倒计时结束时将笔记区域设为隐藏
    /// </summary>
    private readonly DispatcherTimer _gridMainHiddenTimer;

    /// <summary>
    /// 是否退出
    /// </summary>
    private bool _isExit = false;

    /// <summary>
    /// 笔记服务，用于记笔记
    /// </summary>
    private INoteService _noteService;

    /// <summary>
    /// 主题颜色
    /// </summary>
    private ThemeColors _themeColor;

    /// <summary>
    /// 当前主题对应的窗口背景色，揭示状态下暂时切换为透明。
    /// </summary>
    private Brush _normalBackground = Brushes.Black;

    /// <summary>
    /// 主题颜色
    /// </summary>
    protected ThemeColors ThemeColor
    {
        get
        {
            return _themeColor;
        }

        set
        {
            _themeColor = value;

            menuThemeColorWhite.IsChecked = false;
            menuThemeColorDimGray.IsChecked = false;
            menuThemeColorLightGray.IsChecked = false;
            menuThemeColorBlack.IsChecked = false;

            switch (_themeColor)
            {
                case ThemeColors.White:
                    SetWindowColor(Brushes.White, Brushes.Black);
                    menuThemeColorWhite.IsChecked = true;
                    break;
                case ThemeColors.LightGray:
                    SetWindowColor(Brushes.LightGray, Brushes.Black);
                    menuThemeColorLightGray.IsChecked = true;
                    break;
                case ThemeColors.DimGray:
                    SetWindowColor(Brushes.DimGray, Brushes.White);
                    menuThemeColorDimGray.IsChecked = true;
                    break;
                case ThemeColors.Black:
                default:
                    SetWindowColor(Brushes.Black, Brushes.FloralWhite);
                    menuThemeColorBlack.IsChecked = true;
                    break;
            }
        }
    }

    public MainWindow()
    {
        _switchOpacityHotKey = ToggleOpacity;
        _switchStudyModeHotKey = ToggleStudyMode;

        _gridMainHiddenTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _gridMainHiddenTimer.Tick += new EventHandler((_, _) =>
        {
            if (!_isExit && !_isStudyMode && gridNoteArea != null)
            {
                // 隐藏输入框与笔记按钮，右侧控制区始终保留。
                gridNoteArea.Visibility = Visibility.Hidden;
                // 隐藏右下角三角标
                ResizeMode = ResizeMode.NoResize;
            }
        });

        _studyModeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _studyModeTimer.Tick += StudyModeTimer_Tick;

        InitializeComponent();

        LoadSettingsAndInitializeServices();

        txtInput.Text = "可在此处输入笔记 / 按 Ctrl+Q 切换不透明度 / 按 Ctrl+Shift+Q 切换学习模式 / 右击悬浮窗边缘可打开菜单";
        gridNoteWrote.Visibility = Visibility.Hidden;
        gridWriteNote.Visibility = Visibility.Hidden;
        gridReset.Visibility = Visibility.Hidden;
    }

    /// <summary>
    /// 窗体
    /// 加载完毕
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _normalOpacity = Opacity;

        // 注册系统快捷键
        var opacityHotKeyRegistSuccess = HotKeyHelper.TryRegist(_windowHandle, HotKeyModifiers.Ctrl, Key.Q, _switchOpacityHotKey);
        var studyModeHotKeyRegistSuccess = HotKeyHelper.TryRegist(_windowHandle, HotKeyModifiers.Ctrl | HotKeyModifiers.Shift, Key.Q, _switchStudyModeHotKey);
        if (!opacityHotKeyRegistSuccess || !studyModeHotKeyRegistSuccess)
        {
            txtInput.Text = !opacityHotKeyRegistSuccess
                ? "Ctrl+Q 快捷键已被其他程序占用"
                : "Ctrl+Shift+Q 快捷键已被其他程序占用";
        }

        // 恢复上次关闭时的编辑/学习模式；新安装的默认值为编辑模式。
        SetStudyMode(Properties.Settings.Default.StudyMode);

        // 创建一个新线程，每过 800ms 就重新将该窗体设为置顶（与视频播放器争夺 TopMost）
        var windowHandle = _windowHandle;
        _setTopMostThread = new Thread(() =>
        {
            while (true)
            {
                Thread.Sleep(800);
                if (_isExit)
                {
                    break;
                }
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    SetWindowPos(windowHandle, _hwndTopMost, 0, 0, 0, 0, 0x0003);
                });
            }
        });
        _setTopMostThread.Start();
    }

    /// <summary>
    /// 窗体
    /// 鼠标进入
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_isStudyMode)
        {
            return;
        }

        _gridMainHiddenTimer.Stop();
        // 显示输入框与笔记按钮
        gridNoteArea.Visibility = Visibility.Visible;
        // 显示右下角三角标
        ResizeMode = ResizeMode.CanResizeWithGrip;
    }

    /// <summary>
    /// 窗体
    /// 鼠标离开
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isStudyMode)
        {
            return;
        }

        if (gridReset.Visibility == Visibility.Hidden)
        {
            _gridMainHiddenTimer.Start();
        }
    }

    /// <summary>
    /// 窗体
    /// 鼠标单击
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isStudyMode)
        {
            return;
        }

        if (e.ChangedButton == MouseButton.Left && Mouse.LeftButton == MouseButtonState.Pressed)
        {
            ResizeMode = ResizeMode.NoResize; // 防止窗口拖到屏幕边缘自动最大化
            UpdateLayout();

            DragMove(); // 拖动窗体

            ResizeMode = ResizeMode.CanResizeWithGrip;
            UpdateLayout();
        }
    }

    /// <summary>
    /// 窗体
    /// 关闭中
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // 先保存当前模式，再恢复编辑状态并清理窗口资源。
        SaveCurrentSettings();

        _gridMainHiddenTimer.Stop();
        _studyModeTimer.Stop();
        SetStudyMode(false);

        if (_windowHandle != IntPtr.Zero)
        {
            HotKeyHelper.UnRegist(_windowHandle, _switchOpacityHotKey);
            HotKeyHelper.UnRegist(_windowHandle, _switchStudyModeHotKey);
            SetClickThrough(false);
        }

        gridWriteNote.Visibility = Visibility.Hidden;
        gridReset.Visibility = Visibility.Hidden;

        _isExit = true;
    }

    /// <summary>
    /// 学习模式定时检测鼠标是否位于遮罩窗口范围内。
    /// GetCursorPos 与 GetWindowRect 都使用屏幕像素坐标，避免与 WPF 逻辑坐标混用。
    /// </summary>
    private void StudyModeTimer_Tick(object? sender, EventArgs e)
    {
        if (_isStudyMode)
        {
            _isControlAreaHovered = IsCursorInsideControlArea();
            SetRevealState(IsCursorInsideWindow() && !_isControlAreaHovered);
        }
    }

    /// <summary>
    /// 右侧常驻控制区进入：学习模式下保留按钮可操作。
    /// </summary>
    private void GridControls_MouseEnter(object sender, MouseEventArgs e)
    {
        _isControlAreaHovered = true;
        if (_isStudyMode)
        {
            SetRevealState(false);
        }
    }

    /// <summary>
    /// 右侧常驻控制区离开：下一个轮询周期恢复正常揭示判断。
    /// </summary>
    private void GridControls_MouseLeave(object sender, MouseEventArgs e)
    {
        _isControlAreaHovered = false;
    }

    /// <summary>
    /// 切换学习模式。
    /// </summary>
    private void ToggleStudyMode()
    {
        SetStudyMode(!_isStudyMode);
    }

    /// <summary>
    /// 设置学习模式，并在离开学习模式时立即恢复可操作状态。
    /// </summary>
    private void SetStudyMode(bool enabled)
    {
        _isStudyMode = enabled;
        menuStudyMode.IsChecked = enabled;
        gridStudyMode.ToolTip = enabled ? "关闭学习模式" : "开启学习模式";
        _isControlAreaHovered = IsCursorInsideControlArea();

        // 学习模式只隐藏笔记区域，根布局和右侧操作区始终保留。
        _gridMainHiddenTimer.Stop();
        gridNoteArea.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        ResizeMode = enabled ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
        Cursor = enabled ? Cursors.None : Cursors.Arrow;
        gridControls.Cursor = Cursors.Arrow;
        imgClose.Cursor = Cursors.Hand;
        gridEye.Cursor = Cursors.Hand;
        gridStudyMode.Cursor = Cursors.Hand;
        gridMinimize.Cursor = Cursors.Hand;

        if (enabled)
        {
            _studyModeTimer.Start();
            SetRevealState(IsCursorInsideWindow() && !_isControlAreaHovered);
        }
        else
        {
            _studyModeTimer.Stop();
            SetRevealState(false);
        }
    }

    /// <summary>
    /// 根据鼠标是否在窗口矩形内切换揭示状态。
    /// </summary>
    private void SetRevealState(bool revealed)
    {
        if (revealed && !_isStudyMode)
        {
            revealed = false;
        }

        if (_isRevealed == revealed && _isClickThrough == revealed)
        {
            return;
        }

        _isRevealed = revealed;
        if (revealed)
        {
            // 只让窗口背景透明，不能把右侧操作区随整个 Window 一起设为透明。
            Background = Brushes.Transparent;
            SetWindowOpacity(1d);
            SetClickThrough(true);
        }
        else
        {
            SetClickThrough(false);
            Background = _normalBackground;
            SetWindowOpacity(_normalOpacity);
        }
    }

    /// <summary>
    /// 只增删 WS_EX_TRANSPARENT，保留窗口现有的其他扩展样式。
    /// </summary>
    private void SetClickThrough(bool enabled)
    {
        if (_isClickThrough == enabled || _windowHandle == IntPtr.Zero)
        {
            return;
        }

        long currentStyle = GetWindowLongPtrCompat(_windowHandle, GwlExStyle).ToInt64();
        long updatedStyle = enabled
            ? currentStyle | WsExTransparent
            : currentStyle & ~WsExTransparent;

        if (currentStyle != updatedStyle)
        {
            SetWindowLongPtrCompat(_windowHandle, GwlExStyle, new IntPtr(updatedStyle));
            SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        }

        _isClickThrough = enabled;
    }

    private static IntPtr GetWindowLongPtrCompat(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : GetWindowLongPtr32(hWnd, nIndex);
    }

    private static IntPtr SetWindowLongPtrCompat(IntPtr hWnd, int nIndex, IntPtr newValue)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, newValue)
            : SetWindowLongPtr32(hWnd, nIndex, newValue);
    }

    private bool IsCursorInsideWindow()
    {
        return _windowHandle != IntPtr.Zero
            && GetCursorPos(out var cursor)
            && GetWindowRect(_windowHandle, out var windowRect)
            && cursor.X >= windowRect.Left
            && cursor.X < windowRect.Right
            && cursor.Y >= windowRect.Top
            && cursor.Y < windowRect.Bottom;
    }

    /// <summary>
    /// 使用 Win32 屏幕像素坐标检测右侧常驻控制区。
    /// 控制区宽高来自 WPF 逻辑单位，并按当前 DPI 换算为屏幕像素。
    /// </summary>
    private bool IsCursorInsideControlArea()
    {
        if (_windowHandle == IntPtr.Zero
            || !GetCursorPos(out var cursor)
            || !GetWindowRect(_windowHandle, out var windowRect))
        {
            return false;
        }

        var compositionTarget = PresentationSource.FromVisual(this)?.CompositionTarget;
        double scaleX = compositionTarget?.TransformToDevice.M11 ?? 1d;
        double scaleY = compositionTarget?.TransformToDevice.M22 ?? 1d;
        int controlWidth = Math.Max(1, (int)Math.Round(gridControls.ActualWidth * scaleX));
        int controlHeight = Math.Max(1, (int)Math.Round(gridControls.ActualHeight * scaleY));
        int controlLeft = windowRect.Right - controlWidth;
        int controlTop = windowRect.Top;

        return cursor.X >= controlLeft
            && cursor.X < windowRect.Right
            && cursor.Y >= controlTop
            && cursor.Y < controlTop + controlHeight;
    }

    private void SetWindowOpacity(double opacity)
    {
        if (Opacity != opacity)
        {
            Opacity = opacity;
        }
    }

    /// <summary>
    /// 设置编辑模式下应恢复的透明度。
    /// </summary>
    private void SetNormalOpacity(double opacity)
    {
        _normalOpacity = opacity;
        if (!_isRevealed)
        {
            SetWindowOpacity(opacity);
        }
    }

    /// <summary>
    /// 保留 Ctrl+Q 原有功能：切换正常遮罩透明度。
    /// </summary>
    private void ToggleOpacity()
    {
        SetNormalOpacity(_normalOpacity == 1d ? WINDOW_MINIUMU_OPACITY : 1d);
    }

    /// <summary>
    /// 加载配置并初始化服务
    /// </summary>
    [MemberNotNull(nameof(_noteService))]
    private void LoadSettingsAndInitializeServices()
    {
        // 读取配置文件，设置位置、大小、主题颜色和语言模式
        try
        {
            Rect restoreBounds = Properties.Settings.Default.MainWindowsRect;
            Left = restoreBounds.Left;
            Top = restoreBounds.Top;
            Width = restoreBounds.Width;
            Height = restoreBounds.Height;
            ThemeColor = Properties.Settings.Default.ThemeColor;
        }
        catch
        {
            Width = WINDOW_DEFAULT_WIDTH;
            Height = WINDOW_DEFAULT_HEIGHT;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ThemeColor = ThemeColors.Black;
        }

        _noteService = new CsvFileNoteService();
    }

    /// <summary>
    /// 保存当前配置
    /// </summary>
    private void SaveCurrentSettings()
    {
        // 保存当前位置、大小和状态到配置文件
        Properties.Settings.Default.MainWindowsRect = RestoreBounds;
        Properties.Settings.Default.ThemeColor = ThemeColor;
        Properties.Settings.Default.StudyMode = _isStudyMode;
        Properties.Settings.Default.Save();
    }

    /// <summary>
    /// 设置窗体颜色
    /// </summary>
    /// <param name="background"></param>
    /// <param name="foreground"></param>
    private void SetWindowColor(Brush background, Brush foreground)
    {
        _normalBackground = background;
        if (!_isRevealed)
        {
            Background = background;
        }

        txtInput.Background = background;
        txtInput.Foreground = foreground;
        imgEye.Foreground = imgStudyMode.Foreground = imgReset.Foreground = labReset.Foreground = imgWriteNote.Foreground = labWriteNote.Foreground = imgClose.Foreground = foreground;
    }

    /// <summary>
    /// 重置按钮
    /// 鼠标单击
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ImgReset_MouseDown(object sender, MouseButtonEventArgs e)
    {
        txtInput.Text = string.Empty;
        gridReset.Visibility = Visibility.Hidden;
    }

    /// <summary>
    /// 关闭按钮
    /// 鼠标单击
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ImgClose_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isExit = true;
        Application.Current.Shutdown();
    }

    /// <summary>
    /// 右侧常驻按钮
    /// 切换学习模式
    /// </summary>
    private void GridStudyMode_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            ToggleStudyMode();
        }
    }

    /// <summary>
    /// 最小化按钮。
    /// </summary>
    private void GridMinimize_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (_isStudyMode)
            {
                SetRevealState(false);
            }

            WindowState = WindowState.Minimized;
        }
    }

    /// <summary>
    /// 右键菜单
    /// 学习模式
    /// </summary>
    private void MenuStudyMode_Click(object sender, RoutedEventArgs e)
    {
        SetStudyMode(menuStudyMode.IsChecked);
    }

    /// <summary>
    /// 眼睛按钮
    /// 鼠标移入
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void GridEye_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_isStudyMode)
        {
            SetNormalOpacity(WINDOW_MINIUMU_OPACITY);
        }
    }

    /// <summary>
    /// 眼睛按钮
    /// 鼠标移出
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void GridEye_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isStudyMode)
        {
            SetNormalOpacity(1d);
        }
    }

    /// <summary>
    /// 笔记按钮
    /// 鼠标单击
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void ImgWriteNote_MouseDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            gridWriteNote.Visibility = Visibility.Hidden;
            gridNoteWrote.Visibility = Visibility.Visible;

            await _noteService.WriteAsync(txtInput.Text);
        }
        catch
        {
            gridWriteNote.Visibility = Visibility.Visible;
            gridNoteWrote.Visibility = Visibility.Hidden;

            txtInput.Text = "笔记记录失败，可能是因为文件被占用或没有写入文件的权限。如果您已打开笔记文件，请将其关闭后再记录笔记；如果依然无法记录笔记，请尝试使用系统管理员权限启动本应用。";
        }
    }

    /// <summary>
    /// 右键菜单
    /// 退出
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        _isExit = true;
        Application.Current.Shutdown();
    }

    /// <summary>
    /// 右键菜单
    /// 亮白
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MenuThemeColorWhite_Click(object sender, RoutedEventArgs e)
    {
        ThemeColor = ThemeColors.White;
    }

    /// <summary>
    /// 右键菜单
    /// 亮灰
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MenuThemeColorLightGray_Click(object sender, RoutedEventArgs e)
    {
        ThemeColor = ThemeColors.LightGray;
    }

    /// <summary>
    /// 右键菜单
    /// 暗灰
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MenuThemeColorDimGray_Click(object sender, RoutedEventArgs e)
    {
        ThemeColor = ThemeColors.DimGray;
    }

    /// <summary>
    /// 右键菜单
    /// 暗黑
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MenuThemeColorBlack_Click(object sender, RoutedEventArgs e)
    {
        ThemeColor = ThemeColors.Black;
    }

    /// <summary>
    /// 快捷键
    /// 切换窗体不透明度
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CommandBinding_SwitchOpacity(object sender, CanExecuteRoutedEventArgs e)
    {
        ToggleOpacity();
    }

    /// <summary>
    /// 文本输入框
    /// 文本改变
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        gridNoteWrote.Visibility = Visibility.Hidden;

        if (string.IsNullOrWhiteSpace(txtInput.Text))
        {
            gridWriteNote.Visibility = Visibility.Hidden;
            gridReset.Visibility = Visibility.Hidden;
        }
        else
        {
            gridWriteNote.Visibility = Visibility.Visible;
            gridReset.Visibility = Visibility.Visible;
        }
    }
}
