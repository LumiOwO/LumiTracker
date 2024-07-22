class TaskBase:
    def __init__(self, db):
        self.db           = db
        self.frame_buffer = None
        self.valid        = True
    
    def PreTick(self, frame_manager, frame_buffer):
        self.frame_buffer = frame_buffer
        self._PreTick(frame_manager)
    
    def Tick(self, frame_manager):
        self._Tick(frame_manager)

    def _PreTick(self, frame_manager):
        raise NotImplementedError()

    def _Tick(self, frame_manager):
        raise NotImplementedError()
    
    def OnResize(self, client_width, client_height, ratio_type):
        raise NotImplementedError()
    
    def Reset(self):
        raise NotImplementedError()