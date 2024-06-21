class TaskBase:
    def __init__(self, db, task_type):
        self.db           = db
        self.task_type    = task_type

        self.frame_buffer = None
        self.crop_box     = None
    
    def OnFrameArrived(self, frame_buffer):
        self.frame_buffer = frame_buffer

        self.Tick()
    
    def Tick(self):
        raise NotImplementedError()
    
    def OnResize(self, client_width, client_height, ratio_type):
        raise NotImplementedError()