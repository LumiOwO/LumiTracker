import sys
import win32api
import logging
import ctypes

from .frame_manager import FrameManager

class WindowWatcher:
    def __init__(self):
        self.frame_manager = None
        self.hwnd          = 0

    def Start(self, hwnd, title):
        logging.debug(f'"info": "WindowWatcher start, {hwnd=}, {title=}"')

        PROCESS_PER_MONITOR_DPI_AWARE = 2
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE)
        except Exception as e:
            logging.error(f'"info": "Error setting DPI awareness: {e}"')

        self.hwnd = hwnd
        self.frame_manager = FrameManager()

        self.OnStart(hwnd, title)

    def OnStart(self, hwnd, title):
        pass
    
    def OnClosed(self):
        pass

    def OnFrameArrived(self, frame):
        self.frame_manager.OnFrameArrived(frame)

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
    assert len(sys.argv) == 3, "Wrong number of arguments"
    hwnd  = int(sys.argv[1])
    title = sys.argv[2]

    from .capture import WindowsCaptureWatcher, BitBltWatcher
    # window_watcher = WindowsCaptureWatcher()
    window_watcher = BitBltWatcher()
    window_watcher.Start(hwnd, title)
