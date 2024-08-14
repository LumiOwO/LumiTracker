import asyncio
import threading
import queue

class AsyncInput:
    def __init__(self):
        self.queue = queue.Queue()
        self.thread = threading.Thread(target=self.ReadInput, daemon=True)
        self.thread.start()

    def ReadInput(self):
        while True:
            user_input = input()
            self.queue.put(user_input)

    async def ReadAsync(self):
        loop = asyncio.get_event_loop()
        return await loop.run_in_executor(None, self.queue.get)

    def Read(self):
        try:
            user_input = self.queue.get_nowait()
            return user_input
        except queue.Empty:
            return ""

class InputManager:
    def __init__(self):
        self.ainput = AsyncInput()

        self.capture_save_dir = ""
    
    def ResetSignals(self):
        self.capture_save_dir = ""

    def Tick(self):
        self.ResetSignals()

        user_input = self.ainput.Read()
        if user_input:
            self.capture_save_dir = user_input
