import windows_capture
from windows_capture import Frame, InternalCaptureControl

import time
import logging
from overrides import override

from ..config import cfg, LogDebug
from .base import CaptureBase

class WindowsCapture(CaptureBase):
    def __init__(self, can_hide_border):
        super().__init__()

        self.capture         = None
        self.draw_border     = False if can_hide_border else None

        self.border_size     = (0, 0)
        self.window_size     = (0, 0) # border included
        self.client_size     = (0, 0)

        self.prev_frame_time = time.perf_counter()

    @override
    def OnStart(self, hwnd):
        # Every Error From on_closed and on_frame_arrived Will End Up Here
        self.capture = windows_capture.WindowsCapture(
            cursor_capture=False,
            draw_border=self.draw_border,
            hwnd=hwnd,
        )
        self.capture.event(self.on_frame_arrived)
        self.capture.event(self.on_closed)

    @override
    def OnClosed(self):
        LogDebug(info=f"Window Capture Session Closed")

    @override
    def MainLoop(self):
        self.capture.start()

    @override
    def OnResize(self, window_width, window_height):
        self.window_size = (window_width, window_height)

        # !!! Must consider DPI scale !!!
        # https://blog.csdn.net/frostime/article/details/104798061
        # Now DpiAwareness enabled, so no need to scale
        # scale = self.GetMonitorScale()

        (client_left, client_top, client_right, client_bottom), (offset_x, offset_y) = self.GetClientRect()
        self.client_size = (client_right - client_left, client_bottom - client_top)
        left_border = (self.window_size[0] - self.client_size[0]) // 2
        self.border_size = (left_border, offset_y)

        # LogDebug(window_size=self.window_size, client_size=self.client_size, border_size=self.border_size)

        self.frame_manager.Resize(self.client_size[0], self.client_size[1])

    # Called Every Time A New Frame Is Available
    def on_frame_arrived(self, frame: Frame, capture_control: InternalCaptureControl):
        self.BeforeFrameArrived()

        if frame.width != self.window_size[0] or frame.height != self.window_size[1]:
            self.OnResize(frame.width, frame.height)

        left   = self.border_size[0]
        top    = self.border_size[1]
        frame  = frame.crop(left, top, left + self.client_size[0], top + self.client_size[1])

        self.OnFrameArrived(frame.frame_buffer)

        dt = time.perf_counter() - self.prev_frame_time
        self.WaitForFrameRateLimit(elapsed_time=dt)
        cur_time = time.perf_counter()
        self.prev_frame_time = cur_time

    # Called When The Capture Item Closes Usually When The Window Closes, Capture
    # Session Will End After This Function Ends
    def on_closed(self):
        self.Close()
