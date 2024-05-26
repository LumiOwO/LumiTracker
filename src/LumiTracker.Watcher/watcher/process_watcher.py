import logging
import sys
import time
import psutil
import win32gui
import win32process
import subprocess

from .config import cfg

class WindowInfo:
    def __init__(self):
        self.hwnd  = 0
        self.title = ""

class ProcessWatcher:
    def StartWindowWatcher(self, info):
        # Start the subprocess
        process = subprocess.Popen([sys.executable, "-E", "-m", "watcher.window_watcher", str(int(info.hwnd)), info.title])
        while True:
            return_code = process.poll()
            if return_code is not None:
                logging.info(f"Subprocess terminated with return code: {return_code}")
                return
            time.sleep(cfg.proc_watch_interval)

    def Start(self):
        while True:
            info = self.FindProcessWindow()
            if info.hwnd:
                self.StartWindowWatcher(info)
    
            time.sleep(cfg.proc_watch_interval)

    def FindProcessWindow(self):
        process_name = cfg.proc_name

        info = WindowInfo()
        processes = self.GetProcessByName(process_name)
        if not processes:
            logging.info(f"No process found with name: {process_name}")
            return info
        
        if len(processes) > 1:
            logging.warning(f"Found multiple processes with name: {process_name}, using the first one")
        proc = processes[0]

        infos = self.GetWindowsByPID(proc.info['pid'])
        if not infos:
            logging.info(f"No windows found for process '{process_name}' (PID: {proc.info['pid']})")
            return info
        
        foreground_hwnd = win32gui.GetForegroundWindow()
        if infos[0].hwnd != foreground_hwnd:
            logging.info(f"Window for process '{process_name}' (PID: {proc.info['pid']}) is not foreground")
            return info

        info = infos[0]
        logging.info(f"Window titles for process '{process_name}' (PID: {proc.info['pid']}):")
        for i in infos:
            logging.info(f"  - {i.title}")

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
        def callback(hwnd, infos):
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
    process_watcher = ProcessWatcher()
    process_watcher.Start()
