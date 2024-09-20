from abc import ABC, abstractmethod

class TaskBase(ABC):
    def __init__(self, frame_manager):
        self.fm = frame_manager
        self.db = frame_manager.db
        self.frame_buffer = None

    def SetFrameBuffer(self, frame_buffer):
        self.frame_buffer = frame_buffer

    def PreTick(self):
        pass

    def PostTick(self):
        pass

    @abstractmethod
    def Tick(self):
        raise NotImplementedError()

    @abstractmethod
    def OnResize(self, client_width, client_height, ratio_type):
        raise NotImplementedError()
    
    @abstractmethod
    def Reset(self):
        raise NotImplementedError()