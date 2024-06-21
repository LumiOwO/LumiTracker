import win32gui
import win32api
import ctypes

from ..frame_manager import FrameManager

class CaptureBase:
    def __init__(self):
        self.hwnd          = 0
        self.frame_manager = None

    def Start(self, hwnd):
        self.hwnd = hwnd
        self.frame_manager = FrameManager()

        self.OnStart(hwnd)
        self.MainLoop()
    
    def OnStart(self, hwnd):
        raise NotImplementedError()

    def OnClosed(self):
        raise NotImplementedError()

    def MainLoop(self):
        raise NotImplementedError()

    def OnResize(self, client_width, client_height):
        raise NotImplementedError()

    def OnFrameArrived(self, frame_buffer):
        # frame_buffer: 4-channels, BGRX
        self.frame_manager.OnFrameArrived(frame_buffer)


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