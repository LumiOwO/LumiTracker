import threading
import queue
import socket
import json

from .config import LogDebug, LogInfo, LogError
from .enums import EInputType

class AsyncInput:
    def __init__(self, port):
        self.backend_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.backend_socket.bind(('localhost', port))
        self.port   = port
        self.conn   = None

        self.queue  = queue.Queue()
        self.thread = threading.Thread(target=self.ReadInput, daemon=True)
        self.thread.start()
    
    def Close(self):
        if self.conn is not None:
            self.conn.close()
        self.backend_socket.close()

        self.thread.join()

    def ReadInput(self):
        try:
            self.backend_socket.listen(1)
            self.conn, addr = self.backend_socket.accept()
            LogDebug(info=f"[AsyncInput] Socket connected, listening on port {self.port}.")

            buffer = ""
            while True:
                data = self.conn.recv(1024)
                if not data:
                    break
                data = data.decode('utf-8')
                buffer += data

                # Process lines from the buffer
                while '\n' in buffer:
                    message, buffer = buffer.split('\n', 1)
                    self.queue.put(message)
        
        except (socket.error, ConnectionResetError):
            # Exception will occur when socket is closed
            LogDebug(info=f"[AsyncInput] Backend socket is closed.")
        except Exception as e:
            LogError(info=f"[AsyncInput] Unexpected error: {e}")
        
    def Read(self):
        try:
            message = self.queue.get_nowait()
            return message
        except queue.Empty:
            return ""

class InputManager:
    def __init__(self, port, frame_manager):
        self.ainput = AsyncInput(port)
        self.frame_manager = frame_manager

    def Close(self):
        self.ainput.Close()

    def Tick(self):
        message = self.ainput.Read()
        if message == "":
            return

        try:
            message_data = json.loads(message)
        except Exception as e:
            LogError(info=f"[InputManager.Tick] Failed to parse input message.", message=message)
            return
        LogInfo(message_data)

        input_type = message_data["input_type"]
        if input_type == EInputType.CaptureTest.name:
            self.frame_manager.need_capture = True
        else:
            LogError(info="[InputManager.Tick] Unknown input type.")
