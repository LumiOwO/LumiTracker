from windows_capture import WindowsCapture, Frame, InternalCaptureControl
import sys
import win32api
import win32gui
import logging
import ctypes

from PIL import Image

from .config import cfg
from .frame_manager import FrameManager

class WindowWatcher:
    def __init__(self):
        self.capture       = None
        self.frame_manager = None
        self.hwnd          = 0
        self.border_size   = (0, 0)
        self.window_size   = (0, 0) # border included
        self.client_size   = (0, 0)

    def Start(self, hwnd, title):
        logging.debug(f'"info": "WindowWatcher start, {hwnd=}, {title=}"')

        PROCESS_PER_MONITOR_DPI_AWARE = 2
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE)
        except Exception as e:
            logging.error(f'"info": "Error setting DPI awareness: {e}"')

        self.hwnd = hwnd

        self.frame_manager = FrameManager()

        # Every Error From on_closed and on_frame_arrived Will End Up Here
        self.capture = WindowsCapture(
            cursor_capture=False,
            draw_border=False,
            window_name=title,
        )
        self.capture.event(self.on_frame_arrived)
        self.capture.event(self.on_closed)

        self.capture.start()
    
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
    
    def UpdateWindowBorderSize(self, window_width, window_height):
        self.window_size = (window_width, window_height)

        # !!! Must consider DPI scale !!!
        # https://blog.csdn.net/frostime/article/details/104798061
        # Now DpiAwareness enabled, so no need to scale
        # scale = self.GetMonitorScale()

        # Get the client rect (excludes borders and title bar)
        client_rect = win32gui.GetClientRect(self.hwnd)
        client_left, client_top, client_right, client_bottom = client_rect
        self.client_size = (client_right - client_left, client_bottom - client_top)

        left_border  = (self.window_size[0] - self.client_size[0]) // 2
        title_height =  self.window_size[1] - self.client_size[1]
        self.border_size = (left_border, title_height)

        # logging.debug(self.window_size)
        # logging.debug(self.client_size)
        # logging.debug(self.border_size)

    # Called Every Time A New Frame Is Available
    def on_frame_arrived(self, frame: Frame, capture_control: InternalCaptureControl):
        if frame.width != self.window_size[0] or frame.height != self.window_size[1]:
            self.UpdateWindowBorderSize(frame.width, frame.height)

        left   = self.border_size[0]
        top    = self.border_size[1]
        frame  = frame.crop(left, top, left + self.client_size[0], top + self.client_size[1])

        buffer = frame.frame_buffer[:, :, 2::-1] # bgr to rgb
        image  = Image.fromarray(buffer)
        self.frame_manager.OnFrameArrived(image)

    # Called When The Capture Item Closes Usually When The Window Closes, Capture
    # Session Will End After This Function Ends
    def on_closed(self):
        logging.debug('"info": "Window Capture Session Closed"')

if __name__ == '__main__':
    assert len(sys.argv) == 3, "Wrong number of arguments"
    hwnd  = int(sys.argv[1])
    title = sys.argv[2]

    window_watcher = WindowWatcher()
    window_watcher.Start(hwnd, title)
