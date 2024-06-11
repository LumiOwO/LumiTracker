import os
import time
import logging

import win32gui
import win32ui
import win32con

from PIL import Image

from ..config import cfg

from ..window_watcher import WindowWatcher

class BitBltWatcher(WindowWatcher):
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

        delay = cfg.frame_interval
        while True:
            start_time = time.time()

            # Capture frame
            image, status = self.CaptureWindow()
            if status == BitBltWatcher.SUCCESS:
                self.OnFrameArrived(image)
            elif status == BitBltWatcher.FAILED:
                self.OnClosed()
                break
            elif status == BitBltWatcher.MINIMIZED:
                pass
            else:
                raise NotImplementedError()

            if cfg.DEBUG_SAVE:
                # image.save(os.path.join(cfg.debug_dir, "save", f"bitblt.png"))
                # exit(1)
                pass

            dt = time.time() - start_time
            if dt < delay:
                time.sleep(delay - dt)
    
    def OnClosed(self):
        # Clean up
        self.DestroyBitmap()

        self.save_dc.DeleteDC()
        self.mfc_dc.DeleteDC()
        win32gui.ReleaseDC(self.hwnd, self.hwnd_dc)
    
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

    def OnResize(self, client_width, client_height):
        self.DestroyBitmap()
        self.CreateBitmap(client_width, client_height)
        self.SetFrameRatio(client_width, client_height)

    def CaptureWindow(self):
        try:
            (client_left, client_top, client_right, client_bot), offset = self.GetClientRect()
            client_width  = client_right - client_left
            client_height = client_bot   - client_top
            if client_width == 0 or client_height == 0:
                return (None, BitBltWatcher.MINIMIZED)

            if client_width != self.width or client_height != self.height:
                self.OnResize(client_width, client_height)

            # BitBlt to capture the window frame
            self.save_dc.BitBlt(
                (0, 0), (client_width, client_height), self.mfc_dc, 
                (offset[0], offset[1]), win32con.SRCCOPY)

            # Get the bitmap data
            bitmap_bits = self.bitmap.GetBitmapBits(True)
            image = Image.frombuffer(
                'RGB',
                (client_width, client_height),
                bitmap_bits,
                'raw',
                'BGRX',
                0,
                1
            )

            return (image, BitBltWatcher.SUCCESS)
        except win32gui.error:
            return (None, BitBltWatcher.FAILED)
