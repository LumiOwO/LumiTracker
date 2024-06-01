from windows_capture import WindowsCapture, Frame, InternalCaptureControl
import os
import sys
import time
import win32api
import win32gui
import logging
import ctypes

from types import SimpleNamespace

from PIL import Image

from .config import cfg
from .database import Database
from .database import ExtractFeature, FeatureDistance, HashToFeature

def ExtractFrameFeature(frame: Frame):
    buffer = frame.frame_buffer[:, :, 2::-1] # bgr to rgb
    image  = Image.fromarray(buffer)

    # Note: no resize here, currently good result
    return ExtractFeature(image)

class StreamFilter:
    def __init__(self, null_val, valid_count=5):
        self.value        = null_val
        self.count        = 0
        self.valid_count  = valid_count
        self.null_val     = null_val
    
    def Filter(self, value):
        # push
        if value == self.null_val:
            self.value = value
            self.count = 0
        elif self.value == value:
            self.count += 1
        else:
            self.value = value
            self.count = 1

        # logging.debug(self.count, self.value)

        # read
        if self.value != self.null_val and self.count == self.valid_count:
            return self.value
        else:
            return self.null_val

class WindowWatcher:
    def __init__(self):
        self.db            = Database()
        self.db.Load()
        self.start_feature = HashToFeature(self.db["controls"]["start_hash"])

        self.capture       = None
        self.hwnd          = 0
        self.border_size   = (0, 0)
        self.window_size   = (0, 0) # border included
        self.client_size   = (0, 0)

        self.prev_log_time = time.time()
        self.frame_count   = 0
        self.skip_count    = 0

        self.filters            = SimpleNamespace()
        self.filters.my_event   = StreamFilter(null_val=-1)
        self.filters.op_event   = StreamFilter(null_val=-1)
        self.filters.game_start = StreamFilter(null_val=False)

    def Start(self, hwnd, title):
        logging.info("WindowWatcher start")

        PROCESS_PER_MONITOR_DPI_AWARE = 2
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE)
        except Exception as e:
            logging.error("Error setting DPI awareness:", e)

        self.hwnd = hwnd

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

    def DetectGameStart(self, frame):
        start_w = int(self.client_size[0] * cfg.start_screen_size[0])
        start_h = int(self.client_size[1] * cfg.start_screen_size[1])

        start_left = (1.0 - cfg.start_screen_size[0]) / 2
        start_left = int(self.client_size[0] * start_left) + self.border_size[0]
        start_top  = (1.0 - cfg.start_screen_size[1]) / 2
        start_top  = int(self.client_size[1] * start_top ) + self.border_size[1]
        start_event_frame = frame.crop(start_left, start_top, start_left + start_w, start_top + start_h)

        start_feature = ExtractFrameFeature(start_event_frame)
        dist = FeatureDistance(start_feature, self.start_feature)
        start = (dist <= cfg.threshold)
        start = self.filters.game_start.Filter(start)
        if start:
            logging.info(f"Game start")
            logging.debug(dist)
            if cfg.DEBUG_SAVE:
                buffer = start_event_frame.frame_buffer[:, :, 2::-1] # bgr to rgb
                image  = Image.fromarray(buffer)
                image.save(os.path.join(cfg.debug_dir, "save", f"start_event_frame.png"))

    def DetectEvent(self, frame):
        event_w = int(self.client_size[0] * cfg.event_screen_size[0])
        event_h = int(self.client_size[1] * cfg.event_screen_size[1])
        
        # my event
        my_left = int(self.client_size[0] * cfg.my_event_pos[0]) + self.border_size[0]
        my_top  = int(self.client_size[1] * cfg.my_event_pos[1]) + self.border_size[1]
        my_event_frame = frame.crop(my_left, my_top, my_left + event_w, my_top + event_h)

        my_feature = ExtractFrameFeature(my_event_frame)
        my_id, my_dist = self.db.SearchByFeature(my_feature, card_type="event")
        
        if my_dist > cfg.threshold:
            my_id = -1
        my_id = self.filters.my_event.Filter(my_id)

        if my_id >= 0:
            logging.info(f"my event: {self.db['events'][my_id].get('name_CN', 'None')}")
            logging.debug(my_dist)

        # op event
        op_left = int(self.client_size[0] * cfg.op_event_pos[0]) + self.border_size[0]
        op_top  = int(self.client_size[1] * cfg.op_event_pos[1]) + self.border_size[1]
        op_event_frame = frame.crop(op_left, op_top, op_left + event_w, op_top + event_h)

        op_feature = ExtractFrameFeature(op_event_frame)
        op_id, op_dist = self.db.SearchByFeature(op_feature, card_type="event")
        
        if op_dist > cfg.threshold:
            op_id = -1
        op_id = self.filters.op_event.Filter(op_id)

        if op_id >= 0:
            logging.info(f"op event: {self.db['events'][op_id].get('name_CN', 'None')}")
            logging.debug(op_dist)

        if cfg.DEBUG_SAVE:
            buffer = my_event_frame.frame_buffer[:, :, 2::-1] # bgr to rgb
            image  = Image.fromarray(buffer)
            image.save(os.path.join(cfg.debug_dir, "save", f"my_image{self.frame_count}.png"))

            buffer = op_event_frame.frame_buffer[:, :, 2::-1] # bgr to rgb
            image  = Image.fromarray(buffer)
            image.save(os.path.join(cfg.debug_dir, "save", f"op_image{self.frame_count}.png"))

    # Called Every Time A New Frame Is Available
    def on_frame_arrived(self, frame: Frame, capture_control: InternalCaptureControl):
        self.skip_count += 1
        if self.skip_count <= cfg.SKIP_FRAMES:
            return
        self.skip_count = 0

        self.frame_count += 1
        cur_time = time.time()
        if cur_time - self.prev_log_time >= cfg.LOG_INTERVAL:
            logging.info(f"FPS: {self.frame_count / (cur_time - self.prev_log_time)}")
            self.frame_count   = 0
            self.prev_log_time = cur_time

        if frame.width != self.window_size[0] or frame.height != self.window_size[1]:
            self.UpdateWindowBorderSize(frame.width, frame.height)

        self.DetectGameStart(frame)
        self.DetectEvent(frame)

        # # Save The Frame As An Image To The Specified Path
        # frame.save_as_image("image.png")

        # # Gracefully Stop The Capture Thread
        # capture_control.stop()

    # Called When The Capture Item Closes Usually When The Window Closes, Capture
    # Session Will End After This Function Ends
    def on_closed(self):
        logging.info("Window Capture Session Closed")

if __name__ == '__main__':
    assert len(sys.argv) == 3, "Wrong number of arguments"
    hwnd  = int(sys.argv[1])
    title = sys.argv[2]

    window_watcher = WindowWatcher()
    window_watcher.Start(hwnd, title)
