import sys
import logging
import ctypes
import json

from .config import cfg, LogDebug, LogError
from .enums import ECaptureType

class WindowWatcher:
    def __init__(self):
        self.hwnd    = 0
        self.capture = None

    def Start(self, hwnd, capture, port, client_type, log_dir, test_on_resize):
        PROCESS_PER_MONITOR_DPI_AWARE = 2
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE)
        except Exception as e:
            LogError(info=f"Error setting DPI awareness: {e}")

        self.hwnd = hwnd
        self.capture = capture

        self.capture.Start(hwnd, port, client_type, log_dir, test_on_resize)

if __name__ == '__main__':
    assert len(sys.argv) == 2, "Wrong number of arguments"
    init_file_path = sys.argv[1]
    with open(init_file_path, 'r', encoding='utf-8') as f:
        params = json.load(f)
    hwnd            = int(params["hwnd"])
    client_type     = str(params["client_type"])
    capture_type    = str(params["capture_type"])
    can_hide_border = int(params["can_hide_border"])
    port            = int(params["port"])
    log_dir         = str(params["log_dir"])
    test_on_resize  = int(params["test_on_resize"])

    LogDebug(
        params,
        info="WindowWatcher start",
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
    window_watcher.Start(hwnd, capture, port, client_type, log_dir, test_on_resize)
