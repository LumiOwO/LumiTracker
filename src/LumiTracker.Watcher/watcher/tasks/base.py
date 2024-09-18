class TaskBase:
    def __init__(self, frame_manager):
        self.fm = frame_manager
        self.db = frame_manager.db
        self.frame_buffer = None

    def SetFrameBuffer(self, frame_buffer):
        self.frame_buffer = frame_buffer

    def PreTick(self):
        pass

    def Tick(self):
        pass
    
    def PostTick(self):
        pass

    def OnResize(self, client_width, client_height, ratio_type):
        pass
    
    def Reset(self):
        pass