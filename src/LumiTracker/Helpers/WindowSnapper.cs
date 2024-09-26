using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using LumiTracker.Config;
using LumiTracker.Views.Windows;
using Microsoft.Extensions.Logging;

namespace LumiTracker.Helpers
{
    public delegate void OnGenshinWindowResizedCallback(int width, int height, bool isMinimized);

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
        private Rect            _lastBounds;
        private IntPtr          _lastForeground;
        private DeckWindow      _src_window;
        private IntPtr          _src_hwnd;
        private IntPtr          _dst_hwnd;
        private bool            _bOutside;
        private bool            _isFirstTick;

        public event OnGenshinWindowResizedCallback? GenshinWindowResized;

        public WindowSnapper(DeckWindow window, IntPtr hwnd, bool bOutside)
        {
            _lastBounds     = new Rect();
            _lastForeground = 0;
            _src_window     = window;
            _src_hwnd       = new WindowInteropHelper(window).Handle;
            _dst_hwnd       = hwnd;
            _bOutside       = bOutside;
            _isFirstTick    = true;

            GenshinWindowResized += window.OnGenshinWindowResized;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += (x, y) => Tick();
            _timer.IsEnabled = false;
        }

        public void Attach()
        {
            _isFirstTick = true;
            _timer.Start();
        }

        public void Detach()
        {
            _timer.Stop();
            _isFirstTick = false;
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
            var bounds = GetWindowBounds(_dst_hwnd);
            if (_isFirstTick || bounds != _lastBounds)
            {
                Rect clientRect = SnapToWindow(bounds);
                if (bounds.Width != _lastBounds.Width || bounds.Height != _lastBounds.Height)
                {
                    bool isMinimized = IsIconic(_dst_hwnd);
                    GenshinWindowResized?.Invoke(clientRect.Width, clientRect.Height, isMinimized);
                }
                _lastBounds = bounds;
            }
            //Configuration.Logger.LogDebug($"bounds: {bounds.Width}, {bounds.Height}");
            //Configuration.Logger.LogDebug($"_lastBounds: {_lastBounds.Width}, {_lastBounds.Height}");
            //Configuration.Logger.LogDebug($"_src_window.Height: {_src_window.Height}");

            var foregroundHwnd = GetForegroundWindow();
            if (_bOutside)
            {
                _src_window.ShowWindow();
                if (foregroundHwnd != _lastForeground && foregroundHwnd == _dst_hwnd)
                {
                    // Set to topmost once
                    _src_window.Topmost = true;
                    _src_window.Topmost = false;
                }
            }
            else
            {
                if (foregroundHwnd != _src_hwnd && foregroundHwnd != _dst_hwnd)
                {
                    _src_window.HideWindow();
                }
                else
                {
                    _src_window.ShowWindow();
                }
            }

            _lastForeground = foregroundHwnd;
            _isFirstTick = false;
        }

        private Rect SnapToWindow(Rect bounds)
        {
            // Get client rect
            Rect clientRect = new Rect();
            GetClientRect(_dst_hwnd, ref clientRect);
            if (clientRect.Height == 0 || clientRect.Width == 0) return clientRect;

            POINT clientLeftTop = new POINT { x = clientRect.Left, y = clientRect.Top };
            ClientToScreen(_dst_hwnd, ref clientLeftTop);

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

            if (bounds.Height != _lastBounds.Height || bounds.Width != _lastBounds.Width)
            {
                Configuration.Logger.LogInformation(
                    $"Game window resized to {clientRect.Width} x {clientRect.Height}, " +
                    $"clientRect: [{clientRect.Left}, {clientRect.Top}, {clientRect.Right}, {clientRect.Bottom}], " +
                    $"scale=({PhysicalHeight}/{LogicalHeight})={scale}"
                    );
            }
            Configuration.Logger.LogDebug($"Game window moved to [{bounds.Left}, {bounds.Top}, {bounds.Right}, {bounds.Bottom}]");

            // Snap to target window
            if (_bOutside)
            {
                _src_window.Width  = clientRect.Width  / scale * 0.18;
                _src_window.Height = bounds.Height     / scale;
                _src_window.Left   = clientLeftTop.x   / scale + clientRect.Width  / scale;
                _src_window.Top    = clientLeftTop.y   / scale + clientRect.Height / scale - _src_window.Height;
            }
            else
            {
                _src_window.Width  = clientRect.Width  / scale * 0.18;
                _src_window.Height = bounds.Height     / scale;
                _src_window.Left   = clientLeftTop.x   / scale;
                _src_window.Top    = clientLeftTop.y   / scale + clientRect.Height / scale - _src_window.Height;
            }

            return clientRect;
        }

        private Rect GetWindowBounds(IntPtr handle)
        {
            Rect bounds = new Rect();
            GetWindowRect(handle, ref bounds);
            return bounds;
        }
    }
}
