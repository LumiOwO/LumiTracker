from windows_capture import WindowsCapture, Frame, InternalCaptureControl
import os
import time
import psutil
import win32api
import win32con
import win32print
import win32gui
import win32process

from PIL import Image

from .database import cfg, ExtractFeature, Database

def ExtractFrameFeature(frame: Frame):
    buffer = frame.frame_buffer[:, :, 2::-1] # bgr to rgb
    image  = Image.fromarray(buffer)

    # Note: no resize here, currently good result
    return ExtractFeature(image)


class WindowInfo:
    def __init__(self):
        self.hwnd  = None
        self.title = ""

class WindowWatcher:
    def __init__(self):
        self.capture         = None
        self.db              = Database()

        self.hwnd            = None
        self.border_size     = (0, 0)
        self.window_size     = (0, 0) # border included
        self.client_size     = (0, 0)

        self.prev_frame_time = time.time()
        self.prev_log_time   = self.prev_frame_time
        self.FPS_LIMIT       = 30
        self.LOG_FRAMES      = 200
        self._frame_count    = 0

        # (y, x), same as screen coordinate
        # namely (width, height)
        self.event_screen_size = (0.1400, 0.4270)
        self.my_event_pos      = (0.1225, 0.1755)
        self.op_event_pos      = (0.7380, 0.1755)

    def Start(self, hwnd, title):
        self.hwnd = hwnd

        self.db.Load()

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
        hDC      = win32gui.GetDC(0)
        real_w   = win32print.GetDeviceCaps(hDC, win32con.DESKTOPHORZRES)
        screen_w = win32api.GetSystemMetrics(0)
        # real_h   = win32print.GetDeviceCaps(hDC, win32con.DESKTOPVERTRES)
        # screen_h = win32api.GetSystemMetrics(1)
        scale    = real_w / screen_w
        # print(scale)

        # Get the client rect (excludes borders and title bar)
        client_rect = win32gui.GetClientRect(self.hwnd)
        client_rect = tuple(int(i * scale) for i in client_rect)
        client_left, client_top, client_right, client_bottom = client_rect
        self.client_size = (client_right - client_left, client_bottom - client_top)

        left_border  = (self.window_size[0] - self.client_size[0]) // 2
        title_height =  self.window_size[1] - self.client_size[1]
        self.border_size = (left_border, title_height)

        # print(self.window_size)
        # print(self.client_size)
        # print(self.border_size)

    # Called Every Time A New Frame Is Available
    def on_frame_arrived(self, frame: Frame, capture_control: InternalCaptureControl):
        cur_time = time.time()
        dt = cur_time - self.prev_frame_time
        if dt < (1 / self.FPS_LIMIT):
            return
        self.prev_frame_time = cur_time

        self._frame_count += 1
        if self._frame_count >= self.LOG_FRAMES:
            print(f"FPS: {self._frame_count / (cur_time - self.prev_log_time)}")
            self._frame_count  = 0
            self.prev_log_time = cur_time

        if frame.width != self.window_size[0] or frame.height != self.window_size[1]:
            self.UpdateWindowBorderSize(frame.width, frame.height)

        event_w    = int(self.client_size[0] * self.event_screen_size[0])
        event_h    = int(self.client_size[1] * self.event_screen_size[1])
        
        # my event
        my_start_w = int(self.client_size[0] * self.my_event_pos[0]) + self.border_size[0]
        my_start_h = int(self.client_size[1] * self.my_event_pos[1]) + self.border_size[1]
        my_event_frame = frame.crop(my_start_w, my_start_h, my_start_w + event_w, my_start_h + event_h)

        my_feature = ExtractFrameFeature(my_event_frame)
        my_id, my_dist = self.db.SearchByFeature(my_feature, card_type="event")
        
        if my_dist <= cfg.threshold:
            print(f"my event: {self.db['events'][my_id].get('name_CN', 'None')}")
            print(my_dist)
        

        # op event
        op_start_w = int(self.client_size[0] * self.op_event_pos[0]) + self.border_size[0]
        op_start_h = int(self.client_size[1] * self.op_event_pos[1]) + self.border_size[1]
        op_event_frame = frame.crop(op_start_w, op_start_h, op_start_w + event_w, op_start_h + event_h)

        op_feature = ExtractFrameFeature(op_event_frame)
        op_id, op_dist = self.db.SearchByFeature(op_feature, card_type="event")
        if op_dist <= cfg.threshold:
            print(f"op event: {self.db['events'][op_id].get('name_CN', 'None')}")
        # print(ids)
        # print(distances)

        if cfg.DEBUG:
            my_event_frame.save_as_image(os.path.join(cfg.debug_dir, "save", f"my_image{self._frame_count}.png"))
            op_event_frame.save_as_image(os.path.join(cfg.debug_dir, "save", f"op_image{self._frame_count}.png"))

        # # Save The Frame As An Image To The Specified Path
        # my_event_frame.save_as_image("image.png")

        # # Gracefully Stop The Capture Thread
        # capture_control.stop()

    # Called When The Capture Item Closes Usually When The Window Closes, Capture
    # Session Will End After This Function Ends
    def on_closed(self):
        print("Capture Session Closed")

class ProcessWatcher:

    def __init__(self):
        self.window_watcher = WindowWatcher()
        self.process_name   = "YuanShen.exe"
        self.INTERVAL       = 1
    
    def Start(self):
        while True:
            info = self.FindProcessWindow()
            if info.hwnd is not None:
                self.window_watcher.Start(info.hwnd, info.title)
            time.sleep(self.INTERVAL)
    
    def FindProcessWindow(self):
        info = WindowInfo()
        processes = self.GetProcessByName(self.process_name)
        if not processes:
            print(f"No process found with name: {self.process_name}")
            return info
        else:
            for proc in processes:
                infos = self.GetWindowsByPID(proc.info['pid'])
                if infos:
                    info = infos[0]
                    print(f"Window titles for process '{self.process_name}' (PID: {proc.info['pid']}):")
                    for i in infos:
                        print(f"  - {i.title}")
                else:
                    print(f"No windows found for process '{self.process_name}' (PID: {proc.info['pid']})")
                    return infos
        
        return info

    def GetProcessByName(self, process_name):
        """Returns a list of processes matching the given process name."""
        processes = []
        for proc in psutil.process_iter(['pid', 'name']):
            if proc.info['name'].lower() == process_name.lower():
                processes.append(proc)
        return processes

    def GetWindowsByPID(self, pid):
        """Returns the window handles (HWNDs) for the given process ID."""
        def callback(hwnd, titles):
            _, found_pid = win32process.GetWindowThreadProcessId(hwnd)
            if found_pid == pid and win32gui.IsWindowVisible(hwnd):
                info = WindowInfo()
                info.hwnd  = hwnd
                info.title = win32gui.GetWindowText(hwnd)

                infos.append(info)
            return True
        
        infos = []
        win32gui.EnumWindows(callback, infos)
        return infos


if __name__ == '__main__':
    watcher = ProcessWatcher()
    watcher.Start()
