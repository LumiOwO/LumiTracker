import threading
import queue
import socket

from .config import LogDebug, LogError

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
                    user_input, buffer = buffer.split('\n', 1)
                    self.queue.put(user_input)
        
        except (socket.error, ConnectionResetError):
            # Exception will occur when socket is closed
            LogDebug(info=f"[AsyncInput] Backend socket is closed.")
        except Exception as e:
            LogError(info=f"[AsyncInput] Unexpected error: {e}")
        
    def Read(self):
        try:
            user_input = self.queue.get_nowait()
            return user_input
        except queue.Empty:
            return ""

class InputManager:
    def __init__(self, port):
        self.ainput = AsyncInput(port)

        self.capture_save_dir = ""
    
    def Close(self):
        self.ainput.Close()
    
    def ResetSignals(self):
        self.capture_save_dir = ""

    def Tick(self):
        self.ResetSignals()

        user_input = self.ainput.Read()
        if user_input:
            self.capture_save_dir = user_input
