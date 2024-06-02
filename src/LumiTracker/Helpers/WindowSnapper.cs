using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using LumiTracker.Config;
using LumiTracker.Views.Windows;
using Microsoft.Extensions.Logging;

namespace LumiTracker.Helpers
{
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

        private DispatcherTimer _timer;
        private Rect            _lastBounds;
        private DeckWindow      _src_window;
        private IntPtr          _src_hwnd;
        private IntPtr          _dst_hwnd;

        public WindowSnapper(DeckWindow window, IntPtr hwnd)
        {
            _src_window = window;
            _src_hwnd   = new WindowInteropHelper(window).Handle;
            _dst_hwnd   = hwnd;

            _lastBounds.Left   = 0;
            _lastBounds.Top    = 0;
            _lastBounds.Right  = 0;
            _lastBounds.Bottom = 0;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += (x, y) => Tick();
            _timer.IsEnabled = false;
        }

        public void Attach()
        {
            _timer.Start();
        }

        public void Detach()
        {
            _timer.Stop();
        }

        private void Tick()
        {
            var bounds = GetWindowBounds(_dst_hwnd);
            if (bounds != _lastBounds)
            {
                SnapToWindow(bounds);
            }

            var foregroundHwnd = GetForegroundWindow();
            //Configuration.Logger.LogDebug($"foregroundHwnd={foregroundHwnd}, _src_hwnd={_src_hwnd}, _dst_hwnd={_dst_hwnd}");
            if (_src_hwnd != foregroundHwnd)
            {
                if (_dst_hwnd != foregroundHwnd)
                {
                    _src_window.HideWindow();
                }
                else
                {
                    _src_window.ShowWindow();
                }
            }
        }

        private void SnapToWindow(Rect bounds)
        {
            Configuration.Logger.LogDebug($"bounds: {bounds.Left}, {bounds.Top}, {bounds.Right}, {bounds.Bottom}");

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
            Configuration.Logger.LogDebug($"PhysicalHeight={PhysicalHeight}, LogicalHeight={LogicalHeight}, scale={scale}");

            Rect clientRect = new Rect();
            GetClientRect(_dst_hwnd, ref clientRect);
            Configuration.Logger.LogDebug($"clientRect: {clientRect.Left}, {clientRect.Top}, {clientRect.Right}, {clientRect.Bottom}");
            POINT clientLeftTop = new POINT { x = clientRect.Left, y = clientRect.Top };
            ClientToScreen(_dst_hwnd, ref clientLeftTop);

            _src_window.Width  = clientRect.Width  / scale * 0.2;
            _src_window.Height = clientRect.Height / scale;
            _src_window.Left   = clientLeftTop.x / scale;
            _src_window.Top    = clientLeftTop.y / scale + clientRect.Height / scale - _src_window.Height;

            // Refresh popup, force the Popup to recalculate its position
            var offset = _src_window.DeckWindowPopup.HorizontalOffset;
            _src_window.DeckWindowPopup.HorizontalOffset = offset + 1;
            _src_window.DeckWindowPopup.HorizontalOffset = offset;

            _lastBounds = bounds;
        }

        private Rect GetWindowBounds(IntPtr handle)
        {
            Rect bounds = new Rect();
            GetWindowRect(handle, ref bounds);
            return bounds;
        }
    }
}
