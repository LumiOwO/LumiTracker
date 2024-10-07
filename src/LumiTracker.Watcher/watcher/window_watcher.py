import sys
import logging
import ctypes

from .config import cfg, LogDebug, LogError
from .enums import ECaptureType

class WindowWatcher:
    def __init__(self):
        self.hwnd    = 0
        self.capture = None

    def Start(self, hwnd, capture, port, client_type):
        PROCESS_PER_MONITOR_DPI_AWARE = 2
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE)
        except Exception as e:
            LogError(info=f"Error setting DPI awareness: {e}")

        self.hwnd = hwnd
        self.capture = capture

        self.capture.Start(hwnd, port, client_type)

if __name__ == '__main__':
    assert len(sys.argv) == 6, "Wrong number of arguments"
    hwnd            = int(sys.argv[1])
    client_type     = sys.argv[2]
    capture_type    = sys.argv[3]
    can_hide_border = int(sys.argv[4])
    port            = int(sys.argv[5])

    LogDebug(
        info="WindowWatcher start",
        hwnd=hwnd, 
        client_type=client_type,
        capture_type=capture_type, 
        can_hide_border=can_hide_border,
        port=port
        )

    if capture_type == ECaptureType.BitBlt.name:
        from .capture import BitBlt
        capture = BitBlt()
    elif capture_type == ECaptureType.WindowsCapture.name:
        from .capture import WindowsCapture
        capture = WindowsCapture(can_hide_border)
    else:
        raise NotImplementedError()

    window_watcher = WindowWatcher()
    window_watcher.Start(hwnd, capture, port, client_type)
