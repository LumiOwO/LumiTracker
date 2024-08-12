import time
import logging

import win32gui
import win32ui
import win32con

import numpy as np

from ..config import cfg, LogWarning
from .base import CaptureBase


class BitBlt(CaptureBase):
    SUCCESS   = 0
    FAILED    = 1
    MINIMIZED = 2

    def __init__(self):
        super().__init__()

        self.hwnd_dc = None
        self.mfc_dc  = None
        self.save_dc = None
        self.bitmap  = None
        self.width   = 0
        self.height  = 0

    def OnStart(self, hwnd):
        # Get window device context
        self.hwnd_dc = win32gui.GetWindowDC(self.hwnd)
        self.mfc_dc  = win32ui.CreateDCFromHandle(self.hwnd_dc)
        self.save_dc = self.mfc_dc.CreateCompatibleDC()

    def OnClosed(self):
        # Clean up
        self.DestroyBitmap()
        
        try:
            self.save_dc.DeleteDC()
        except win32ui.error as e:
            LogWarning(info=f"Error deleting save_dc, maybe the reason is the closed hwnd")
    
        try:
            self.mfc_dc.DeleteDC()
        except win32ui.error as e:
            LogWarning(info=f"Error deleting mfc_dc, maybe the reason is the closed hwnd")

        try:
            win32gui.ReleaseDC(self.hwnd, self.hwnd_dc)
        except win32ui.error as e:
            LogWarning(info=f"Error releasing hwnd_dc, maybe the reason is the closed hwnd")

    def MainLoop(self):
        while True:
            start_time = time.perf_counter()

            self.BeforeFrameArrived()

            # Capture frame
            frame_buffer, status = self.CaptureWindow()
            if status == BitBlt.SUCCESS:
                self.OnFrameArrived(frame_buffer)
            elif status == BitBlt.FAILED:
                self.OnClosed()
                break
            elif status == BitBlt.MINIMIZED:
                pass
            else:
                raise NotImplementedError()

            if cfg.DEBUG_SAVE:
                # image.save(os.path.join(cfg.debug_dir, "save", f"bitblt.png"))
                # exit(1)
                pass

            dt = time.perf_counter() - start_time
            self.WaitForFrameRateLimit(elapsed_time=dt)
    
    def OnResize(self, client_width, client_height):
        self.DestroyBitmap()
        self.CreateBitmap(client_width, client_height)
        self.frame_manager.Resize(client_width, client_height)
    
    def CreateBitmap(self, width, height):
        self.bitmap = win32ui.CreateBitmap()
        self.bitmap.CreateCompatibleBitmap(self.mfc_dc, width, height)
        self.save_dc.SelectObject(self.bitmap)

        self.width  = width
        self.height = height

    def DestroyBitmap(self):
        if self.bitmap is None:
            return
        win32gui.DeleteObject(self.bitmap.GetHandle())

    def CaptureWindow(self):
        try:
            (client_left, client_top, client_right, client_bot), offset = self.GetClientRect()
            client_width  = client_right - client_left
            client_height = client_bot   - client_top
            if client_width == 0 or client_height == 0:
                self.frame_manager.prev_log_time = time.perf_counter()
                return (None, BitBlt.MINIMIZED)

            if client_width != self.width or client_height != self.height:
                self.OnResize(client_width, client_height)

            # BitBlt to capture the window frame
            self.save_dc.BitBlt(
                (0, 0), (client_width, client_height), self.mfc_dc, 
                (offset[0], offset[1]), win32con.SRCCOPY)

            # Get the bitmap data
            bitmap_bits = self.bitmap.GetBitmapBits(True)
            frame_buffer = np.frombuffer(bitmap_bits, dtype=np.uint8)
            frame_buffer = frame_buffer.reshape((client_height, client_width, 4))

            return (frame_buffer, BitBlt.SUCCESS)
        except win32gui.error:
            return (None, BitBlt.FAILED)
