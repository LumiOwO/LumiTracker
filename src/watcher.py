from windows_capture import WindowsCapture, Frame, InternalCaptureControl
import os
import time
import psutil
import win32gui
import win32process

from PIL import Image

from .database import cfg, ExtractFeature, Database

def ExtractFrameFeature(frame: Frame):
    buffer = frame.frame_buffer[:, :, 2::-1] # bgr to rgb
    image  = Image.fromarray(buffer)

    # Note: no resize here, currently good result
    return ExtractFeature(image)


class WindowWatcher:
    def __init__(self):
        self.capture      = None
        self.db           = Database()

        self.prev_time    = time.time()
        self.SKIP_FRAMES  = 0
        self.LOG_FRAMES   = 200
        self._frame_count = 0

        # (y, x), same as screen coordinate
        # namely (width, height)
        self.event_screen_size = (0.1400, 0.4270)
        self.my_event_pos      = (0.1225, 0.1755)
        self.op_event_pos      = (0.7380, 0.1755)

    def Start(self, title):
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

    # Called Every Time A New Frame Is Available
    def on_frame_arrived(self, frame: Frame, capture_control: InternalCaptureControl):
        self._frame_count += 1
        if self._frame_count % (self.SKIP_FRAMES + 1) != 0:
            return
        
        if self._frame_count >= self.LOG_FRAMES:
            cur_time = time.time()
            dt = cur_time - self.prev_time
            frames = self._frame_count // (self.SKIP_FRAMES + 1)
            print(f"FPS: {frames / dt}")
            self.prev_time = cur_time
            self._frame_count = 0

        width      = frame.width
        height     = frame.height
        event_w    = int(width  * self.event_screen_size[0])
        event_h    = int(height * self.event_screen_size[1])
        
        # my event
        my_start_w = int(width  * self.my_event_pos[0])
        my_start_h = int(height * self.my_event_pos[1])
        my_event_frame = frame.crop(my_start_w, my_start_h, my_start_w + event_w, my_start_h + event_h)

        my_feature = ExtractFrameFeature(my_event_frame)
        my_id, my_dist = self.db.SearchByFeature(my_feature, card_type="event")
        
        if my_dist <= cfg.threshold:
            print(f"my event: {self.db['events'][my_id].get('name_CN', 'None')}")
            print(my_dist)
        

        # op event
        op_start_w = int(width  * self.op_event_pos[0])
        op_start_h = int(height * self.op_event_pos[1])
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
            title = self.FindProcessWindow()
            if title:
                self.window_watcher.Start(title)
            time.sleep(self.INTERVAL)
    
    def FindProcessWindow(self):
        title = ""
        processes = self.GetProcessByName(self.process_name)
        if not processes:
            print(f"No process found with name: {self.process_name}")
            return title
        else:
            for proc in processes:
                titles = self.GetTitlesByPID(proc.info['pid'])
                if titles:
                    title = titles[0]
                    print(f"Window titles for process '{self.process_name}' (PID: {proc.info['pid']}):")
                    for t in titles:
                        print(f"  - {t}")
                else:
                    print(f"No windows found for process '{self.process_name}' (PID: {proc.info['pid']})")
                    return title
        
        return title

    def GetProcessByName(self, process_name):
        """Returns a list of processes matching the given process name."""
        processes = []
        for proc in psutil.process_iter(['pid', 'name']):
            if proc.info['name'].lower() == process_name.lower():
                processes.append(proc)
        return processes

    def GetTitlesByPID(self, pid):
        """Returns the window handles (HWNDs) for the given process ID."""
        def callback(hwnd, titles):
            _, found_pid = win32process.GetWindowThreadProcessId(hwnd)
            if found_pid == pid and win32gui.IsWindowVisible(hwnd):
                titles.append(win32gui.GetWindowText(hwnd))
            return True
        
        titles = []
        win32gui.EnumWindows(callback, titles)
        return titles


if __name__ == '__main__':
    watcher = ProcessWatcher()
    watcher.Start()
