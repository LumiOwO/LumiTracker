using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LumiTracker.Config;
using LumiTracker.Views.Windows;
using Microsoft.Extensions.Logging;

namespace LumiTracker.Helpers
{
    public delegate void OnGenshinWindowResizedCallback(int width, int height, bool isMinimized, float dpiScale);

    // https://stackoverflow.com/questions/32806280/attach-wpf-window-to-the-window-of-another-process
    public class WindowSnapper
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public Rect rcMonitor;
            public Rect rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        public enum MonitorDpiType
        {
            MDT_EFFECTIVE_DPI = 0,
            MDT_ANGULAR_DPI = 1,
            MDT_RAW_DPI = 2,
            MDT_DEFAULT = MDT_EFFECTIVE_DPI
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }

            public Rect()
            {
                Left = 0; Top = 0; Right = 0; Bottom = 0;
            }

            public int Height
            {
                get { return Bottom - Top; }
            }

            public int Width
            {
                get { return Right - Left; }
            }

            public static bool operator!=(Rect r1, Rect r2)
            {
                return !(r1 == r2);
            }

            public static bool operator==(Rect r1, Rect r2)
            {
                return r1.Left == r2.Left && r1.Right == r2.Right && r1.Top == r2.Top && r1.Bottom == r2.Bottom;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(obj, null) || GetType() != obj.GetType())
                {
                    return false;
                }

                var other = (Rect)obj;
                return (this == other);
            }

            public override int GetHashCode()
            {
                return Left.GetHashCode() ^ Top.GetHashCode() ^ Right.GetHashCode() ^ Bottom.GetHashCode();
            }
        }


        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, ref Rect rectangle);
        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr hMonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetClientRect(IntPtr hWnd, ref Rect lpRect);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        private DispatcherTimer _timer;
        private Rect            _lastBounds;    // This is not client bounds and contains margin size
        private IntPtr          _lastForeground;
        private DeckWindow?     _src_window;
        private IntPtr          _src_hwnd;
        private IntPtr          _canvas_hwnd;
        private IntPtr          _dst_hwnd;
        private bool            _bOutside;
        private bool            _isFirstTick;
        private float           _dpiscale;

        public event OnGenshinWindowResizedCallback? GenshinWindowResized;

        public WindowSnapper(DeckWindow? window, IntPtr hwnd, bool bOutside)
        {
            _lastBounds     = new Rect();
            _lastForeground = 0;
            _src_window     = window;
            _src_hwnd       = window != null ? new WindowInteropHelper(window).Handle : IntPtr.Zero;
            _canvas_hwnd    = window != null ? new WindowInteropHelper(window.CanvasWindow).Handle : IntPtr.Zero;
            _dst_hwnd       = hwnd;
            _bOutside       = bOutside;
            _isFirstTick    = true;
            _dpiscale       = 1.0f;  

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += (x, y) => Tick();
            _timer.IsEnabled = false;
        }

        public void Attach()
        {
            if (_src_window != null)
            {
                GenshinWindowResized += _src_window.OnGenshinWindowResized;
            }
            _isFirstTick = true;
            _timer.Start();
        }

        public void Detach()
        {
            _timer.Stop();
            _isFirstTick = false;
            if (_src_window != null)
            {
                GenshinWindowResized -= _src_window.OnGenshinWindowResized;
            }
        }

        public void SetbOutside(bool bOutside)
        {
            if (_bOutside != bOutside)
            {
                _bOutside = bOutside;
                // force update
                _lastBounds = new Rect();
            }
        }

        private void Tick()
        {
            Rect bounds = GetDstWindowBounds();
            if (_isFirstTick || bounds != _lastBounds)
            {
                // Get dpi scale
                const int MONITOR_DEFAULTTONEAREST = 0x00000002;
                IntPtr hMonitor = MonitorFromWindow(_dst_hwnd, MONITOR_DEFAULTTONEAREST);
                MONITORINFOEX monitorInfo = new MONITORINFOEX();
                monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);
                GetMonitorInfo(hMonitor, ref monitorInfo);
                int PhysicalHeight = monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top;

                uint dpiX, dpiY;
                int result = GetDpiForMonitor(hMonitor, MonitorDpiType.MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
                float LogicalHeight = (float)PhysicalHeight * 96 / dpiY;

                // Calculate the scale factor
                float scale = (float)PhysicalHeight / LogicalHeight;
                if (scale <= 0) scale = 1.0f; // protect

                // Snap to game window
                Rect clientRect = SnapToWindow(scale);

                // Trigger GenshinWindowResized if needed
                bool isMinimized = IsIconic(_dst_hwnd);
                if (isMinimized || bounds.Height != _lastBounds.Height || bounds.Width != _lastBounds.Width || scale != _dpiscale)
                {
                    Configuration.Logger.LogInformation(
                        $"Game window resized to {clientRect.Width} x {clientRect.Height}, isMinimized: {isMinimized}, " +
                        $"clientRect: [{clientRect.Left}, {clientRect.Top}, {clientRect.Right}, {clientRect.Bottom}], " +
                        $"scale=({PhysicalHeight}/{LogicalHeight})={scale}"
                        );
                    GenshinWindowResized?.Invoke(clientRect.Width, clientRect.Height, isMinimized, scale);
                }
                Configuration.Logger.LogDebug($"Game window moved to [{bounds.Left}, {bounds.Top}, {bounds.Right}, {bounds.Bottom}]");

                // Update last bounds
                _lastBounds = bounds;
                _dpiscale = scale;
            }
            //Configuration.Logger.LogDebug($"bounds: {bounds.Width}, {bounds.Height}");
            //Configuration.Logger.LogDebug($"_lastBounds: {_lastBounds.Width}, {_lastBounds.Height}");
            //Configuration.Logger.LogDebug($"_src_window.Height: {_src_window.Height}");

            var foregroundHwnd = GetForegroundWindow();
            if (_lastForeground != foregroundHwnd)
            {
                OnForegroundChanged(_lastForeground, foregroundHwnd);
                _lastForeground = foregroundHwnd;
            }

            _isFirstTick = false;
        }

        private static readonly double WidthRatio = Configuration.Get<double>("deck_window_width_ratio");
        private Rect SnapToWindow(float dpiScale)
        {
            // Get client rect
            Rect clientRect = new Rect();
            GetClientRect(_dst_hwnd, ref clientRect);
            if (clientRect.Height == 0 || clientRect.Width == 0) return clientRect;

            POINT clientLeftTop = new POINT { x = clientRect.Left, y = clientRect.Top };
            ClientToScreen(_dst_hwnd, ref clientLeftTop);

            // Snap to target window
            if (_src_window != null)
            {
                float dpiScaleInv = 1.0f / dpiScale;

                var deck = _src_window;
                if (_bOutside)
                {
                    deck.Width  = dpiScaleInv * clientRect.Width * WidthRatio;
                    deck.Height = dpiScaleInv * clientRect.Height;
                    deck.Left   = dpiScaleInv * (clientLeftTop.x + clientRect.Width);
                    deck.Top    = dpiScaleInv * clientLeftTop.y;
                }
                else
                {
                    deck.Width  = dpiScaleInv * clientRect.Width * WidthRatio;
                    deck.Height = dpiScaleInv * clientRect.Height;
                    deck.Left   = dpiScaleInv * clientLeftTop.x;
                    deck.Top    = dpiScaleInv * clientLeftTop.y;
                }

                var canv = _src_window.CanvasWindow;
                canv.Width  = dpiScaleInv * clientRect.Width;
                canv.Height = dpiScaleInv * clientRect.Height;
                canv.Left   = dpiScaleInv * clientLeftTop.x;
                canv.Top    = dpiScaleInv * clientLeftTop.y;
            }

            return clientRect;
        }

        private void OnForegroundChanged(IntPtr oldHwnd, IntPtr newHwnd)
        {
            if (_src_window == null) return;

            // Deck window
            if (_bOutside)
            {
                _src_window.ShowWindow();
                _src_window.CanvasWindow.ShowWindow();
                if (newHwnd == _dst_hwnd)
                {
                    // Trigger Topmost OnChanged event to bring the window to topmost
                    bool topmost = _src_window.Topmost;
                    _src_window.Topmost = !topmost;
                    _src_window.Topmost = topmost;

                    topmost = _src_window.CanvasWindow.Topmost;
                    _src_window.CanvasWindow.Topmost = !topmost;
                    _src_window.CanvasWindow.Topmost = topmost;
                }
            }
            else
            {
                if (newHwnd != _src_hwnd && newHwnd != _dst_hwnd && newHwnd != _canvas_hwnd)
                {
                    _src_window.HideWindow();
                    _src_window.CanvasWindow.HideWindow();
                }
                else
                {
                    _src_window.ShowWindow();
                    _src_window.CanvasWindow.ShowWindow();
                }
            }
        }

        private Rect GetDstWindowBounds()
        {
            Rect bounds = new Rect();
            GetWindowRect(_dst_hwnd, ref bounds);
            return bounds;
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int Left, Right, Top, Bottom;
        }

        // Set the window to be layered, so it can be captured by OBS with AllowsTransparency="True"
        // Note: OBS must use Windows Capture to capture this window
        static public void SetLayeredWindow(IntPtr hwnd)
        {
            HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor = Colors.Transparent;

            MARGINS margins = new MARGINS() { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);

            const int GWL_EXSTYLE = -20;
            const int WS_EX_LAYERED = 0x80000;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
        }
    }
}
