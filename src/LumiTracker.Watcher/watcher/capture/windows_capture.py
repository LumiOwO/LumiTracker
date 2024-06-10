from windows_capture import WindowsCapture, Frame, InternalCaptureControl
import time
import win32gui
import logging

from PIL import Image

from ..window_watcher import WindowWatcher

class WindowsCaptureWatcher(WindowWatcher):
    def __init__(self):
        self.capture       = None
        self.border_size   = (0, 0)
        self.window_size   = (0, 0) # border included
        self.client_size   = (0, 0)

        self.prev_frame_time = time.time()

    def OnStart(self, hwnd, title):
        # Every Error From on_closed and on_frame_arrived Will End Up Here
        self.capture = WindowsCapture(
            cursor_capture=False,
            draw_border=False,
            window_name=title,
        )
        self.capture.event(self.on_frame_arrived)
        self.capture.event(self.on_closed)

        self.capture.start()

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
        self.OnFrameArrived(image)

        # limit the speed in case of too fast
        INTERVAL = 0.01
        dt = time.time() - self.prev_frame_time
        if dt < INTERVAL:
            time.sleep(INTERVAL - dt)
        cur_time = time.time()
        self.prev_frame_time = cur_time

    # Called When The Capture Item Closes Usually When The Window Closes, Capture
    # Session Will End After This Function Ends
    def on_closed(self):
        logging.debug('"info": "Window Capture Session Closed"')
        self.OnClosed()
