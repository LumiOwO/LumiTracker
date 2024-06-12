import sys
import win32api
import win32gui
import logging
import ctypes

from .frame_manager import FrameManager

class WindowWatcher:
    def __init__(self):
        self.frame_manager = None
        self.hwnd          = 0

    def Start(self, hwnd):
        PROCESS_PER_MONITOR_DPI_AWARE = 2
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE)
        except Exception as e:
            logging.error(f'"info": "Error setting DPI awareness: {e}"')

        self.hwnd = hwnd
        self.frame_manager = FrameManager()

        self.OnStart(hwnd)

    def OnStart(self, hwnd):
        pass
    
    def OnClosed(self):
        pass

    def OnFrameArrived(self, frame):
        self.frame_manager.OnFrameArrived(frame)

    def SetFrameRatio(self, client_width, client_height):
        if client_height == 0:
            return

        ratio = client_width / client_height
        EPSILON = 0.005
        if   abs( ratio - 16 / 9 ) < EPSILON:
            self.frame_manager.ratio = "16:9"
        elif abs( ratio - 16 / 10) < EPSILON:
            self.frame_manager.ratio = "16:10"
        elif abs( ratio - 21 / 9 ) < EPSILON:
            self.frame_manager.ratio = "21:9"
        else:
            logging.info(f'"type": "unsupported_ratio"')
            logging.warning(f'"info": "Current resolution is {client_width}x{client_height} with {ratio=}, which is not supported now."')
            self.frame_manager.ratio = "16:9" # default

    def GetClientRect(self):
        # Get window rect
        window_left, window_top, window_right, window_bot = win32gui.GetWindowRect(self.hwnd)

        # Get client rect
        client_rect = win32gui.GetClientRect(self.hwnd)
        client_left, client_top, client_right, client_bot = client_rect

        # Compute client rect offset
        client_left, client_top = win32gui.ClientToScreen(self.hwnd, (client_left, client_top))
        offset = (client_left - window_left, client_top - window_top)

        return client_rect, offset

    def GetMonitorScale(self):
        MONITOR_DEFAULTTONEAREST = 0x00000002
        MDT_EFFECTIVE_DPI = 0

        hMonitor = win32api.MonitorFromWindow(self.hwnd, MONITOR_DEFAULTTONEAREST)
        monitorInfo = win32api.GetMonitorInfo(hMonitor)
        physicalHeight = monitorInfo['Monitor'][3] - monitorInfo['Monitor'][1]

        dpiX = ctypes.c_uint()
        dpiY = ctypes.c_uint()
        ctypes.windll.shcore.GetDpiForMonitor(
            hMonitor.handle,
            MDT_EFFECTIVE_DPI,
            ctypes.byref(dpiX),
            ctypes.byref(dpiY)
        )
        logicalHeight = float(physicalHeight) * 96 / dpiY.value

        # Calculate scale factor
        scale = float(physicalHeight) / logicalHeight
        # logging.debug(f"{physicalHeight=}, {logicalHeight=}, {scale=}, {hMonitor.handle=}")
        return scale


if __name__ == '__main__':
    assert len(sys.argv) == 4, "Wrong number of arguments"
    hwnd            = int(sys.argv[1])
    capture_type    = sys.argv[2]
    can_hide_border = int(sys.argv[3])

    logging.debug(f'"info": "WindowWatcher start, {hwnd=}, {capture_type=}, {can_hide_border=}"')

    if capture_type == "BitBlt":
        from .capture import BitBltWatcher
        window_watcher = BitBltWatcher()
    elif capture_type == "WindowsCapture":
        from .capture import WindowsCaptureWatcher
        window_watcher = WindowsCaptureWatcher(can_hide_border)
    else:
        raise NotImplementedError()

    window_watcher.Start(hwnd)
