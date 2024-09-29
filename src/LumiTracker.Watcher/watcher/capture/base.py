import win32gui
import win32api
import ctypes
from datetime import datetime
import os
from abc import ABC, abstractmethod

from ..config import cfg, LogDebug, LogInfo
from ..enums import ETaskType
from ..database import SaveImage
from ..frame_manager import FrameManager
from ..input_manager import InputManager

kernel32 = ctypes.WinDLL('kernel32')
kernel32.Sleep.argtypes = [ctypes.c_ulong]
kernel32.Sleep.restype = None

winmm = ctypes.WinDLL('winmm')
winmm.timeBeginPeriod.argtypes = [ctypes.c_uint]
winmm.timeBeginPeriod.restype = ctypes.c_uint
winmm.timeEndPeriod.argtypes = [ctypes.c_uint]
winmm.timeEndPeriod.restype = ctypes.c_uint

class CaptureBase(ABC):
    def __init__(self):
        self.hwnd           = 0
        self.frame_manager  = None
        self.frame_interval = 1.0 / cfg.frame_limit
        self.input_manager  = None

        # set timer resolution to 1 ms
        winmm.timeBeginPeriod(1)

    def Start(self, hwnd, port):
        self.hwnd = hwnd
        self.frame_manager = FrameManager()
        self.input_manager = InputManager(port)

        self.OnStart(hwnd)
        self.MainLoop()
    
    def Close(self):
        self.OnClosed()
        self.input_manager.Close()
    
    @abstractmethod
    def OnStart(self, hwnd):
        raise NotImplementedError()

    @abstractmethod
    def OnClosed(self):
        raise NotImplementedError()

    @abstractmethod
    def MainLoop(self):
        raise NotImplementedError()

    @abstractmethod
    def OnResize(self, width, height):
        raise NotImplementedError()

    def BeforeFrameArrived(self):
        self.input_manager.Tick()

    def OnFrameArrived(self, frame_buffer):
        if self.input_manager.capture_save_dir:
            filename = datetime.now().strftime("%Y-%m-%d_%H-%M-%S") + ".png"
            path = os.path.join(self.input_manager.capture_save_dir, filename)
            SaveImage(frame_buffer, path, remove_alpha=True)
            LogInfo(
                type=f"{ETaskType.CAPTURE_TEST.name}",
                filename=filename,
                width=frame_buffer.shape[1],
                height=frame_buffer.shape[0],
                )

        # frame_buffer: 4-channels, BGRX
        self.frame_manager.OnFrameArrived(frame_buffer)

    def WaitForFrameRateLimit(self, elapsed_time):
        # use Windows native api to get accurate sleep interval
        # should call winmm.timeBeginPeriod() to set timer resolution
        dt = self.frame_interval - elapsed_time
        if dt > 0:
            kernel32.Sleep(int(dt * 1000))

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
        # LogDebug(f"{physicalHeight=}, {logicalHeight=}, {scale=}, {hMonitor.handle=}")
        return scale