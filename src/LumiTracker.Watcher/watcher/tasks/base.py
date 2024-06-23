class TaskBase:
    def __init__(self, db, task_type):
        self.db           = db
        self.task_type    = task_type

        self.frame_buffer = None
        self.crop_box     = None
    
    def OnFrameArrived(self, frame_buffer, frame_manager):
        self.frame_buffer = frame_buffer

        self.Tick(frame_manager)
    
    def Tick(self, frame_manager):
        raise NotImplementedError()
    
    def OnResize(self, client_width, client_height, ratio_type):
        raise NotImplementedError()