import os
import time
import logging

import numpy as np
from PIL import Image

import win32api
import win32gui
import win32gui
import win32ui
import win32con

from PIL import Image

from ..config import cfg

from ..window_watcher import WindowWatcher

class BitBltWatcher(WindowWatcher):
    def __init__(self):
        self.hwnd_dc = None
        self.mfc_dc  = None
        self.save_dc = None
        self.bitmap  = None
        self.width   = 0
        self.height  = 0

    def OnStart(self, hwnd, title):
        # Get window device context
        self.hwnd_dc = win32gui.GetWindowDC(self.hwnd)
        self.mfc_dc  = win32ui.CreateDCFromHandle(self.hwnd_dc)
        self.save_dc = self.mfc_dc.CreateCompatibleDC()

        # Limit to 60 FPS
        delay = 1 / 60
        while True:
            start_time = time.time()

            # Capture frame
            image, success = self.CaptureWindow()
            if not success:
                self.OnClosed()
                break
            self.OnFrameArrived(image)

            if cfg.DEBUG_SAVE:
                image.save(os.path.join(cfg.debug_dir, "save", f"bitblt.png"))

            # Sleep to maintain 60 FPS
            dt = delay - (time.time() - start_time)
            if dt > 0:
                time.sleep(dt)
    
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

    def CaptureWindow(self):
        try:
            # Get window rect
            window_left, window_top, window_right, window_bot = win32gui.GetWindowRect(self.hwnd)
            window_width = window_right - window_left
            window_height = window_bot - window_top

            # Get client rect
            client_left, client_top, client_right, client_bot = win32gui.GetClientRect(self.hwnd)
            client_width = client_right - client_left
            client_height = client_bot - client_top

            if client_width != self.width or client_height != self.height:
                self.DestroyBitmap()
                self.CreateBitmap(client_width, client_height)

            # BitBlt to capture the window frame
            client_left, client_top = win32gui.ClientToScreen(self.hwnd, (client_left, client_top))
            self.save_dc.BitBlt(
                (0, 0), (client_width, client_height), self.mfc_dc, 
                (client_left - window_left, client_top - window_top), win32con.SRCCOPY)

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

            return (image, True)
        except win32gui.error:
            return (None, False)
